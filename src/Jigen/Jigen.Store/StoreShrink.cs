using System.Buffers;
using System.Collections.Concurrent;
using Jigen.Extensions;

namespace Jigen;

public partial class Store
{
  private readonly SemaphoreSlim _shrinkGate = new(1, 1);

  /// <summary>Bytes made unreachable by deletes and overwrites, reclaimable by <see cref="ShrinkAsync"/>.</summary>
  public long DeadBytes => SharedMetadata.DeadBytes + Volatile.Read(ref DeadEmbeddingBytes)
                           + Volatile.Read(ref DeadSparseBytes);

  /// <summary>Dead/total ratio of the content and embeddings files.</summary>
  public double FragmentationRatio
  {
    get
    {
      long total = ContentFileStream.Length + SharedMetadata.DataSize + EmbeddingFileStream.Length
                + SparseFileLength;
      return total > 0 ? (double)DeadBytes / total : 0d;
    }
  }

  /// <summary>True when both shrink thresholds in <see cref="StoreOptions"/> are exceeded.</summary>
  public bool NeedsShrink => DeadBytes >= Options.ShrinkMinDeadBytes &&
                             FragmentationRatio >= Options.ShrinkFragmentationThreshold;

  /// <summary>
  /// Activates a compact shared-content generation and compacts embeddings and
  /// the index through the existing swap. Ingestion is paused for the duration;
  /// reads during the final swap may transiently fail. The vector/index swap is
  /// not a transaction across files.
  /// Returns false if a shrink is already in progress.
  /// </summary>
  public async Task<bool> ShrinkAsync()
  {
    if (!await _shrinkGate.WaitAsync(0)) return false;

    try
    {
      // Drain the ingestion queue, then take the writer's I/O lock so no batch
      // can run during compaction, and the index lock so deletes are blocked too.
      await Writer.WaitForWritingCompleted;
      await Writer.WaitForIndexingCompleted;

      Writer.RunExclusive(() =>
      {
        lock (IndexAppendLock)
          CompactCore();
      });

      if (Options.Indexer is not null)
        await Options.Indexer.ShrinkAsync();

      return true;
    }
    finally
    {
      _shrinkGate.Release();
    }
  }

  private sealed class CompactEntry
  {
    public string Collection;
    public byte[] Id;
    public long OldContentPosition;
    public long OldEmbeddingPosition;
    public long NewContentPosition;
    public long NewEmbeddingPosition;
    public int Dimensions;
    public long Size;
    public long OldSparsePosition;
    public long NewSparsePosition;
    public int SparseCount;
  }

  private CompactEntry MakeCompactEntry(string collection, byte[] id,
    (long contentposition, long embeddingsposition, int dimensions, long size) value)
  {
    var sparsePosition = 0L;
    var sparseCount = 0;
    if (SparsePositionIndex.TryGetValue(collection, out var sparsePositions)
        && sparsePositions.TryGetValue(id, out var sparse)
        && sparse.position > 0)
    {
      sparsePosition = sparse.position;
      sparseCount = sparse.count;
    }

    return new CompactEntry
    {
      Collection = collection,
      Id = id,
      OldContentPosition = value.contentposition,
      OldEmbeddingPosition = value.embeddingsposition,
      Dimensions = value.dimensions,
      Size = value.size,
      OldSparsePosition = sparsePosition,
      SparseCount = sparseCount
    };
  }

