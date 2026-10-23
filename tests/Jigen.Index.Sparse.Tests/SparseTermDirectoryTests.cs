using System.Buffers.Binary;
using Jigen.Persistance;

namespace Jigen.Index.Sparse.Tests;

public class SparseTermDirectoryTests
{
    [Fact]
    public async Task DirectoryRetainsBoundsAndVectorIntervalsWithoutOpeningPostingFiles()
    {
        using var directory = new TestStorageDirectory();
        var options = new StoreListOptions { FilePath = Path.Combine(directory.DirectoryPath, "collection.terms") };
        var first = new SparseBlockReference(91, new SparsePostingBlock(
        [
            new([1], 0, -3f),
            new([1], 2, 2f)
        ]));
        var second = new SparseBlockReference(8, new SparsePostingBlock(
        [
            new([1], 3, 4f),
            new([1, 0], 0, 1f)
        ]));

        await using (var list = new StoredList<SparseTermDirectoryEntry, ValueTuple>(options, default))
        {
            list.Add(new SparseTermDirectoryEntry(17, [first, second]));
            list.Add(new SparseTermDirectoryEntry(91, []));
            list.Flush();
        }

        await using var reopened = new StoredList<SparseTermDirectoryEntry, ValueTuple>(options, default);
        Assert.Equal(2, reopened.Count);
        var entry = reopened[0];
        Assert.Equal(17, entry.TermId);
        Assert.Equal(-3f, entry.MinimumWeight);
        Assert.Equal(4f, entry.MaximumWeight);
        Assert.Collection(entry.Blocks,
            block =>
            {
                Assert.Equal(91, block.BlockIndex);
                Assert.Equal(2, block.PostingCount);
                Assert.Equal(new byte[] { 1 }, block.FirstDocumentId.ToArray());
                Assert.Equal(0, block.FirstVectorIndex);
                Assert.Equal(new byte[] { 1 }, block.LastDocumentId.ToArray());
                Assert.Equal(2, block.LastVectorIndex);
                Assert.Equal(-3f, block.MinimumWeight);
                Assert.Equal(2f, block.MaximumWeight);
            },
            block =>
            {
                Assert.Equal(8, block.BlockIndex);
                Assert.Equal(2, block.PostingCount);
                Assert.Equal(new byte[] { 1 }, block.FirstDocumentId.ToArray());
                Assert.Equal(3, block.FirstVectorIndex);
                Assert.Equal(new byte[] { 1, 0 }, block.LastDocumentId.ToArray());
                Assert.Equal(0, block.LastVectorIndex);
                Assert.Equal(0f, block.MinimumWeight);
                Assert.Equal(4f, block.MaximumWeight);
            });

        var empty = reopened[1];
        Assert.Equal(91, empty.TermId);
        Assert.Empty(empty.Blocks);
        Assert.Equal(0f, empty.MinimumWeight);
        Assert.Equal(0f, empty.MaximumWeight);
    }

    [Fact]
    public void DirectoryOwnsItsReferenceCollectionAndDeserializedIdentifiers()
    {
        SparseBlockReference[] references = [CreateReference(7, 1, 2)];
        var entry = new SparseTermDirectoryEntry(17, references);
        references[0] = CreateReference(9, 5, 6);
        var bytes = entry.Serialize().ToArray();
        var restored = SparseTermDirectoryEntry.Deserialize(bytes, default);
        Array.Fill(bytes, (byte)0);

        foreach (var value in new[] { entry, restored })
        {
            var reference = Assert.Single(value.Blocks);
            Assert.Equal(7, reference.BlockIndex);
            Assert.Equal(new byte[] { 1 }, reference.FirstDocumentId.ToArray());
            Assert.Equal(new byte[] { 2 }, reference.LastDocumentId.ToArray());
            Assert.Equal(-2f, value.MinimumWeight);
            Assert.Equal(3f, value.MaximumWeight);
        }
    }

    [Theory]
    [InlineData(4f, 0f, 4f)]
    [InlineData(-4f, -4f, 0f)]
    [InlineData(0f, 0f, 0f)]
    public void SinglePostingReferencesRoundTripWithZeroInTheirBounds(float weight, float minimum, float maximum)
    {
        var block = new SparsePostingBlock([new([1], 2, weight)]);
        var entry = new SparseTermDirectoryEntry(0, [new SparseBlockReference(0, block)]);

        var restored = SparseTermDirectoryEntry.Deserialize(entry.Serialize(), default);

        var reference = Assert.Single(restored.Blocks);
        Assert.Equal(1, reference.PostingCount);
        Assert.Equal(reference.FirstDocumentId.ToArray(), reference.LastDocumentId.ToArray());
        Assert.Equal(2, reference.FirstVectorIndex);
        Assert.Equal(2, reference.LastVectorIndex);
        Assert.Equal(minimum, restored.MinimumWeight);
        Assert.Equal(maximum, restored.MaximumWeight);
    }

    [Fact]
    public void DirectoryUsesTheDocumentedLittleEndianVersionOneLayout()
    {
        var entry = new SparseTermDirectoryEntry(17, [CreateReference(7, 1, 2)]);
        var expected = Convert.FromHexString(
            "010000001100000001000000" +
            "0700000002000000000000C000004040" +
            "000000000000000001000000010000000102");

        Assert.Equal(expected, entry.Serialize().ToArray());
    }

