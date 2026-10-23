using Jigen.Persistance;

namespace Jigen.Index.Sparse.Tests;

public class SparsePostingCursorTests
{
    [Fact]
    public async Task TraversalAfterReopeningPreservesEveryVectorAndWeightInIdentityOrder()
    {
        using var storage = new TestStorageDirectory();
        var options = Options(storage);
        var first = FirstBlock();
        var second = SecondBlock();
        var entry = Entry(first, second);
        await using (var blocks = new StoredList<SparsePostingBlock, ValueTuple>(options, default))
        {
            blocks.Add(second);
            blocks.Add(first);
            blocks.Flush();
        }

        await using var reopened = new StoredList<SparsePostingBlock, ValueTuple>(options, default);
        var cursor = new SparsePostingCursor(entry, reopened);
        Assert.Null(cursor.Current);
        Assert.Same(entry.Blocks[0], cursor.CurrentBlock);
        Assert.False(cursor.IsExhausted);

        foreach (var posting in first.Postings.Concat(second.Postings))
        {
            Assert.True(cursor.MoveNext());
            AssertPosting(cursor, posting.DocumentId.ToArray(), posting.VectorIndex, posting.Weight);
        }

        Assert.False(cursor.MoveNext());
        AssertExhausted(cursor);
    }

    [Theory]
    [InlineData(new byte[] { 0 }, 0, new byte[] { 1 }, 0, -2f)]
    [InlineData(new byte[] { 1 }, 0, new byte[] { 1 }, 0, -2f)]
    [InlineData(new byte[] { 1 }, 1, new byte[] { 1 }, 2, 3f)]
    [InlineData(new byte[] { 1 }, 2, new byte[] { 1 }, 2, 3f)]
    [InlineData(new byte[] { 1 }, 3, new byte[] { 1 }, 3, 4f)]
    [InlineData(new byte[] { 1 }, 4, new byte[] { 1, 0 }, 0, -1f)]
    [InlineData(new byte[] { 1, 0 }, 0, new byte[] { 1, 0 }, 0, -1f)]
    [InlineData(new byte[] { 1, 0, 0 }, 0, new byte[] { 3 }, 0, 0f)]
    [InlineData(new byte[] { 3 }, 0, new byte[] { 3 }, 0, 0f)]
    public async Task SeekFindsTheFirstIdentityAtOrAfterTheTarget(
        byte[] targetId, int targetVector, byte[] expectedId, int expectedVector, float expectedWeight)
    {
        using var storage = new TestStorageDirectory();
        await using var blocks = new StoredList<SparsePostingBlock, ValueTuple>(Options(storage), default);
        var first = FirstBlock();
        var second = SecondBlock();
        blocks.Add(second);
        blocks.Add(first);
        var cursor = new SparsePostingCursor(Entry(first, second), blocks);

        Assert.True(cursor.Seek(targetId, targetVector));
        AssertPosting(cursor, expectedId, expectedVector, expectedWeight);
        Assert.True(cursor.Seek(targetId, targetVector));
        AssertPosting(cursor, expectedId, expectedVector, expectedWeight);
    }

    [Fact]
    public async Task SeekingBackwardKeepsTheCurrentPostingAndAdvancingDoesNotRepeatIt()
    {
        using var storage = new TestStorageDirectory();
        await using var blocks = new StoredList<SparsePostingBlock, ValueTuple>(Options(storage), default);
        var first = FirstBlock();
        var second = SecondBlock();
        blocks.Add(second);
        blocks.Add(first);
        var cursor = new SparsePostingCursor(Entry(first, second), blocks);

        Assert.True(cursor.Seek([1], 3));
        Assert.True(cursor.Seek([0], 0));
        AssertPosting(cursor, [1], 3, 4f);
        Assert.True(cursor.MoveNext());
        AssertPosting(cursor, [1, 0], 0, -1f);
        Assert.True(cursor.Seek([3], 0));
        Assert.False(cursor.MoveNext());
        AssertExhausted(cursor);
    }

