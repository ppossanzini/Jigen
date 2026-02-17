using System.Collections.Concurrent;
using System.IO.MemoryMappedFiles;
using System.Reflection;
using System.Text;
using Jigen.Extensions;
using Jigen.DataStructures;
using Jigen.PerformancePrimitives;

// ReSharper disable MemberCanBePrivate.Global


namespace Jigen;

public class Store : IStore, IDisposable
{
  private const int CircularWritingBufferSize = 1_000_000;
  internal readonly CircularMemoryQueue<VectorEntry> IngestionQueue = new(CircularWritingBufferSize);

  // MemoryMappedFiles only for reading
  internal MemoryMappedFile ContentData;
  internal MemoryMappedFile EmbeddingsData;

  // FileStream only for writings. 
  internal FileStream ContentFileStream;
  internal FileStream EmbeddingFileStream;
  internal FileStream IndexFileStream;

  internal readonly StoreOptions Options;
  internal readonly StoreHeader VectorStoreHeader = new();

  public Dictionary<string, Dictionary<byte[], (long contentposition, long embeddingsposition, int dimensions, long size)>> PositionIndex { get; set; } = new();

  internal readonly Writer Writer;

  internal string ContentFullFileName => Path.Combine(this.Options.DataBasePath, $"{this.Options.DataBaseName}.{StoreOptions.ContentSuffix}.jigen");
  internal string IndexFullFileName => Path.Combine(this.Options.DataBasePath, $"{this.Options.DataBaseName}.index.jigen");
  internal string EmbeddingsFullFileName => Path.Combine(this.Options.DataBasePath, $"{this.Options.DataBaseName}.{StoreOptions.EmbeddingSuffix}.jigen");

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
    return new CollectionInfo()
    {
      Name = name,
      Vectors = PositionIndex[name].Count,
      Dimensions = PositionIndex[name].Values.FirstOrDefault().dimensions,
      ContentSize = PositionIndex[name].Values.Sum(i => i.size),
      VectorSize = PositionIndex[name].Values.Sum(i => i.embeddingsposition > 0 ? i.dimensions * sizeof(float) : 0)
    };
  }

  public Store(StoreOptions options)
  {
    this.Options = options;
    EnsureFileCreated();

    EnableWriting();
    EnableReading();

    this.LoadIndex();
    this.ReadHeader();

    Writer = new Writer(this);
  }

  internal void EnableWriting()
  {
    ContentFileStream = File.Open(ContentFullFileName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
    EmbeddingFileStream = File.Open(EmbeddingsFullFileName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
    IndexFileStream = File.Open(IndexFullFileName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
  }
  
  internal void EnableReading()
  {
    var oldcontent = ContentData;
    var oldembeddings = EmbeddingsData;

    if (this.ContentFileStream.Length > 0)
      ContentData = MemoryMappedFile.CreateFromFile(File.Open(ContentFullFileName, FileMode.OpenOrCreate, FileAccess.Read, FileShare.ReadWrite),
        null, 0, MemoryMappedFileAccess.Read, HandleInheritability.None, false);

    if (this.EmbeddingFileStream.Length > 0)
      EmbeddingsData = MemoryMappedFile.CreateFromFile(File.Open(EmbeddingsFullFileName, FileMode.OpenOrCreate, FileAccess.Read, FileShare.ReadWrite),
        null, 0, MemoryMappedFileAccess.Read, HandleInheritability.None, false);

    oldcontent?.Dispose();
    oldembeddings?.Dispose();
  }

  public Task SaveIndexChanges()
  {
    return Task.CompletedTask;
  }

  public async Task SaveChangesAsync(CancellationToken? cancellationToken = null)
  {
    await Writer.WaitForWritingCompleted;
  }

  public Task Close()
  {
    if (!ContentData.SafeMemoryMappedFileHandle.IsClosed) ContentData.SafeMemoryMappedFileHandle.Close();
    if (!EmbeddingsData.SafeMemoryMappedFileHandle.IsClosed) EmbeddingsData.SafeMemoryMappedFileHandle.Close();

    Writer.Stop();

    this.ContentFileStream.Flush(true);
    this.EmbeddingFileStream.Flush(true);
    this.IndexFileStream.Flush(true);

    this.ContentFileStream.Close();
    this.EmbeddingFileStream.Close();
    this.IndexFileStream.Close();

    return Task.CompletedTask;
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
  }

  #endregion
}