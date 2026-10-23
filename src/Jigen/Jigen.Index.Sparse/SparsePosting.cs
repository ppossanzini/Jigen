namespace Jigen.Index.Sparse;

public readonly struct SparsePosting
{
    private readonly byte[] _documentId;

    public ReadOnlySpan<byte> DocumentId => _documentId;
    public int VectorIndex { get; }
    public float Weight { get; }

    public SparsePosting(ReadOnlySpan<byte> documentId, int vectorIndex, float weight)
    {
        if (documentId.IsEmpty)
            throw new ArgumentException("A posting requires a document identifier.", nameof(documentId));
        ArgumentOutOfRangeException.ThrowIfNegative(vectorIndex);
        if (!float.IsFinite(weight))
            throw new ArgumentOutOfRangeException(nameof(weight), "A posting weight must be finite.");

        _documentId = documentId.ToArray();
        VectorIndex = vectorIndex;
        Weight = weight;
    }
}
