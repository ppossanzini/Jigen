namespace Jigen.Index.Sparse;

public readonly struct SparsePosting
{
    private readonly byte[] _documentBuffer;
    private readonly int _documentOffset;
    private readonly int _documentLength;

    public ReadOnlySpan<byte> DocumentId => _documentBuffer is null
        ? default
        : _documentBuffer.AsSpan(_documentOffset, _documentLength);

    public int VectorIndex { get; }
    public float Weight { get; }

    public SparsePosting(ReadOnlySpan<byte> documentId, int vectorIndex, float weight)
    {
        if (documentId.IsEmpty)
            throw new ArgumentException("A posting requires a document identifier.", nameof(documentId));
        ArgumentOutOfRangeException.ThrowIfNegative(vectorIndex);
        if (!float.IsFinite(weight))
            throw new ArgumentOutOfRangeException(nameof(weight), "A posting weight must be finite.");

        _documentBuffer = documentId.ToArray();
        _documentOffset = 0;
        _documentLength = documentId.Length;
        VectorIndex = vectorIndex;
        Weight = weight;
    }

    // Shares one identifier buffer across the postings of a deserialized block,
    // avoiding one allocation per posting and keeping the identifiers contiguous.
    internal SparsePosting(byte[] documentBuffer, int documentOffset, int documentLength,
        int vectorIndex, float weight)
    {
        _documentBuffer = documentBuffer;
        _documentOffset = documentOffset;
        _documentLength = documentLength;
        VectorIndex = vectorIndex;
        Weight = weight;
    }
}