  private void CompactCore()
  {
    SharedMetadata.Compact();
    var contentCompact = ContentFullFileName + ".compact";
    var embeddingsCompact = EmbeddingsFullFileName + ".compact";
    var indexCompact = IndexFullFileName + ".compact";
    var compactSparse = SparseFilesExist;
    var sparseCompact = SparseFullFileName + ".compact";
    var sparseIndexCompact = SparseIndexFullFileName + ".compact";

    var entries = PositionIndex
      .SelectMany(c => c.Value.Select(kv => MakeCompactEntry(c.Key, kv.Key, kv.Value)))
      .ToList();

    FileStream newSparse = null;
    FileStream newSparseIndex = null;
    using (var newContent = new FileStream(contentCompact, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20))
    using (var newEmbeddings = new FileStream(embeddingsCompact, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20))
    using (var newIndex = new FileStream(indexCompact, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16))
    {
      // Same initial headers written by EnsureFileCreated: data starts at 8.
      newContent.WriteInt64Le(sizeof(long));
      newEmbeddings.WriteInt64Le(2 * sizeof(long) + sizeof(int));

      // Copy live records in file order → sequential reads on the old files.
      foreach (var entry in entries.Where(e => e.OldContentPosition > 0).OrderBy(e => e.OldContentPosition))
      {
        entry.NewContentPosition = entry.OldContentPosition;
      }

      foreach (var entry in entries.Where(e => e.OldEmbeddingPosition > 0).OrderBy(e => e.OldEmbeddingPosition))
      {
        entry.NewEmbeddingPosition = newEmbeddings.Position;
        CopyRecord(EmbeddingFileStream, entry.OldEmbeddingPosition, EmbeddingRecordSize(entry.Id.Length, entry.Dimensions), newEmbeddings);
      }

      if (compactSparse)
      {
        newSparse = new FileStream(sparseCompact, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20);
        newSparseIndex = new FileStream(sparseIndexCompact, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16);
        newSparse.WriteInt64Le(SparseHeaderSize);

        // Sparse records are copied in file order, like the dense ones, so the
        // rewrite stays sequential; a key deleted since the last position wins
        // nowhere because it is absent from the live map.
        foreach (var entry in entries.Where(e => e.OldSparsePosition > 0).OrderBy(e => e.OldSparsePosition))
        {
          entry.NewSparsePosition = newSparse.Position;
          CopyRecord(SparseFileStream, entry.OldSparsePosition,
            SparseRecordSize(entry.Id.Length, entry.SparseCount), newSparse);
        }
      }

      // Rewrite the index log from scratch: only live entries, no tombstones.
      foreach (var entry in entries)
        StoreWritingExtensions.WriteIndexRecord(newIndex, entry.Id, entry.Collection,
          entry.NewContentPosition, entry.NewEmbeddingPosition, entry.Dimensions, entry.Size);

      newContent.Flush(true);
      newEmbeddings.Flush(true);
      newIndex.Flush(true);
      newSparse?.Flush(true);
      newSparseIndex?.Flush(true);
    }

    var newPositionIndex = new ConcurrentDictionary<string, ConcurrentDictionary<byte[], (long contentposition, long embeddingsposition, int dimensions, long size)>>();
    var newSparsePositions = new ConcurrentDictionary<string, ConcurrentDictionary<byte[], (long position, int count)>>();
    foreach (var entry in entries)
    {
      if (!newPositionIndex.TryGetValue(entry.Collection, out var collectionIndex))
        newPositionIndex[entry.Collection] = collectionIndex = new(ByteArrayEqualityComparer.Instance);
      collectionIndex[entry.Id] = (entry.NewContentPosition, entry.NewEmbeddingPosition, entry.Dimensions, entry.Size);

      if (entry.NewSparsePosition > 0)
      {
        if (!newSparsePositions.TryGetValue(entry.Collection, out var sparseIndex))
          newSparsePositions[entry.Collection] = sparseIndex = new(ByteArrayEqualityComparer.Instance);
        sparseIndex[entry.Id] = (entry.NewSparsePosition, entry.SparseCount);

        WriteSparsePositionRecord(newSparseIndex!, entry.Id, entry.Collection,
          entry.NewSparsePosition, entry.SparseCount);
      }
    }

    if (compactSparse)
    {
      newSparseIndex!.Flush(true);
      newSparseIndex.Dispose();
      newSparse!.Flush(true);
      newSparse.Dispose();
    }

    // Swap: atomic renames, then reopen handles on the new inodes. Old handles
    // (including in-flight memory-mapped view accessors) keep reading the old
    // data until released, so ongoing reads are not corrupted.
    var oldContent = ContentFileStream;
    var oldEmbeddings = EmbeddingFileStream;
    var oldIndex = IndexFileStream;

    var oldSparse = SparseFileStream;
    var oldSparseIndex = SparseIndexFileStream;

    File.Move(contentCompact, ContentFullFileName, overwrite: true);
    File.Move(embeddingsCompact, EmbeddingsFullFileName, overwrite: true);
    File.Move(indexCompact, IndexFullFileName, overwrite: true);

    if (compactSparse)
    {
      File.Move(sparseCompact, SparseFullFileName, overwrite: true);
      File.Move(sparseIndexCompact, SparseIndexFullFileName, overwrite: true);
    }

    EnableWriting();
    EnableReading();
    ReadHeader();

    PositionIndex = newPositionIndex;

    // Reopen the sparse handles on the new inodes; the old ones keep serving
    // in-flight reads until released.
    SparseFileStream = null;
    SparseIndexFileStream = null;
    if (compactSparse) EnsureSparseFiles();
    SparsePositionIndex = newSparsePositions;

    oldContent.Dispose();
    oldEmbeddings.Dispose();
    oldIndex.Dispose();
    oldSparse?.Dispose();
    oldSparseIndex?.Dispose();

    DeadContentBytes = 0;
    DeadEmbeddingBytes = 0;
    DeadSparseBytes = 0;

    // After shrink the data files are a complete, self-consistent snapshot:
    // the WAL can be discarded entirely.
    if (Options.Wal?.Enabled == true && WalFileStream is not null)
    {
      lock (WalLock)
      {
        WalFileStream.SetLength(0);
        WalFileStream.Flush(true);
        WalFileStream.Seek(0, SeekOrigin.End);
      }
      CheckpointedWalPosition = 0;
      _walCheckpointer?.ResetPosition();
    }
  }

  private static void CopyRecord(FileStream source, long position, long length, FileStream destination)
  {
    var buffer = ArrayPool<byte>.Shared.Rent((int)Math.Min(length, 1 << 20));
    try
    {
      long remaining = length;
      long offset = position;
      while (remaining > 0)
      {
        int chunk = (int)Math.Min(remaining, buffer.Length);
        int read = RandomAccess.Read(source.SafeFileHandle!, buffer.AsSpan(0, chunk), offset);
        if (read <= 0)
          throw new InvalidDataException($"Unexpected end of file while compacting (position {offset}).");

        destination.Write(buffer, 0, read);
        offset += read;
        remaining -= read;
      }
    }
    finally
    {
      ArrayPool<byte>.Shared.Return(buffer);
    }
  }
}
