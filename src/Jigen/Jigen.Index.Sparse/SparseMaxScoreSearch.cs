using Jigen.Persistance;

namespace Jigen.Index.Sparse;

internal static class SparseMaxScoreSearch
{
    internal static IReadOnlyList<SparseSearchResult> Execute(
        List<(SparseTermDirectoryEntry Directory, float Weight)> entries,
        StoredList<SparsePostingBlock, ValueTuple> blocks, int k, SparseDocumentFilter filter,
        CancellationToken cancellationToken)
    {
        var terms = entries.Select(entry => new Term(
            new SparsePostingCursor(entry.Directory, blocks), entry.Weight)).ToArray();
        var ordered = Enumerable.Range(0, terms.Length).ToArray();
        var boundComparer = Comparer<int>.Create((left, right) =>
        {
            var comparison = terms[left].UpperBound.CompareTo(terms[right].UpperBound);
            return comparison != 0 ? comparison : left.CompareTo(right);
        });
        var top = new TopDocuments(k, filter);
        SparsePosting? lower = null;

        while (TryGetIntervalEnd(terms, out var end))
        {
            cancellationToken.ThrowIfCancellationRequested();
            SetBounds(terms, end);
            var intervalBound = SumBounds(terms, nonessentialOnly: false);
            var selectedThreshold = double.NaN;
            while (intervalBound >= top.Threshold)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (selectedThreshold != top.Threshold)
                {
                    selectedThreshold = top.Threshold;
                    SelectEssentialTerms(terms, ordered, boundComparer, selectedThreshold);
                }
                if (!TryGetCandidate(terms, lower, end, cancellationToken, out var candidate))
                    break;
                var score = Score(terms, candidate, cancellationToken);
                top.Collect(candidate.DocumentId, score);
                lower = candidate;
            }

            // Every bound stays valid through this inclusive interval end.
            // Only references are advanced; the next payload remains unread.
            foreach (var term in terms)
            {
                var reference = term.Cursor.CurrentBlock;
                if (reference is not null &&
                    Compare(reference.LastDocumentId, reference.LastVectorIndex, end) == 0)
                    term.Cursor.SkipBlock();
            }
            lower = end;
        }

