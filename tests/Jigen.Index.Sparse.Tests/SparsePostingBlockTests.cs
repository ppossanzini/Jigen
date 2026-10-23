using System.Buffers.Binary;
using Jigen.Persistance;

namespace Jigen.Index.Sparse.Tests;

public class SparsePostingBlockTests
{
    [Fact]
    public async Task StoredBlocksPreserveDocumentVectorsAndFloatBitsAfterReopening()
    {
        using var directory = new TestStorageDirectory();
        var options = new StoreListOptions { FilePath = Path.Combine(directory.DirectoryPath, "collection.sparse") };
        SparsePosting[] expected =
        [
            new([1], 0, -float.MaxValue),
            new([1], 2, float.Epsilon),
            new([1, 0], 0, -0f),
            new([2], 1, float.MaxValue)
        ];

        await using (var list = new StoredList<SparsePostingBlock, ValueTuple>(options, default))
        {
            list.Add(new SparsePostingBlock(expected));
            list.Add(new SparsePostingBlock([]));
            list.Flush();
        }

        await using var reopened = new StoredList<SparsePostingBlock, ValueTuple>(options, default);
        Assert.Equal(2, reopened.Count);
        var block = reopened[0];
        Assert.Equal(expected.Length, block.Postings.Count);
        for (var index = 0; index < expected.Length; index++)
        {
            Assert.Equal(expected[index].DocumentId.ToArray(), block.Postings[index].DocumentId.ToArray());
            Assert.Equal(expected[index].VectorIndex, block.Postings[index].VectorIndex);
            Assert.Equal(
                BitConverter.SingleToInt32Bits(expected[index].Weight),
                BitConverter.SingleToInt32Bits(block.Postings[index].Weight));
        }

        Assert.Equal(-float.MaxValue, block.MinimumWeight);
        Assert.Equal(float.MaxValue, block.MaximumWeight);
        Assert.Empty(reopened[1].Postings);
        Assert.Equal(0f, reopened[1].MinimumWeight);
        Assert.Equal(0f, reopened[1].MaximumWeight);
    }

    [Fact]
    public void BlocksDoNotAliasCallerOwnedIdentifiersCollectionsOrDeserializationInput()
    {
        byte[] documentId = [1, 2];
        SparsePosting[] postings = [new(documentId, 3, 0.1f)];
        var block = new SparsePostingBlock(postings);
        documentId[0] = 99;
        postings[0] = new SparsePosting([9], 0, -10f);

        var bytes = block.Serialize().ToArray();
        var restored = SparsePostingBlock.Deserialize(bytes, default);
        Array.Fill(bytes, (byte)0);

        foreach (var value in new[] { block, restored })
        {
            var posting = Assert.Single(value.Postings);
            Assert.Equal(new byte[] { 1, 2 }, posting.DocumentId.ToArray());
            Assert.Equal(3, posting.VectorIndex);
            Assert.Equal(BitConverter.SingleToInt32Bits(0.1f), BitConverter.SingleToInt32Bits(posting.Weight));
            Assert.Equal(0f, value.MinimumWeight);
            Assert.Equal(0.1f, value.MaximumWeight);
        }
    }

    [Fact]
    public void SerializedBlockUsesTheDocumentedLittleEndianVersionOneLayout()
    {
        var expected = Convert.FromHexString(
            "0100000002000000000000C000004040" +
            "010000000100000000000000C0" +
            "01000000020000000000004040");

        Assert.Equal(expected, CreatePopulatedBlock().Serialize().ToArray());
    }

    [Theory]
    [InlineData(new float[] { }, 0f, 0f)]
    [InlineData(new[] { 1f, 4f }, 0f, 4f)]
    [InlineData(new[] { -4f, -1f }, -4f, 0f)]
    [InlineData(new[] { -2f, 3f }, -2f, 3f)]
    [InlineData(new[] { 0f }, 0f, 0f)]
    public void BoundsIncludeZeroForAbsentPostings(float[] weights, float minimum, float maximum)
    {
        var block = new SparsePostingBlock(weights.Select(
            (weight, index) => new SparsePosting([1], index, weight)));
        var restored = SparsePostingBlock.Deserialize(block.Serialize(), default);

        Assert.Equal(minimum, block.MinimumWeight);
        Assert.Equal(maximum, block.MaximumWeight);
        Assert.Equal(minimum, restored.MinimumWeight);
        Assert.Equal(maximum, restored.MaximumWeight);
    }

