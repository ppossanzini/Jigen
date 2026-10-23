using Jigen.Persistance;

namespace Jigen.Index.Sparse;

/// <summary>
/// Forward-only cursor over a term's blocks. The caller owns the block list
/// and keeps it open and unchanged for the cursor's lifetime.
/// </summary>
public sealed class SparsePostingCursor
{
    private readonly SparseTermDirectoryEntry _directory;
    private readonly StoredList<SparsePostingBlock, ValueTuple> _blocks;
    private int _blockPosition;
    private int _postingPosition = -1;
    private SparsePostingBlock _loadedBlock;

    public SparsePostingCursor(SparseTermDirectoryEntry directory,
        StoredList<SparsePostingBlock, ValueTuple> blocks)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(blocks);
        _directory = directory;
        _blocks = blocks;
    }

    public bool IsExhausted => _blockPosition == _directory.Blocks.Count;
    public SparseBlockReference CurrentBlock => IsExhausted ? null : _directory.Blocks[_blockPosition];
    public SparsePosting? Current => _postingPosition < 0 ? null : _loadedBlock.Postings[_postingPosition];

    public bool MoveNext()
    {
        if (IsExhausted)
            return false;

        if (_loadedBlock is not null && _postingPosition + 1 == _loadedBlock.Postings.Count)
            SkipBlock();

        if (IsExhausted)
            return false;

        LoadBlock();
        _postingPosition++;
        return true;
    }

    /// <summary>
    /// Positions at the first posting at or after the target, without moving backward.
    /// </summary>
    public bool Seek(ReadOnlySpan<byte> documentId, int vectorIndex)
    {
        if (documentId.IsEmpty)
            throw new ArgumentException("A seek requires a document identifier.", nameof(documentId));
        ArgumentOutOfRangeException.ThrowIfNegative(vectorIndex);
        if (IsExhausted)
            return false;

        var lower = _blockPosition;
        var upper = _directory.Blocks.Count;
        while (lower < upper)
        {
            var middle = lower + (upper - lower) / 2;
            var reference = _directory.Blocks[middle];
            if (Compare(reference.LastDocumentId, reference.LastVectorIndex, documentId, vectorIndex) < 0)
                lower = middle + 1;
            else
                upper = middle;
        }

        if (lower != _blockPosition)
            SelectBlock(lower);
        if (IsExhausted)
            return false;

        LoadBlock();
        lower = Math.Max(_postingPosition, 0);
        upper = _loadedBlock.Postings.Count;
        while (lower < upper)
        {
            var middle = lower + (upper - lower) / 2;
            var posting = _loadedBlock.Postings[middle];
            if (Compare(posting.DocumentId, posting.VectorIndex, documentId, vectorIndex) < 0)
                lower = middle + 1;
            else
                upper = middle;
        }

        _postingPosition = lower;
        return true;
    }

    /// <summary>
    /// Skips the selected block without reading a payload. Returns whether another block exists.
    /// </summary>
    public bool SkipBlock()
    {
        if (!IsExhausted)
            SelectBlock(_blockPosition + 1);
        return !IsExhausted;
    }

    private void SelectBlock(int position)
    {
        _blockPosition = position;
        _postingPosition = -1;
        _loadedBlock = null;
    }

    private void LoadBlock()
    {
        if (_loadedBlock is not null)
            return;

        var reference = CurrentBlock;
        if (reference.BlockIndex >= _blocks.Count)
            throw new InvalidDataException("Sparse block reference points outside the block list.");
        var block = _blocks[reference.BlockIndex];
        if (block.Postings.Count != reference.PostingCount ||
            block.MinimumWeight != reference.MinimumWeight ||
            block.MaximumWeight != reference.MaximumWeight ||
            Compare(block.Postings[0].DocumentId, block.Postings[0].VectorIndex,
                reference.FirstDocumentId, reference.FirstVectorIndex) != 0 ||
            Compare(block.Postings[^1].DocumentId, block.Postings[^1].VectorIndex,
                reference.LastDocumentId, reference.LastVectorIndex) != 0)
            throw new InvalidDataException("Sparse posting block does not match its directory reference.");

        _loadedBlock = block;
    }

    private static int Compare(ReadOnlySpan<byte> leftDocumentId, int leftVectorIndex,
        ReadOnlySpan<byte> rightDocumentId, int rightVectorIndex)
    {
        var comparison = leftDocumentId.SequenceCompareTo(rightDocumentId);
        return comparison != 0 ? comparison : leftVectorIndex.CompareTo(rightVectorIndex);
    }
}
