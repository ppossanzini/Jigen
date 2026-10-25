namespace Jigen.DataStructures;

/// <summary>
/// Structure/size metrics of a collection's sparse index. Null on
/// <see cref="CollectionInfo.Sparse"/> when the collection has no sparse index.
/// </summary>
public class SparseCollectionInfo
{
  /// <summary>Immutable segments the collection is currently read from; compaction reduces them to one.</summary>
  public int Segments { get; set; }

  /// <summary>Distinct terms across segments (a term retired by a tombstone still counts).</summary>
  public int Terms { get; set; }

  /// <summary>Posting blocks across segments, at most <see cref="BlockSize"/> postings each.</summary>
  public int Blocks { get; set; }

  /// <summary>Postings per block; the store writes blocks of 64.</summary>
  public int BlockSize { get; set; }

  /// <summary>Documents retired by a tombstone but still present until compaction.</summary>
  public int Tombstones { get; set; }

  /// <summary>On-disk size of the collection's sparse directory (manifest, tombstones, segments).</summary>
  public long SizeBytes { get; set; }
}
