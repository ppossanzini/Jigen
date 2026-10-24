using Jigen.Persistance;

namespace Jigen.Index.Sparse.Tests;

public class SparseIndexTests
{
    [Fact]
    public async Task RankingUsesTheBestMatchingVectorRatherThanCombiningDifferentVectors()
    {
        await using var collection = new Collection();
        collection.AddTerm(17,
        [
            new([1], 0, 10), new([1], 1, 7), new([2], 0, 12), new([3], 0, -2)
        ], 1);
        collection.AddTerm(91, [new([1], 0, -20), new([1], 1, 4)], 2);
        collection.AddTerm(999, [new([3], 1, 100), new([4], 0, 100)], 1);
        var index = collection.Index();
        Dictionary<int, float> query = new() { [91] = 1, [17] = 1 };

        var hits = index.Search(query, 10);

        Assert.Equal(new[] { "02", "01", "03" }, hits.Select(hit => Convert.ToHexString(hit.DocumentId)));
        Assert.Equal(new[] { 12d, 11d, -2d }, hits.Select(hit => hit.Score));
        AssertMatchesExhaustive(collection, index, query, 10);
        AssertMatchesExhaustive(collection, index, query, 1);
    }

    [Fact]
    public async Task NonessentialTermsStillCompleteTheScoreOfEssentialCandidates()
    {
        await using var collection = new Collection();
        collection.AddTerm(1,
            [new([1], 0, 10), new([2], 0, 3), new([3], 0, 8), new([4], 0, 0)], 4);
        collection.AddTerm(2, [new([1], 0, 0), new([3], 0, 5), new([4], 0, 0)], 3);
        Dictionary<int, float> query = new() { [1] = 1, [2] = 1 };

        var hit = Assert.Single(collection.Index().Search(query, 1));

        Assert.Equal(new byte[] { 3 }, hit.DocumentId.ToArray());
        Assert.Equal(13d, hit.Score);
    }

    [Fact]
    public async Task EqualScoresUseBinaryKeyOrderAndReturnOneHitPerDocument()
    {
        await using var collection = new Collection();
        collection.AddTerm(0,
        [
            new([1], 0, 5), new([1], 1, 5), new([1, 0], 0, 5),
            new([1, 0, 0], 0, 5), new([1, 1], 0, 5), new([2], 0, 5)
        ], 2);
        Dictionary<int, float> query = new() { [0] = 1 };
        var index = collection.Index();

        foreach (var k in new[] { 1, 2, 3, 5, 20 })
            AssertMatchesExhaustive(collection, index, query, k);
        Assert.Equal(new[] { "01", "0100", "010000" },
            index.Search(query, 3).Select(hit => Convert.ToHexString(hit.DocumentId)));
    }

    [Fact]
    public async Task SignedQueriesAndZeroWeightsPreserveMatchingOnlySemantics()
    {
        await using var collection = new Collection();
        collection.AddTerm(1, [new([1], 0, 2), new([2], 0, -3), new([3], 0, 0)], 1);
        collection.AddTerm(2, [new([4], 0, 8)], 1);
        var index = collection.Index();

        Dictionary<int, float> negative = new() { [1] = -2 };
        AssertMatchesExhaustive(collection, index, negative, 10);
        Assert.Equal(new[] { 6d, 0d, -4d }, index.Search(negative, 10).Select(hit => hit.Score));

        Dictionary<int, float> zero = new() { [1] = 0 };
        AssertMatchesExhaustive(collection, index, zero, 10);
        Assert.Equal(3, index.Search(zero, 10).Count);
        Assert.All(index.Search(zero, 10), hit => Assert.Equal(0d, hit.Score));
    }

    [Fact]
    public async Task ExtremeWeightsAndCancellationOfLargeContributionsMatchDoubleAccumulation()
    {
        await using var collection = new Collection();
        float[] weights = [float.MaxValue, -float.MaxValue, 1, float.Epsilon, -float.Epsilon];
        for (var term = 0; term < weights.Length; term++)
            collection.AddTerm(term,
            [
                new([1], 0, weights[term]), new([2], 0, weights[weights.Length - term - 1]),
                new([3], 0, term % 2 == 0 ? weights[term] : -weights[term])
            ], 1 + term % 3);
        var index = collection.Index();
        Dictionary<int, float> query = new()
        {
            [4] = float.Epsilon,
            [3] = -float.MaxValue,
            [2] = 1,
            [1] = float.MaxValue,
            [0] = float.MaxValue
        };

        foreach (var k in new[] { 1, 2, 10 })
            AssertMatchesExhaustive(collection, index, query, k);
        Assert.All(index.Search(query, 10), hit => Assert.True(double.IsFinite(hit.Score)));
    }

