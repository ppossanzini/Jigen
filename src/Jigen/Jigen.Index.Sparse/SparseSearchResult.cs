namespace Jigen.Index.Sparse;

public sealed class SparseSearchResult
{
    private readonly byte[] _documentId;

    public ReadOnlySpan<byte> DocumentId => _documentId;
    public double Score { get; }

    internal SparseSearchResult(ReadOnlySpan<byte> documentId, double score)
    {
        _documentId = documentId.ToArray();
        Score = score;
    }
}