    [Theory]
    [InlineData(1, 0, 1, 0)]
    [InlineData(1, 2, 1, 1)]
    [InlineData(2, 0, 1, 0)]
    public void BlocksRejectDuplicateOrDescendingPostingIdentities(
        byte firstId, int firstVector, byte secondId, int secondVector)
    {
        Assert.Throws<ArgumentException>(() => new SparsePostingBlock(
        [
            new([firstId], firstVector, 1f),
            new([secondId], secondVector, 2f)
        ]));
    }

    [Fact]
    public void BlocksRejectDefaultPostingsWithoutAnIdentifier()
    {
        Assert.Throws<ArgumentException>(() => new SparsePostingBlock([default]));
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void PostingsRejectNonFiniteWeights(float weight)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SparsePosting([1], 0, weight));
    }

    [Fact]
    public void PostingsRequireADocumentIdentifierAndNonnegativeVectorIndex()
    {
        Assert.Throws<ArgumentException>(() => new SparsePosting([], 0, 1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SparsePosting([1], -1, 1f));
    }

    [Fact]
    public void EveryTruncatedPrefixOfAPopulatedBlockIsRejected()
    {
        var bytes = CreatePopulatedBlock().Serialize();
        for (var length = 0; length < bytes.Length; length++)
        {
            var prefix = bytes[..length];
            Assert.Throws<InvalidDataException>(() => SparsePostingBlock.Deserialize(prefix, default));
        }
    }

    [Theory]
    [MemberData(nameof(CorruptedPayloads))]
    public void MalformedBlocksAreRejectedInsteadOfReturningPartialPostings(string corruption, byte[] bytes)
    {
        var error = Record.Exception(() => SparsePostingBlock.Deserialize(bytes, default));
        Assert.True(error is InvalidDataException,
            $"{corruption}: expected InvalidDataException, received {error?.GetType().Name ?? "no exception"}.");
    }

    public static IEnumerable<object[]> CorruptedPayloads()
    {
        var bytes = CreatePopulatedBlock().Serialize().ToArray();
        yield return ["unsupported version", WithInt32(bytes, 0, 2)];
        yield return ["negative posting count", WithInt32(bytes, 4, -1)];
        yield return ["oversized posting count", WithInt32(bytes, 4, int.MaxValue)];
        yield return ["extra postings after count", WithInt32(bytes, 4, 1)];
        yield return ["empty document identifier", WithInt32(bytes, 16, 0)];
        yield return ["negative document identifier length", WithInt32(bytes, 16, -1)];
        yield return ["oversized document identifier length", WithInt32(bytes, 16, int.MaxValue)];
        yield return ["negative vector index", WithInt32(bytes, 21, -1)];
        yield return ["nonfinite posting weight", WithFloat(bytes, 25, float.NaN)];
        yield return ["infinite posting weight", WithFloat(bytes, 25, float.PositiveInfinity)];
        yield return ["incorrect minimum", WithFloat(bytes, 8, -100f)];
        yield return ["incorrect maximum", WithFloat(bytes, 12, 100f)];
        yield return ["nonfinite minimum", WithFloat(bytes, 8, float.NaN)];
        yield return ["nonfinite maximum", WithFloat(bytes, 12, float.PositiveInfinity)];
        yield return ["trailing bytes", bytes.Concat(new byte[] { 0 }).ToArray()];

        var duplicate = bytes.ToArray();
        duplicate[33] = 1;
        yield return ["duplicate posting identity", duplicate];

        var descending = bytes.ToArray();
        descending[20] = 3;
        yield return ["descending document identifiers", descending];

        var descendingVectors = duplicate.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(descendingVectors.AsSpan(21), 1);
        yield return ["descending vector indices for the same document", descendingVectors];
    }

    private static SparsePostingBlock CreatePopulatedBlock() => new(
    [
        new([1], 0, -2f),
        new([2], 0, 3f)
    ]);

    private static byte[] WithInt32(byte[] source, int offset, int value)
    {
        var bytes = source.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset), value);
        return bytes;
    }

    private static byte[] WithFloat(byte[] source, int offset, float value)
    {
        var bytes = source.ToArray();
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset), value);
        return bytes;
    }

}