    [Theory]
    [InlineData(0, "extreme")]
    [InlineData(1, "extreme")]
    [InlineData(17, "extreme")]
    [InlineData(31337, "extreme")]
    [InlineData(99991, "extreme")]
    [InlineData(0, "signed")]
    [InlineData(1, "signed")]
    [InlineData(17, "signed")]
    [InlineData(31337, "signed")]
    [InlineData(99991, "signed")]
    [InlineData(0, "nonnegative")]
    [InlineData(1, "nonnegative")]
    [InlineData(17, "nonnegative")]
    [InlineData(31337, "nonnegative")]
    [InlineData(99991, "nonnegative")]
    public async Task GeneratedMultivectorCorporaMatchExhaustiveSearch(int seed, string weightProfile)
    {
        await using var collection = new Collection();
        var random = new Random(seed);
        float[] weights = weightProfile switch
        {
            "extreme" =>
            [
                -float.MaxValue, float.MaxValue, -5, 3, -0.25f, 0, 0.5f,
                float.Epsilon, -float.Epsilon, MathF.BitIncrement(1), MathF.BitDecrement(1)
            ],
            "signed" => [-5, -3, -0.25f, 0, 0.5f, 1, 3, 5],
            "nonnegative" => [0, 0.25f, 0.5f, 1, 3, 5],
            _ => throw new ArgumentOutOfRangeException(nameof(weightProfile))
        };
        var postings = Enumerable.Range(0, 12).Select(_ => new List<SparsePosting>()).ToArray();
        for (var document = 0; document < 80; document++)
        {
            var prefix = (byte)(document / 3 + 1);
            byte[] documentId = (document % 3) switch
            {
                0 => [prefix],
                1 => [prefix, 0],
                _ => [prefix, 0, 0]
            };
            var vectorCount = random.Next(1, 5);
            for (var vector = 0; vector < vectorCount; vector++)
                for (var term = 0; term < postings.Length; term++)
                    if (random.NextDouble() < 0.3)
                        postings[term].Add(new SparsePosting(documentId, vector, weights[random.Next(weights.Length)]));
        }
        for (var term = 0; term < postings.Length; term++)
            collection.AddTerm(term, postings[term].ToArray(), random.Next(1, 14));
        var index = collection.Index();

        for (var iteration = 0; iteration < 35; iteration++)
        {
            Dictionary<int, float> query = [];
            foreach (var term in Enumerable.Range(0, 16).OrderBy(_ => random.Next()))
                if (random.NextDouble() < 0.4)
                    query.Add(term, weights[random.Next(weights.Length)]);
            foreach (var k in new[] { 0, 1, 2, 5, 17, 200 })
                AssertMatchesExhaustive(collection, index, query, k);
        }
    }

    [Fact]
    public async Task LowBoundIntervalsSkipUnreadablePayloadsOnceTheHeapIsFull()
    {
        await using var collection = new Collection();
        var high = new SparsePostingBlock([new([1], 0, 100), new([2], 0, 100), new([3], 0, 100)]);
        var low = new SparsePostingBlock([new([4], 0, 1), new([5], 0, 1)]);
        collection.Blocks.Add(high);
        collection.Terms.Add(new SparseTermDirectoryEntry(1,
            [new SparseBlockReference(0, high), new SparseBlockReference(99, low)]));
        collection.Terms.Add(new SparseTermDirectoryEntry(2, [new SparseBlockReference(98, low)]));
        var index = collection.Index();
        Dictionary<int, float> query = new() { [1] = 1, [2] = 1 };

        var hit = Assert.Single(index.Search(query, 1));

        Assert.Equal(new byte[] { 1 }, hit.DocumentId.ToArray());
        Assert.Equal(100d, hit.Score);
        Assert.Throws<InvalidDataException>(() => index.Search(query, 10));
    }

