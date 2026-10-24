namespace Jigen.Index.Sparse;

/// <summary>What a build wrote: term count, posting count and block count.</summary>
public readonly record struct SparseIndexBuildSummary(int TermCount, int PostingCount, int BlockCount);