        return top.Results();
    }

    private static bool TryGetIntervalEnd(Term[] terms, out SparsePosting end)
    {
        SparseBlockReference earliest = null;
        foreach (var term in terms)
        {
            var reference = term.Cursor.CurrentBlock;
            if (reference is not null && (earliest is null ||
                Compare(reference.LastDocumentId, reference.LastVectorIndex,
                    earliest.LastDocumentId, earliest.LastVectorIndex) < 0))
                earliest = reference;
        }

        end = earliest is null ? default : earliest.LastPosting;
        return earliest is not null;
    }

    private static void SetBounds(Term[] terms, SparsePosting end)
    {
        foreach (var term in terms)
        {
            var reference = term.Cursor.CurrentBlock;
            term.UpperBound = reference is null ||
                Compare(reference.FirstDocumentId, reference.FirstVectorIndex, end) > 0
                ? 0
                : (double)term.Weight *
                    (term.Weight < 0 ? reference.MinimumWeight : reference.MaximumWeight);
        }
    }

    private static void SelectEssentialTerms(Term[] terms, int[] ordered,
        IComparer<int> boundComparer, double threshold)
    {
        Array.Sort(ordered, boundComparer);
        var prefix = 0;
        var bound = 0d;
        foreach (var term in terms)
        {
            term.IsEssential = true;
        }
        foreach (var index in ordered)
        {
            var term = terms[index];
            var next = AddUpperBound(bound, term.UpperBound);
            if (next >= threshold)
                break;
            term.IsEssential = false;
            bound = next;
            prefix++;
        }

        // Scoring follows term-id order. Recheck the prefix in that same order
        // so rounding cannot underestimate a score accumulated differently.
        while (prefix > 0 && SumBounds(terms, nonessentialOnly: true) >= threshold)
            terms[ordered[--prefix]].IsEssential = true;
    }

    private static double SumBounds(Term[] terms, bool nonessentialOnly)
    {
        var sum = 0d;
        foreach (var term in terms)
            if (!nonessentialOnly || !term.IsEssential)
                sum = AddUpperBound(sum, term.UpperBound);
        return sum;
    }

    private static double AddUpperBound(double left, double right) =>
        right == 0 ? left : Math.BitIncrement(left + right);

    private static bool TryGetCandidate(Term[] terms, SparsePosting? lower, SparsePosting end,
        CancellationToken cancellationToken, out SparsePosting candidate)
    {
        candidate = default;
        if (lower is { } boundary && Compare(boundary.DocumentId, boundary.VectorIndex, end) >= 0)
            return false;

        var found = false;
        foreach (var term in terms)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!term.IsEssential || !Position(term.Cursor, lower, end))
                continue;

            var posting = term.Cursor.Current.Value;
            if (!found || Compare(posting.DocumentId, posting.VectorIndex, candidate) < 0)
            {
                candidate = posting;
                found = true;
            }
        }
        return found;
    }

    private static bool Position(SparsePostingCursor cursor, SparsePosting? lower, SparsePosting end)
    {
        var reference = cursor.CurrentBlock;
        if (reference is null || Compare(reference.FirstDocumentId, reference.FirstVectorIndex, end) > 0)
            return false;

        if (lower is { } boundary)
        {
            if (!AdvancePast(cursor, boundary))
                return false;
        }
        else if (!cursor.Current.HasValue && !cursor.MoveNext())
            return false;

        var current = cursor.Current.Value;
        return Compare(current.DocumentId, current.VectorIndex, end) <= 0;
    }

    // Leaves the cursor on the first posting strictly after the boundary.
    private static bool AdvancePast(SparsePostingCursor cursor, SparsePosting boundary)
    {
        if (cursor.Current is { } current)
        {
            var comparison = Compare(current.DocumentId, current.VectorIndex, boundary);
            if (comparison > 0)
                return true;
            if (comparison == 0)
                return cursor.MoveNext();
        }

        if (!cursor.Seek(boundary.DocumentId, boundary.VectorIndex))
            return false;
        var posting = cursor.Current.Value;
        return Compare(posting.DocumentId, posting.VectorIndex, boundary) != 0 || cursor.MoveNext();
    }

    // Reads the weight of the term for an exact identity, leaving the cursor on it.
    private static bool TryReadAt(SparsePostingCursor cursor, SparsePosting candidate, out float weight)
    {
        weight = 0;
        if (cursor.Current is { } current)
        {
            var comparison = Compare(current.DocumentId, current.VectorIndex, candidate);
            if (comparison == 0)
            {
                weight = current.Weight;
                return true;
            }
            if (comparison > 0)
                return false;
        }

        if (!cursor.Seek(candidate.DocumentId, candidate.VectorIndex))
            return false;
        current = cursor.Current.Value;
        if (Compare(current.DocumentId, current.VectorIndex, candidate) != 0)
            return false;
        weight = current.Weight;
        return true;
    }

    private static double Score(Term[] terms, SparsePosting candidate, CancellationToken cancellationToken)
    {
        var score = 0d;
        foreach (var term in terms)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var reference = term.Cursor.CurrentBlock;
            if (reference is null ||
                Compare(reference.FirstDocumentId, reference.FirstVectorIndex, candidate) > 0)
                continue;
            if (TryReadAt(term.Cursor, candidate, out var weight))
                score += (double)term.Weight * weight;
        }
        return score;
    }

    private static int Compare(ReadOnlySpan<byte> documentId, int vectorIndex, SparsePosting right) =>
        Compare(documentId, vectorIndex, right.DocumentId, right.VectorIndex);

    private static int Compare(ReadOnlySpan<byte> leftDocumentId, int leftVectorIndex,
        ReadOnlySpan<byte> rightDocumentId, int rightVectorIndex)
    {
        var comparison = leftDocumentId.SequenceCompareTo(rightDocumentId);
        return comparison != 0 ? comparison : leftVectorIndex.CompareTo(rightVectorIndex);
    }

    private sealed class Term(SparsePostingCursor cursor, float weight)
    {
        public SparsePostingCursor Cursor { get; } = cursor;
        public float Weight { get; } = weight;
        public double UpperBound { get; set; }
        public bool IsEssential { get; set; }
    }

    private sealed class TopDocuments(int capacity, SparseDocumentFilter filter)
    {
        private readonly PriorityQueue<SparseSearchResult, SparseSearchResult> _heap =
            new(WorstFirstComparer.Instance);
        private byte[] _pendingId;
        private double _pendingScore;

        public double Threshold => _heap.Count == capacity ? _heap.Peek().Score : double.NegativeInfinity;

        public void Collect(ReadOnlySpan<byte> documentId, double score)
        {
            // A rejected document must never consume a slot: the caller filters
            // here rather than after the top-k has already been filled.
            if (filter is not null && !filter(documentId))
                return;

            if (_pendingId is not null && documentId.SequenceEqual(_pendingId))
            {
                _pendingScore = Math.Max(_pendingScore, score);
                return;
            }

            PublishPending();
            _pendingId = documentId.ToArray();
            _pendingScore = score;
        }

        public IReadOnlyList<SparseSearchResult> Results()
        {
            PublishPending();
            var results = _heap.UnorderedItems.Select(item => item.Element).ToArray();
            Array.Sort(results, (left, right) => WorstFirstComparer.Instance.Compare(right, left));
            return Array.AsReadOnly(results);
        }

        private void PublishPending()
        {
            if (_pendingId is null)
                return;

            var hit = new SparseSearchResult(_pendingId, _pendingScore);
            if (_heap.Count < capacity)
                _heap.Enqueue(hit, hit);
            else if (WorstFirstComparer.Instance.Compare(hit, _heap.Peek()) > 0)
                _heap.DequeueEnqueue(hit, hit);
            _pendingId = null;
        }
    }

    private sealed class WorstFirstComparer : IComparer<SparseSearchResult>
    {
        public static WorstFirstComparer Instance { get; } = new();

        public int Compare(SparseSearchResult left, SparseSearchResult right)
        {
            var comparison = left.Score.CompareTo(right.Score);
            return comparison != 0 ? comparison : right.DocumentId.SequenceCompareTo(left.DocumentId);
        }
    }
}
