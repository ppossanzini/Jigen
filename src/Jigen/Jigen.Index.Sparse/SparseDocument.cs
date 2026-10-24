using Jigen.DataStructures;

namespace Jigen.Index.Sparse;

/// <summary>
/// One document to index: its binary key and the sparse vectors it carries.
/// Each vector holds term/weight pairs ordered by ascending term.
/// </summary>
public readonly record struct SparseDocument(byte[] DocumentId, IReadOnlyList<SparseTerm[]> Vectors);
