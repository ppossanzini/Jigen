using Jigen.DataStructures;

namespace Jigen.Indexers;

/// <summary>
/// In-memory inverted index over sparse vectors: term → the entries carrying it
/// with their weight. Scoring is the dot product over the terms a query shares
/// with a document, which is what makes the search exact without a graph.
///
/// The index owns no data: every posting is a reference to a key the store
/// already holds, and the whole structure can be rebuilt from the archive.
/// </summary>
public sealed class SparseIndex : ISparseIndex
{
  private sealed class Posting
  {
    public byte[] Key;
    public float Weight;
  }

  private sealed class CollectionIndex
  {
    public readonly Dictionary<int, List<Posting>> Terms = new();
    public readonly Dictionary<byte[], Dictionary<int, float>> Entries =
      new(ByteArrayEqualityComparer.Instance);
    public int RetiredTerms;
  }

  private readonly Lock _gate = new();
  private readonly Dictionary<string, CollectionIndex> _collections = new(StringComparer.Ordinal);
  private readonly HashSet<string> _built = new(StringComparer.Ordinal);

  public void Add(string collection, byte[] key, SparseVector vector)
  {
    if (collection is null || key is null || vector is null) return;

    lock (_gate)
    {
      AddCore(GetOrCreate(collection), key, vector);
    }
  }

  public void Remove(string collection, byte[] key)
  {
    if (collection is null || key is null) return;

    lock (_gate)
    {
      if (!_collections.TryGetValue(collection, out var index)) return;
      RemoveCore(index, key);
    }
  }

  public void Clear(string collection)
  {
    lock (_gate)
    {
      _collections.Remove(collection);
      _built.Remove(collection);
    }
  }

  public void ClearAll()
  {
    lock (_gate)
    {
      _collections.Clear();
      _built.Clear();
    }
  }

  public void Rebuild(string collection, IEnumerable<(byte[] Key, SparseVector Vector)> entries)
  {
    if (collection is null || entries is null) return;

    lock (_gate)
    {
      var index = GetOrCreate(collection);
      foreach (var (key, vector) in entries)
        AddCore(index, key, vector);
      _built.Add(collection);
    }
  }

  public bool IsBuilt(string collection)
  {
    lock (_gate)
      return _built.Contains(collection);
  }

  public IReadOnlyList<(byte[] Key, float Score)> Search(string collection, SparseVector query, int top)
  {
    if (query is null || top <= 0) return [];

    var scores = new Dictionary<byte[], float>(ByteArrayEqualityComparer.Instance);

    lock (_gate)
    {
      if (!_collections.TryGetValue(collection, out var index)) return [];

      var terms = query.TermsSpan;
      var weights = query.WeightsSpan;
      for (var i = 0; i < terms.Length; i++)
      {
        if (!index.Terms.TryGetValue(terms[i], out var postings)) continue;
        var queryWeight = weights[i];
        if (queryWeight <= 0) continue;

        foreach (var posting in postings)
        {
          var contribution = queryWeight * posting.Weight;
          scores[posting.Key] = scores.TryGetValue(posting.Key, out var current)
            ? current + contribution
            : contribution;
        }
      }
    }

    if (scores.Count == 0) return [];

    return scores
      .OrderByDescending(pair => pair.Value)
      .ThenBy(pair => pair.Key, ByteArrayOrderComparer.Instance)
      .Take(top)
      .Select(pair => (pair.Key, pair.Value))
      .ToArray();
  }

  public SparseIndexInfo GetInfo(string collection)
  {
    lock (_gate)
    {
      if (!_collections.TryGetValue(collection, out var index))
        return new SparseIndexInfo();

      long postings = 0;
      long bytes = 0;
      foreach (var (term, list) in index.Terms)
      {
        postings += list.Count;
        // term key + list overhead + one posting per entry (reference + weight)
        bytes += sizeof(int) + 32 + list.Count * (IntPtr.Size + sizeof(float));
      }

      foreach (var (key, terms) in index.Entries)
      {
        bytes += IntPtr.Size + key.Length + 32 + terms.Count * (sizeof(int) + sizeof(float) + 32);
      }

      return new SparseIndexInfo
      {
        Terms = index.Terms.Count,
        Postings = postings,
        AveragePostingsPerTerm = index.Terms.Count == 0 ? 0 : (double)postings / index.Terms.Count,
        MemoryBytes = bytes,
        RetiredTerms = index.RetiredTerms,
        Built = _built.Contains(collection)
      };
    }
  }

  private CollectionIndex GetOrCreate(string collection)
  {
    if (!_collections.TryGetValue(collection, out var index))
      _collections[collection] = index = new CollectionIndex();
    return index;
  }

  private void AddCore(CollectionIndex index, byte[] key, SparseVector vector)
  {
    if (key is null || vector is null) return;
    RemoveCore(index, key);

    var weights = new Dictionary<int, float>(vector.Count);
    var terms = vector.TermsSpan;
    var termWeights = vector.WeightsSpan;
    for (var i = 0; i < terms.Length; i++)
    {
      var weight = termWeights[i];
      if (weight <= 0) continue;
      weights[terms[i]] = weight;
      if (!index.Terms.TryGetValue(terms[i], out var postings))
        index.Terms[terms[i]] = postings = new List<Posting>();
      postings.Add(new Posting { Key = key, Weight = weight });
    }

    index.Entries[key] = weights;
  }

  private static void RemoveCore(CollectionIndex index, byte[] key)
  {
    if (!index.Entries.Remove(key, out var terms)) return;

    foreach (var term in terms.Keys)
    {
      if (!index.Terms.TryGetValue(term, out var postings)) continue;

      for (var i = postings.Count - 1; i >= 0; i--)
        if (ReferenceEquals(postings[i].Key, key) || postings[i].Key.AsSpan().SequenceEqual(key))
          postings.RemoveAt(i);

      if (postings.Count == 0)
      {
        index.Terms.Remove(term);
        index.RetiredTerms++;
      }
    }
  }
}

/// <summary>Orders byte-array keys so tie-breaking between equal scores is deterministic.</summary>
internal sealed class ByteArrayOrderComparer : IComparer<byte[]>
{
  public static readonly ByteArrayOrderComparer Instance = new();

  public int Compare(byte[] x, byte[] y)
  {
    if (ReferenceEquals(x, y)) return 0;
    if (x is null) return -1;
    if (y is null) return 1;
    return x.AsSpan().SequenceCompareTo(y);
  }
}