    [Theory]
    [InlineData("duplicate block position")]
    [InlineData("overlapping intervals")]
    [InlineData("descending intervals")]
    public void DirectoryRejectsAmbiguousBlockReferences(string invalidity)
    {
        var first = CreateReference(7, 1, 2);
        var second = invalidity switch
        {
            "duplicate block position" => CreateReference(7, 3, 4),
            "overlapping intervals" => CreateReference(8, 2, 3),
            "descending intervals" => CreateReference(8, 0, 1),
            _ => throw new ArgumentOutOfRangeException(nameof(invalidity))
        };

        Assert.Throws<ArgumentException>(() => new SparseTermDirectoryEntry(17, [first, second]));
    }

    [Fact]
    public void ReferencesRequireAPopulatedBlockAndNonnegativePosition()
    {
        Assert.Throws<ArgumentException>(() => new SparseBlockReference(0, new SparsePostingBlock([])));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateReference(-1, 1, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SparseTermDirectoryEntry(-1, []));
        Assert.Throws<ArgumentException>(() => new SparseTermDirectoryEntry(1, [null]));
    }

    [Fact]
    public void EveryTruncatedPrefixOfAPopulatedDirectoryIsRejected()
    {
        var bytes = CreateDirectory().Serialize();
        for (var length = 0; length < bytes.Length; length++)
        {
            var prefix = bytes[..length];
            Assert.Throws<InvalidDataException>(() => SparseTermDirectoryEntry.Deserialize(prefix, default));
        }
    }

    [Theory]
    [MemberData(nameof(CorruptedDirectories))]
    public void MalformedDirectoriesAreRejectedWithoutReturningPartialMetadata(string corruption, byte[] bytes)
    {
        var error = Record.Exception(() => SparseTermDirectoryEntry.Deserialize(bytes, default));
        Assert.True(error is InvalidDataException,
            $"{corruption}: expected InvalidDataException, received {error?.GetType().Name ?? "no exception"}.");
    }

    public static IEnumerable<object[]> CorruptedDirectories()
    {
        var bytes = CreateDirectory().Serialize().ToArray();
        yield return ["unsupported version", WithInt32(bytes, 0, 2)];
        yield return ["negative term identifier", WithInt32(bytes, 4, -1)];
        yield return ["negative reference count", WithInt32(bytes, 8, -1)];
        yield return ["oversized reference count", WithInt32(bytes, 8, int.MaxValue)];
        yield return ["extra references after count", WithInt32(bytes, 8, 1)];
        yield return ["negative block position", WithInt32(bytes, 12, -1)];
        yield return ["zero posting count", WithInt32(bytes, 16, 0)];
        yield return ["negative posting count", WithInt32(bytes, 16, -1)];
        yield return ["nonfinite minimum", WithFloat(bytes, 20, float.NaN)];
        yield return ["nonfinite maximum", WithFloat(bytes, 24, float.PositiveInfinity)];
        yield return ["minimum excludes absent postings", WithFloat(bytes, 20, 1f)];
        yield return ["maximum excludes absent postings", WithFloat(bytes, 24, -1f)];
        yield return ["negative first vector index", WithInt32(bytes, 28, -1)];
        yield return ["negative last vector index", WithInt32(bytes, 32, -1)];
        yield return ["zero first identifier length", WithInt32(bytes, 36, 0)];
        yield return ["negative last identifier length", WithInt32(bytes, 40, -1)];
        yield return ["oversized first identifier length", WithInt32(bytes, 36, int.MaxValue)];
        yield return ["oversized last identifier length", WithInt32(bytes, 40, int.MaxValue)];
        yield return ["one posting with distinct endpoints", WithInt32(bytes, 16, 1)];
        yield return ["repeated block position", WithInt32(bytes, 46, 7)];
        yield return ["trailing bytes", bytes.Concat(new byte[] { 0 }).ToArray()];

        var descending = bytes.ToArray();
        descending[44] = 3;
        yield return ["descending document interval", descending];

        var equalEndpoints = bytes.ToArray();
        equalEndpoints[45] = 1;
        yield return ["multiple postings with equal endpoints", equalEndpoints];

        var overlapping = bytes.ToArray();
        overlapping[78] = 2;
        yield return ["overlapping block intervals", overlapping];

        var sameDocument = bytes.ToArray();
        sameDocument[45] = 1;
        BinaryPrimitives.WriteInt32LittleEndian(sameDocument.AsSpan(28), 2);
        BinaryPrimitives.WriteInt32LittleEndian(sameDocument.AsSpan(32), 1);
        yield return ["descending vector interval", sameDocument];

        var single = new SparseTermDirectoryEntry(1,
            [new SparseBlockReference(0, new SparsePostingBlock([new([1], 0, 1f)]))]).Serialize().ToArray();
        yield return ["one posting with negative and positive bounds", WithFloat(single, 20, -1f)];
    }

    private static SparseTermDirectoryEntry CreateDirectory() =>
        new(17, [CreateReference(7, 1, 2), CreateReference(9, 3, 4)]);

    private static SparseBlockReference CreateReference(int position, byte firstId, byte lastId) =>
        new(position, new SparsePostingBlock([new([firstId], 0, -2f), new([lastId], 0, 3f)]));

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