    [Fact]
    public async Task NonessentialOnlyCandidatesAreSkippedEvenWhenTheTotalIntervalBoundIsCompetitive()
    {
        await using var collection = new Collection();
        var essential = new SparsePostingBlock(
            [new([1], 0, 100), new([2], 0, 100), new([3], 0, 100), new([10], 0, 100)]);
        var initial = new SparsePostingBlock([new([1], 0, 0), new([3], 0, 0)]);
        var nonessential = new SparsePostingBlock([new([4], 0, 1), new([8], 0, 1)]);
        collection.Blocks.Add(essential);
        collection.Blocks.Add(initial);
        collection.Terms.Add(new SparseTermDirectoryEntry(1, [new SparseBlockReference(0, essential)]));
        collection.Terms.Add(new SparseTermDirectoryEntry(2,
            [new SparseBlockReference(1, initial), new SparseBlockReference(99, nonessential)]));
        var index = collection.Index();
        Dictionary<int, float> query = new() { [1] = 1, [2] = 1 };

        var hit = Assert.Single(index.Search(query, 1));

        Assert.Equal(new byte[] { 1 }, hit.DocumentId.ToArray());
        Assert.Equal(100d, hit.Score);
        Assert.Throws<InvalidDataException>(() => index.Search(query, 10));
    }

    [Fact]
    public async Task BoundsEqualToTheThresholdAreNotPruned()
    {
        await using var collection = new Collection();
        var initial = new SparsePostingBlock([new([1], 0, 5), new([2], 0, 5), new([3], 0, 5)]);
        var tied = new SparsePostingBlock([new([4], 0, 5)]);
        collection.Blocks.Add(initial);
        collection.Terms.Add(new SparseTermDirectoryEntry(1,
            [new SparseBlockReference(0, initial), new SparseBlockReference(99, tied)]));

        Assert.Throws<InvalidDataException>(() =>
            collection.Index().Search(new Dictionary<int, float> { [1] = 1 }, 1));
    }

    [Fact]
    public async Task LookupAndSearchSurviveFlushAndReopeningWithoutDependingOnTermPositions()
    {
        await using var collection = new Collection();
        collection.AddTerm(91, [new([1], 0, -2), new([2], 1, 3)], 1);
        collection.AddTerm(17, [new([1], 0, 4), new([1], 1, 7), new([3], 0, -1)], 2);
        collection.AddTerm(5, [], 1);
        Dictionary<int, float> query = new() { [91] = -2, [17] = 3 };
        var expected = collection.Index().Search(query, 10);

        await collection.ReopenAsync();
        var index = collection.Index();

        Assert.Equal(3, index.TermCount);
        Assert.True(index.TryGetTerm(17, out var found));
        Assert.Equal(17, found.TermId);
        Assert.True(index.TryGetTerm(5, out var empty));
        Assert.Empty(empty.Blocks);
        Assert.False(index.TryGetTerm(404, out var absent));
        Assert.Null(absent);
        AssertMatchesExhaustive(collection, index, query, 10);
        var actual = index.Search(query, 10);
        Assert.Equal(expected.Select(hit => Convert.ToHexString(hit.DocumentId)),
            actual.Select(hit => Convert.ToHexString(hit.DocumentId)));
        Assert.Equal(expected.Select(hit => hit.Score), actual.Select(hit => hit.Score));
    }

