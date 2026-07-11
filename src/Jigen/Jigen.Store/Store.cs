using System.Collections.Concurrent;
using System.IO.MemoryMappedFiles;
using System.Reflection;
using System.Text;
using Jigen.Extensions;
using Jigen.DataStructures;
using Jigen.PerformancePrimitives;

// ReSharper disable MemberCanBePrivate.Global


namespace Jigen;

public partial class Store : IStore, IDisposable
{
  private const int CircularWritingBufferSize = 1_000_000;
  internal readonly CircularMemoryQueue<VectorEntry> IngestionQueue = new(CircularWritingBufferSize);

  // MemoryMappedFiles only for reading
  private MemoryMappedFile _contentData;
  private MemoryMappedFile _embeddingsData;

  // Extent covered by the current mappings: appends within it are visible
  // through the shared page cache, so a remap is needed only when a read
  // targets bytes past the mapped length (instead of once per writer batch).
  private long _contentMappedLength;
  private long _embeddingsMappedLength;
  private readonly Lock _remapLock = new();

  // FileStream only for writings. 
  internal FileStream ContentFileStream;
  internal FileStream EmbeddingFileStream;
  internal FileStream IndexFileStream;

  public long IngestionQueueLength => IngestionQueue.Count;
  
  public readonly StoreOptions Options;
  internal readonly StoreHeader VectorStoreHeader = new();

  // Sentinel for index-log records marking a deletion: real positions are
  // always >= header size, so -1 can never appear in a live record.
  internal const long IndexTombstone = -1;

  // Serializes appends to IndexFileStream: they come both from the Writer
  // thread (AppendIndex) and from caller threads (DeleteContent tombstones).
  internal readonly Lock IndexAppendLock = new();

  // Bytes made unreachable by deletes and overwrites; reclaimed by ShrinkAsync.
  // Mutated only under IndexAppendLock.
  internal long DeadContentBytes;
  internal long DeadEmbeddingBytes;

  // On-disk record layouts (see StoreWritingExtensions.AppendContent):
  // content:    [id length][id][content length][content bytes]
  // embeddings: [id length][id][dimensions * float]
  internal static long ContentRecordSize(int idLength, long contentSize) => 2 * sizeof(int) + idLength + contentSize;
  internal static long EmbeddingRecordSize(int idLength, int dimensions) => sizeof(int) + idLength + (long)dimensions * sizeof(float);

  // Concurrent on both levels: mutations happen under IndexAppendLock (writer
  // thread, deletes, shrink) but lookups and enumerations come lock-free from
  // reader threads (GetContent, searches, collections).
  internal ConcurrentDictionary<string, ConcurrentDictionary<byte[], (long contentposition, long embeddingsposition, int dimensions, long size)>> PositionIndex { get; set; } = new();

  internal readonly Writer Writer;

  internal string ContentFullFileName => Path.Combine(this.Options.DataBasePath, $"{this.Options.DataBaseName}.{StoreOptions.ContentSuffix}.jigen");
  internal string IndexFullFileName => Path.Combine(this.Options.DataBasePath, $"{this.Options.DataBaseName}.index.jigen");
  internal string EmbeddingsFullFileName => Path.Combine(this.Options.DataBasePath, $"{this.Options.DataBaseName}.{StoreOptions.EmbeddingSuffix}.jigen");
  internal string LockFullFileName => Path.Combine(this.Options.DataBasePath, $"{this.Options.DataBaseName}.lock.jigen");

  // Held exclusively for the Store lifetime: the data files themselves are
  // opened with FileShare.ReadWrite (the read mappings need it), so without
  // this a second Store on the same path would silently corrupt the files.
  private FileStream _databaseLock;

  // True when the previous run did not delete the lock file: Close removes it
  // on clean shutdown, a crash leaves it behind (the OS only releases the
  // handle), so its survival marks the on-disk state as possibly inconsistent.
  private bool _uncleanShutdown;

  /// <summary>True when this database was not closed cleanly by the previous process.</summary>
  public bool WasUncleanShutdown => _uncleanShutdown;

