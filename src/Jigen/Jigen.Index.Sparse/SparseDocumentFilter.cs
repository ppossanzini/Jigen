namespace Jigen.Index.Sparse;

/// <summary>
/// Decides whether a document may enter the result. Documents rejected here are
/// skipped while searching, so they never occupy a slot of the top-k.
/// </summary>
public delegate bool SparseDocumentFilter(ReadOnlySpan<byte> documentId);