    [Fact]
    public async Task IdenticalTermAndDocumentIdentifiersStayIsolatedAcrossCollectionsAndConcurrentQueries()
    {
        await using var first = new Collection();
        first.AddTerm(17, [new([1], 0, 10)], 1);
        var firstIndex = first.Index();
        Dictionary<int, float> query = new() { [17] = 1 };
        await using (var second = new Collection())
        {
            second.AddTerm(17, [new([1], 0, -5)], 1);
            var secondIndex = second.Index();
            var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(iteration => Task.Run(() =>
                Assert.Single((iteration % 2 == 0 ? firstIndex : secondIndex).Search(query, 1)).Score)));
            Assert.Equal(Enumerable.Range(0, 20).Select(iteration => iteration % 2 == 0 ? 10d : -5d), results);
        }
        Assert.Equal(10d, Assert.Single(firstIndex.Search(query, 1)).Score);
    }

    [Fact]
    public async Task EmptyAndAbsentQueriesDoNotReadUnrelatedPostingFiles()
    {
        await using var collection = new Collection();
        var index = collection.Index();
        Assert.Empty(index.Search(new Dictionary<int, float> { [17] = 1 }, 10));
        collection.Terms.Add(new SparseTermDirectoryEntry(17,
            [new SparseBlockReference(99, new SparsePostingBlock([new([1], 0, 1)]))]));
        collection.Terms.Add(new SparseTermDirectoryEntry(5, []));
        index = collection.Index();

        Assert.Empty(index.Search(new Dictionary<int, float>(), 10));
        Assert.Empty(index.Search(new Dictionary<int, float> { [404] = 1 }, 10));
        Assert.Empty(index.Search(new Dictionary<int, float> { [5] = 1 }, 10));
        Assert.Empty(index.Search(new Dictionary<int, float> { [17] = 1 }, 0));
        Assert.Throws<InvalidDataException>(() => index.Search(new Dictionary<int, float> { [17] = 1 }, 1));
    }

    [Fact]
    public async Task DuplicateTermIdentifiersAreRejectedInsteadOfSelectingAnArbitraryPostingList()
    {
        await using var collection = new Collection();
        collection.Terms.Add(new SparseTermDirectoryEntry(17, []));
        collection.Terms.Add(new SparseTermDirectoryEntry(17, []));

        Assert.Throws<InvalidDataException>(() => collection.Index());
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public async Task NonfiniteQueryWeightsAreRejectedEvenForUnknownTerms(float weight)
    {
        await using var collection = new Collection();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            collection.Index().Search(new Dictionary<int, float> { [17] = weight }, 1));
    }

    [Fact]
    public async Task InvalidQueryArgumentsAreReportedAndCancellationDoesNotDamageSubsequentQueries()
    {
        await using var collection = new Collection();
        collection.AddTerm(17, [new([1], 0, 2)], 1);
        var index = collection.Index();
        Dictionary<int, float> query = new() { [17] = 1 };
        Assert.Throws<ArgumentNullException>(() => index.Search(null, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => index.Search(query, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => index.Search(new Dictionary<int, float> { [-1] = 1 }, 1));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() => index.Search(query, 1, cancellation.Token));
        Assert.Equal(2d, Assert.Single(index.Search(query, 1)).Score);
    }

    private static void AssertMatchesExhaustive(Collection collection, SparseIndex index,
        IReadOnlyDictionary<int, float> query, int k)
    {
        Dictionary<(string Document, int Vector), double> scores = [];
        foreach (var (term, weight) in query.OrderBy(pair => pair.Key))
            if (collection.Postings.TryGetValue(term, out var postings))
                foreach (var posting in postings)
                {
                    var identity = (Convert.ToHexString(posting.DocumentId), posting.VectorIndex);
                    scores.TryGetValue(identity, out var score);
                    scores[identity] = score + (double)weight * posting.Weight;
                }
        var expected = scores.GroupBy(pair => pair.Key.Document)
            .Select(group => (Document: group.Key, Score: group.Max(pair => pair.Value)))
            .OrderByDescending(hit => hit.Score).ThenBy(hit => hit.Document, StringComparer.Ordinal)
            .Take(k).ToArray();

        var actual = index.Search(query, k);

        Assert.Equal(expected.Select(hit => hit.Document), actual.Select(hit => Convert.ToHexString(hit.DocumentId)));
        Assert.Equal(expected.Select(hit => hit.Score), actual.Select(hit => hit.Score));
    }

    private sealed class Collection : IAsyncDisposable
    {
        private readonly TestStorageDirectory _storage = new();
        private StoreListOptions TermOptions => new() { FilePath = Path.Combine(_storage.DirectoryPath, "terms") };
        private StoreListOptions BlockOptions => new() { FilePath = Path.Combine(_storage.DirectoryPath, "blocks") };

        public StoredList<SparseTermDirectoryEntry, ValueTuple> Terms { get; private set; }
        public StoredList<SparsePostingBlock, ValueTuple> Blocks { get; private set; }
        public Dictionary<int, SparsePosting[]> Postings { get; } = [];

        public Collection()
        {
            Terms = new(TermOptions, default);
            Blocks = new(BlockOptions, default);
        }

        public SparseIndex Index() => new(Terms, Blocks);

        public void AddTerm(int termId, SparsePosting[] postings, int blockSize)
        {
            Postings.Add(termId, postings);
            var chunks = postings.OrderBy(posting => Convert.ToHexString(posting.DocumentId), StringComparer.Ordinal)
                .ThenBy(posting => posting.VectorIndex).Chunk(blockSize).ToArray();
            var references = new SparseBlockReference[chunks.Length];
            for (var index = chunks.Length - 1; index >= 0; index--)
            {
                var block = new SparsePostingBlock(chunks[index]);
                references[index] = new SparseBlockReference(Blocks.Count, block);
                Blocks.Add(block);
            }
            Terms.Add(new SparseTermDirectoryEntry(termId, references));
        }

        public async Task ReopenAsync()
        {
            Terms.Flush();
            Blocks.Flush();
            await Terms.DisposeAsync();
            await Blocks.DisposeAsync();
            Terms = new(TermOptions, default);
            Blocks = new(BlockOptions, default);
        }

        public async ValueTask DisposeAsync()
        {
            await Terms.DisposeAsync();
            await Blocks.DisposeAsync();
            _storage.Dispose();
        }
    }
}