    [Fact]
    public async Task EmptyDirectoryHasNoCurrentBlockOrPosting()
    {
        using var storage = new TestStorageDirectory();
        await using var blocks = new StoredList<SparsePostingBlock, ValueTuple>(Options(storage), default);
        var cursor = new SparsePostingCursor(new SparseTermDirectoryEntry(7, []), blocks);

        AssertExhausted(cursor);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SkippedBlocksAreNotLoadedEvenWhenTheirReferencesWouldFail(bool useSeek)
    {
        using var storage = new TestStorageDirectory();
        await using var blocks = new StoredList<SparsePostingBlock, ValueTuple>(Options(storage), default);
        var first = FirstBlock();
        var second = SecondBlock();
        blocks.Add(second);
        var entry = new SparseTermDirectoryEntry(7,
            [new SparseBlockReference(99, first), new SparseBlockReference(0, second)]);
        var cursor = new SparsePostingCursor(entry, blocks);

        Assert.Null(cursor.Current);
        if (useSeek)
            Assert.True(cursor.Seek([1], 3));
        else
        {
            Assert.True(cursor.SkipBlock());
            Assert.Null(cursor.Current);
            Assert.Equal(0, cursor.CurrentBlock.BlockIndex);
            Assert.True(cursor.MoveNext());
        }
        AssertPosting(cursor, [1], 3, 4f);

        Assert.False(cursor.SkipBlock());
        AssertExhausted(cursor);
    }

    [Fact]
    public async Task SkippingAPartiallyReadBlockClearsItsPostingAndResumesAtTheNextBlock()
    {
        using var storage = new TestStorageDirectory();
        await using var blocks = new StoredList<SparsePostingBlock, ValueTuple>(Options(storage), default);
        var first = FirstBlock();
        var second = SecondBlock();
        blocks.Add(second);
        blocks.Add(first);
        var cursor = new SparsePostingCursor(Entry(first, second), blocks);

        Assert.True(cursor.MoveNext());
        AssertPosting(cursor, [1], 0, -2f);
        Assert.True(cursor.SkipBlock());
        Assert.Null(cursor.Current);
        Assert.Equal(0, cursor.CurrentBlock.BlockIndex);
        Assert.True(cursor.Seek([0], 0));
        AssertPosting(cursor, [1], 3, 4f);
        Assert.True(cursor.MoveNext());
        AssertPosting(cursor, [1, 0], 0, -1f);
    }

    [Fact]
    public async Task SeekingPastTheLastIdentityNeedsNoPayloadAndCannotRestart()
    {
        using var storage = new TestStorageDirectory();
        await using var blocks = new StoredList<SparsePostingBlock, ValueTuple>(Options(storage), default);
        var cursor = new SparsePostingCursor(Entry(FirstBlock(), SecondBlock()), blocks);

        Assert.True(cursor.SkipBlock());
        Assert.False(cursor.Seek([3], 1));
        AssertExhausted(cursor);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("count")]
    [InlineData("minimum")]
    [InlineData("maximum")]
    [InlineData("first-document")]
    [InlineData("first-vector")]
    [InlineData("last-document")]
    [InlineData("last-vector")]
    public async Task ReadingADiscordantBlockFailsWithoutPublishingAPosting(string mismatch)
    {
        using var storage = new TestStorageDirectory();
        await using var blocks = new StoredList<SparsePostingBlock, ValueTuple>(Options(storage), default);
        var expected = new SparsePostingBlock([new([1], 0, -2f), new([3], 0, 3f)]);
        var actual = mismatch switch
        {
            "count" => new SparsePostingBlock([new([1], 0, -2f)]),
            "minimum" => new SparsePostingBlock([new([1], 0, -1f), new([3], 0, 3f)]),
            "maximum" => new SparsePostingBlock([new([1], 0, -2f), new([3], 0, 4f)]),
            "first-document" => new SparsePostingBlock([new([2], 0, -2f), new([3], 0, 3f)]),
            "first-vector" => new SparsePostingBlock([new([1], 1, -2f), new([3], 0, 3f)]),
            "last-document" => new SparsePostingBlock([new([1], 0, -2f), new([4], 0, 3f)]),
            "last-vector" => new SparsePostingBlock([new([1], 0, -2f), new([3], 1, 3f)]),
            "missing" => expected,
            _ => throw new ArgumentOutOfRangeException(nameof(mismatch))
        };
        if (mismatch != "missing")
            blocks.Add(actual);
        var cursor = new SparsePostingCursor(new SparseTermDirectoryEntry(7,
            [new SparseBlockReference(0, expected)]), blocks);

        Assert.Throws<InvalidDataException>(() => cursor.MoveNext());
        Assert.Null(cursor.Current);
        Assert.False(cursor.IsExhausted);
        Assert.Throws<InvalidDataException>(() => cursor.Seek([1], 0));
        Assert.Null(cursor.Current);
        Assert.False(cursor.SkipBlock());
        AssertExhausted(cursor);
    }

    private static StoreListOptions Options(TestStorageDirectory storage) =>
        new() { FilePath = Path.Combine(storage.DirectoryPath, "collection.blocks") };

    private static SparsePostingBlock FirstBlock() => new([new([1], 0, -2f), new([1], 2, 3f)]);

    private static SparsePostingBlock SecondBlock() =>
        new([new([1], 3, 4f), new([1, 0], 0, -1f), new([3], 0, 0f)]);

    private static SparseTermDirectoryEntry Entry(SparsePostingBlock first, SparsePostingBlock second) =>
        new(7, [new SparseBlockReference(1, first), new SparseBlockReference(0, second)]);

    private static void AssertPosting(SparsePostingCursor cursor, byte[] documentId, int vectorIndex, float weight)
    {
        Assert.True(cursor.Current.HasValue);
        var posting = cursor.Current.Value;
        Assert.Equal(documentId, posting.DocumentId.ToArray());
        Assert.Equal(vectorIndex, posting.VectorIndex);
        Assert.Equal(weight, posting.Weight);
    }

    private static void AssertExhausted(SparsePostingCursor cursor)
    {
        Assert.True(cursor.IsExhausted);
        Assert.Null(cursor.Current);
        Assert.Null(cursor.CurrentBlock);
        Assert.False(cursor.MoveNext());
        Assert.False(cursor.Seek([0], 0));
        Assert.False(cursor.SkipBlock());
        Assert.Null(cursor.Current);
        Assert.Null(cursor.CurrentBlock);
    }
}
