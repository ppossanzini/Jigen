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

    public bool IsExhausted => _blockPosition == _directory.BlockReferences.Length;
    public SparseBlockReference CurrentBlock => IsExhausted ? null : _directory.BlockReferences[_blockPosition];
    public SparsePosting? Current => _postingPosition < 0 ? null : _loadedBlock.PostingItems[_postingPosition];

    public bool MoveNext()
    {
        if (IsExhausted)
            return false;

        if (_loadedBlock is not null && _postingPosition + 1 == _loadedBlock.PostingItems.Length)
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

        if (_postingPosition >= 0 &&
            TrySeekWithinLoadedBlock(documentId, vectorIndex, out var withinBlock))
            return withinBlock;

        var references = _directory.BlockReferences;
        var lower = _blockPosition;
        var upper = references.Length;
        while (lower < upper)
        {
            var middle = lower + (upper - lower) / 2;
            var reference = references[middle];
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
        var postings = _loadedBlock.PostingItems;
        lower = Math.Max(_postingPosition, 0);
        upper = postings.Length;
        while (lower < upper)
        {
            var middle = lower + (upper - lower) / 2;
            var posting = postings[middle];
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

    // Positions inside the already loaded block when the target falls in it,
    // avoiding the block search that the general path would repeat.
    private bool TrySeekWithinLoadedBlock(ReadOnlySpan<byte> documentId, int vectorIndex,
        out bool positioned)
    {
        var postings = _loadedBlock.PostingItems;
        if (Compare(postings[_postingPosition].DocumentId, postings[_postingPosition].VectorIndex,
                documentId, vectorIndex) >= 0)
        {
            positioned = true;
            return true;
        }

        if (Compare(postings[^1].DocumentId, postings[^1].VectorIndex, documentId, vectorIndex) < 0)
        {
            positioned = false;
            return false;
        }

        var lower = _postingPosition + 1;
        var upper = postings.Length;
        while (lower < upper)
        {
            var middle = lower + (upper - lower) / 2;
            var posting = postings[middle];
            if (Compare(posting.DocumentId, posting.VectorIndex, documentId, vectorIndex) < 0)
                lower = middle + 1;
            else
                upper = middle;
        }

        _postingPosition = lower;
        positioned = true;
        return true;
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
        var items = block.PostingItems;
        if (items.Length != reference.PostingCount ||
            block.MinimumWeight != reference.MinimumWeight ||
            block.MaximumWeight != reference.MaximumWeight ||
            Compare(items[0].DocumentId, items[0].VectorIndex,
                reference.FirstDocumentId, reference.FirstVectorIndex) != 0 ||
            Compare(items[^1].DocumentId, items[^1].VectorIndex,
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
