using System.Buffers;
using System.Collections.Concurrent;
using Jigen.Extensions;

namespace Jigen;

public partial class Store
{
  private readonly SemaphoreSlim _shrinkGate = new(1, 1);

  /// <summary>Bytes made unreachable by deletes and overwrites, reclaimable by <see cref="ShrinkAsync"/>.</summary>
  public long DeadBytes => Volatile.Read(ref DeadContentBytes) + Volatile.Read(ref DeadEmbeddingBytes);

  /// <summary>Dead/total ratio of the content and embeddings files.</summary>
  public double FragmentationRatio
  {
    get
    {
      long total = ContentFileStream.Length + EmbeddingFileStream.Length;
      return total > 0 ? (double)DeadBytes / total : 0d;
    }
  }

  /// <summary>True when both shrink thresholds in <see cref="StoreOptions"/> are exceeded.</summary>
  public bool NeedsShrink => DeadBytes >= Options.ShrinkMinDeadBytes &&
                             FragmentationRatio >= Options.ShrinkFragmentationThreshold;

  /// <summary>
  /// Compacts the content, embeddings and index files by copying live records
  /// to fresh files and swapping them in with atomic renames. Crash-safe: the
  /// original files stay intact until the rename. Ingestion is paused for the
  /// duration; reads issued exactly during the final swap may transiently fail.
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
  }

  private void CompactCore()
  {
    var contentCompact = ContentFullFileName + ".compact";
    var embeddingsCompact = EmbeddingsFullFileName + ".compact";
    var indexCompact = IndexFullFileName + ".compact";

    var entries = PositionIndex
      .SelectMany(c => c.Value.Select(kv => new CompactEntry
      {
        Collection = c.Key,
        Id = kv.Key,
        OldContentPosition = kv.Value.contentposition,
        OldEmbeddingPosition = kv.Value.embeddingsposition,
        Dimensions = kv.Value.dimensions,
        Size = kv.Value.size
      }))
      .ToList();

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
        entry.NewContentPosition = newContent.Position;
        CopyRecord(ContentFileStream, entry.OldContentPosition, ContentRecordSize(entry.Id.Length, entry.Size), newContent);
      }

      foreach (var entry in entries.Where(e => e.OldEmbeddingPosition > 0).OrderBy(e => e.OldEmbeddingPosition))
      {
        entry.NewEmbeddingPosition = newEmbeddings.Position;
        CopyRecord(EmbeddingFileStream, entry.OldEmbeddingPosition, EmbeddingRecordSize(entry.Id.Length, entry.Dimensions), newEmbeddings);
      }

      // Rewrite the index log from scratch: only live entries, no tombstones.
      foreach (var entry in entries)
        StoreWritingExtensions.WriteIndexRecord(newIndex, entry.Id, entry.Collection,
          entry.NewContentPosition, entry.NewEmbeddingPosition, entry.Dimensions, entry.Size);

      // fsync the compact files BEFORE the rename: a crash at any point leaves
      // either the originals or fully-written replacements, never a mix.
      newContent.Flush(true);
      newEmbeddings.Flush(true);
      newIndex.Flush(true);
    }

    var newPositionIndex = new ConcurrentDictionary<string, ConcurrentDictionary<byte[], (long contentposition, long embeddingsposition, int dimensions, long size)>>();
    foreach (var entry in entries)
    {
      if (!newPositionIndex.TryGetValue(entry.Collection, out var collectionIndex))
        newPositionIndex[entry.Collection] = collectionIndex = new(ByteArrayEqualityComparer.Instance);
      collectionIndex[entry.Id] = (entry.NewContentPosition, entry.NewEmbeddingPosition, entry.Dimensions, entry.Size);
    }

    // Swap: atomic renames, then reopen handles on the new inodes. Old handles
    // (including in-flight memory-mapped view accessors) keep reading the old
    // data until released, so ongoing reads are not corrupted.
    var oldContent = ContentFileStream;
    var oldEmbeddings = EmbeddingFileStream;
    var oldIndex = IndexFileStream;

    File.Move(contentCompact, ContentFullFileName, overwrite: true);
    File.Move(embeddingsCompact, EmbeddingsFullFileName, overwrite: true);
    File.Move(indexCompact, IndexFullFileName, overwrite: true);

    EnableWriting();
    EnableReading();
    ReadHeader();

    PositionIndex = newPositionIndex;

    oldContent.Dispose();
    oldEmbeddings.Dispose();
    oldIndex.Dispose();

    DeadContentBytes = 0;
    DeadEmbeddingBytes = 0;
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