  private void AcquireDatabaseLock()
  {
    _uncleanShutdown = File.Exists(LockFullFileName);

    try
    {
      _databaseLock = new FileStream(LockFullFileName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }
    catch (IOException ex)
    {
      throw new IOException(
        $"The database '{Options.DataBaseName}' at '{Options.DataBasePath}' is already open in another Store instance or process.", ex);
    }
  }

  public IEnumerable<string> GetFileNames()
  {
    yield return IndexFullFileName;
    yield return ContentFullFileName;
    yield return EmbeddingsFullFileName;
  }

  public IEnumerable<string> GetCollections()
  {
    return PositionIndex.Keys;
  }

  public CollectionInfo GetCollectionInfo(string name)
  {
    if (!PositionIndex.ContainsKey(name)) throw new ArgumentException("Collection not found");
    var info = new CollectionInfo()
    {
      Name = name,
      Vectors = PositionIndex[name].Count,
      Dimensions = PositionIndex[name].Values.FirstOrDefault().dimensions,
      ContentSize = PositionIndex[name].Values.Sum(i => i.size),
      VectorSize = PositionIndex[name].Values.Sum(i => i.embeddingsposition > 0 ? i.dimensions * sizeof(float) : 0)
    };

    if (Options.Indexer is IExplorableIndex explorable)
      info.Index = explorable.GetIndexInfo(name);

    return info;
  }

  public Store(StoreOptions options)
  {
    this.Options = options;
    AcquireDatabaseLock();

    try
    {
      EnsureFileCreated();

      EnableWriting();
      EnableReading();

      this.LoadIndex();
      this.ReadHeader();

      Writer = new Writer(this);

      if (_uncleanShutdown && options.ReconcileOnUncleanShutdown)
        ReconcileIndexAsync().GetAwaiter().GetResult();
    }
    catch
    {
      // Release the exclusive lock so a retry in the same process can open the
      // database. The file itself stays: the state is still possibly dirty.
      _databaseLock?.Dispose();
      throw;
    }
  }

  /// <summary>
  /// Reconciles the vector index with the store content: entries whose index
  /// updates were lost (e.g. in a crash before the index flushed) are
  /// re-indexed from their persisted embeddings, and index entries whose key
  /// no longer exists in the store are dropped. Runs automatically on open
  /// after an unclean shutdown (see <see cref="StoreOptions.ReconcileOnUncleanShutdown"/>).
  /// </summary>
  public async Task ReconcileIndexAsync()
  {
    if (Options.Indexer is null) return;

    await Options.Indexer.ReconcileAsync(this);
    await Options.Indexer.FlushAsync();
  }

  internal void EnableWriting()
  {
    ContentFileStream = File.Open(ContentFullFileName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
    EmbeddingFileStream = File.Open(EmbeddingsFullFileName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
    IndexFileStream = File.Open(IndexFullFileName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
  }

  internal void EnableReading()
  {
    var oldcontent = _contentData;
    var oldembeddings = _embeddingsData;

    // Lengths captured BEFORE mapping: the file can only grow, so a mapping
    // created later covers at least this much (understating is the safe side).
    var contentLength = this.ContentFileStream.Length;
    if (contentLength > 0)
    {
      Volatile.Write(ref _contentData, MemoryMappedFile.CreateFromFile(File.Open(ContentFullFileName, FileMode.OpenOrCreate, FileAccess.Read, FileShare.ReadWrite),
        null, 0, MemoryMappedFileAccess.Read, HandleInheritability.None, false));
      Volatile.Write(ref _contentMappedLength, contentLength);
    }

    var embeddingsLength = this.EmbeddingFileStream.Length;
    if (embeddingsLength > 0)
    {
      Volatile.Write(ref _embeddingsData, MemoryMappedFile.CreateFromFile(File.Open(EmbeddingsFullFileName, FileMode.OpenOrCreate, FileAccess.Read, FileShare.ReadWrite),
        null, 0, MemoryMappedFileAccess.Read, HandleInheritability.None, false));
      Volatile.Write(ref _embeddingsMappedLength, embeddingsLength);
    }

    if (!ReferenceEquals(oldcontent, _contentData)) oldcontent?.Dispose();
    if (!ReferenceEquals(oldembeddings, _embeddingsData)) oldembeddings?.Dispose();
  }

  private void EnsureMapped(long requiredLength, ref MemoryMappedFile mapping, ref long mappedLength)
  {
    if (Volatile.Read(ref mappedLength) >= requiredLength && Volatile.Read(ref mapping) is not null)
      return;

    lock (_remapLock)
    {
      if (Volatile.Read(ref mappedLength) >= requiredLength && Volatile.Read(ref mapping) is not null)
        return;

      EnableReading();
    }
  }

  public Task SaveIndexChanges()
  {
    this.IndexFileStream.Flush(true);
    return Task.CompletedTask;
  }

  /// <summary>
  /// Last failure recorded by the background writer or the indexer, if any.
  /// Set when a queued entry could not be persisted or indexed; cleared and
  /// rethrown by the next <see cref="SaveChangesAsync"/>.
  /// </summary>
  public Exception IngestionError => Writer.LastError;

  public async Task SaveChangesAsync(CancellationToken? cancellationToken = null)
  {
    await Writer.WaitForWritingCompleted;
    await Writer.WaitForIndexingCompleted;

    if (Options.Indexer is not null)
      await Options.Indexer.FlushAsync();

    this.ContentFileStream.Flush(true);
    this.EmbeddingFileStream.Flush(true);
    this.IndexFileStream.Flush(true);

    // Fail loud: entries accepted by AppendContent that the background writer
    // could not persist or index would otherwise be lost silently.
    var pendingError = Writer.TakePendingError();
    if (pendingError is not null)
      throw new IOException("One or more queued entries failed during background ingestion. See the inner exception for the last failure.", pendingError);

    if (Options.AutoShrink && NeedsShrink)
      await ShrinkAsync();
  }

  public MemoryMappedViewAccessor GetContentAccessor(long offset, long size)
  {
    // size == 0 means "map to end of file": the mapping must cover the
    // current file length.
    EnsureMapped(size == 0 ? ContentFileStream.Length : offset + size, ref _contentData, ref _contentMappedLength);

    while (true)
    {
      var data = Volatile.Read(ref _contentData);
      try
      {
        return data.CreateViewAccessor(offset, size, MemoryMappedFileAccess.Read);
      }
      catch (ObjectDisposedException) when (!ReferenceEquals(data, Volatile.Read(ref _contentData)))
      {
        // The mapping was swapped by EnableReading (writer batch or shrink)
        // between the read and the accessor creation: retry on the new one.
        // If the store is closed the reference is unchanged and the exception
        // propagates to the caller.
      }
    }
  }

  public MemoryMappedViewAccessor GetEmbeddingAccessor(long offset, long size)
  {
    EnsureMapped(size == 0 ? EmbeddingFileStream.Length : offset + size, ref _embeddingsData, ref _embeddingsMappedLength);

    while (true)
    {
      var data = Volatile.Read(ref _embeddingsData);
      try
      {
        return data.CreateViewAccessor(offset, size, MemoryMappedFileAccess.Read);
      }
      catch (ObjectDisposedException) when (!ReferenceEquals(data, Volatile.Read(ref _embeddingsData)))
      {
      }
    }
  }

  public bool GetCollectionIndexOf(string collection, out IReadOnlyDictionary<byte[], (long contentposition, long embeddingsposition, int dimensions, long size)> index)
  {
    var found = PositionIndex.TryGetValue(collection, out var collectionIndex);
    index = collectionIndex;
    return found;
  }

  public long ContentSize => ContentFileStream.Length;

  private int _closed;

  public async Task Close()
  {
    // Idempotent: Close followed by Dispose (or double Close) must not throw.
    if (Interlocked.Exchange(ref _closed, 1) == 1) return;

    // Stop the writer FIRST: it drains the ingestion queue, and its final
    // batch still needs the streams and recreates the read mappings.
    Writer.Stop();

    // CloseAsync flushes AND releases the index storage (file handles, flush
    // loops), which would otherwise outlive the store.
    if (Options.Indexer is not null)
      await Options.Indexer.CloseAsync();

    this.ContentFileStream.Flush(true);
    this.EmbeddingFileStream.Flush(true);
    this.IndexFileStream.Flush(true);

    if (_contentData is not null && !_contentData.SafeMemoryMappedFileHandle.IsClosed) _contentData.SafeMemoryMappedFileHandle.Close();
    if (_embeddingsData is not null && !_embeddingsData.SafeMemoryMappedFileHandle.IsClosed) _embeddingsData.SafeMemoryMappedFileHandle.Close();

    this.ContentFileStream.Close();
    this.EmbeddingFileStream.Close();
    this.IndexFileStream.Close();

    // Clean shutdown: release the exclusive lock and remove the file, which
    // doubles as the crash marker (its survival triggers reconciliation).
    _databaseLock?.Dispose();
    try
    {
      File.Delete(LockFullFileName);
    }
    catch
    {
      // Leaving the file behind only costs a spurious reconcile on next open.
    }
  }

  #region Private methods

  private void EnsureFileCreated()
  {
    if (!File.Exists(EmbeddingsFullFileName))
    {
      using var stream = File.Create(EmbeddingsFullFileName);
      stream.Seek(0, SeekOrigin.Begin);
      using var writer = new BinaryWriter(stream);

      writer.Write(VectorStoreHeader.EmbeddingCurrentPosition = 2 * sizeof(long) + sizeof(int));
      writer.Flush();

      stream.Seek(VectorStoreHeader.EmbeddingCurrentPosition, SeekOrigin.Begin);

      stream.Flush(true);
      writer.Close();
    }

    if (!File.Exists(IndexFullFileName))
    {
      using var stream = File.Create(IndexFullFileName);
      stream.Flush(true);
      stream.Close();
    }

    if (!File.Exists(ContentFullFileName))
    {
      using var stream = File.Create(ContentFullFileName);
      using var writer = new BinaryWriter(stream);
      writer.Write(VectorStoreHeader.ContentCurrentPosition = sizeof(long));

      writer.Flush();

      stream.Flush(true);
      writer.Close();
    }
  }


  public void Dispose()
  {
    Close().GetAwaiter().GetResult();
  }

  #endregion
}