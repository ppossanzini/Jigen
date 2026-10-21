using System.Globalization;

namespace Jigen.DataStructures;

/// <summary>
/// A sparse vector: a set of (term, weight) pairs where absent terms are zero.
/// Terms are unique and strictly increasing, so the representation is canonical
/// and two vectors with the same pairs are equal in layout as well as meaning.
/// A <c>null</c> reference means "no sparse vector"; an empty instance is not a
/// valid vector and <see cref="Create"/> rejects it, which keeps "absent" and
/// "empty" distinguishable at every boundary.
/// </summary>
public sealed class SparseVector
{
  /// <summary>Default cap on the number of non-zero terms.</summary>
  public const int DefaultMaxTerms = 1024;

  private readonly ReadOnlyMemory<int> _terms;
  private readonly ReadOnlyMemory<float> _weights;

  private SparseVector(ReadOnlyMemory<int> terms, ReadOnlyMemory<float> weights)
  {
    _terms = terms;
    _weights = weights;
  }

  public int Count => _terms.Length;

  public ReadOnlyMemory<int> Terms => _terms;

  public ReadOnlyMemory<float> Weights => _weights;

  public ReadOnlySpan<int> TermsSpan => _terms.Span;

  public ReadOnlySpan<float> WeightsSpan => _weights.Span;

  /// <summary>
  /// Creates a sparse vector from term/weight arrays, validating both.
  /// The arrays are used as-is: the caller must not mutate them afterwards.
  /// </summary>
  public static SparseVector Create(int[] terms, float[] weights, int maxTerms = DefaultMaxTerms)
  {
    Validate(terms, weights, maxTerms);
    return new SparseVector(terms, weights);
  }

  /// <summary>
  /// Creates a sparse vector from term/weight pairs, sorting them by term so the
  /// result is canonical. Duplicate terms are rejected: silently summing them
  /// would store a vector different from the one the caller described.
  /// </summary>
  public static SparseVector FromPairs(
    IEnumerable<KeyValuePair<int, float>> pairs, int maxTerms = DefaultMaxTerms)
  {
    if (pairs is null) throw new ArgumentNullException(nameof(pairs));

    var ordered = pairs.OrderBy(p => p.Key).ToArray();
    var terms = new int[ordered.Length];
    var weights = new float[ordered.Length];
    for (var i = 0; i < ordered.Length; i++)
    {
      terms[i] = ordered[i].Key;
      weights[i] = ordered[i].Value;
    }

    return Create(terms, weights, maxTerms);
  }

  /// <summary>
  /// Validates a sparse vector representation: at least one term, terms unique
  /// and strictly increasing, weights finite and non-negative with at least one
  /// above zero, and no more than <paramref name="maxTerms"/> terms.
  /// </summary>
  public static void Validate(
    ReadOnlySpan<int> terms, ReadOnlySpan<float> weights, int maxTerms = DefaultMaxTerms)
  {
    if (terms.Length != weights.Length)
      throw new ArgumentException(
        $"A sparse vector needs one weight per term; received {terms.Length} terms and {weights.Length} weights.",
        nameof(weights));

    if (terms.Length == 0)
      throw new ArgumentException("A sparse vector needs at least one term.", nameof(terms));

    if (maxTerms > 0 && terms.Length > maxTerms)
      throw new ArgumentException(
        $"A sparse vector carries at most {maxTerms} terms; received {terms.Length}.",
        nameof(terms));

    var anyPositive = false;
    for (var i = 0; i < terms.Length; i++)
    {
      var term = terms[i];
      if (term < 0)
        throw new ArgumentException($"Term {term} is negative; terms are non-negative integers.", nameof(terms));

      if (i > 0 && term <= terms[i - 1])
        throw new ArgumentException(
          $"Term {term} at position {i} is not greater than the previous term {terms[i - 1]}; terms must be unique and strictly increasing.",
          nameof(terms));

      var weight = weights[i];
      if (float.IsNaN(weight) || float.IsInfinity(weight))
        throw new ArgumentException($"Weight of term {term} is not a finite number.", nameof(weights));
      if (weight < 0)
        throw new ArgumentException(
          $"Weight of term {term} is negative ({weight.ToString(CultureInfo.InvariantCulture)}); weights are non-negative.",
          nameof(weights));
      if (weight > 0) anyPositive = true;
    }

    if (!anyPositive)
      throw new ArgumentException("A sparse vector needs at least one weight greater than zero.", nameof(weights));
  }

  /// <summary>True when <paramref name="other"/> holds the same pairs.</summary>
  public bool SequenceEqual(SparseVector other)
  {
    if (other is null || other.Count != Count) return false;
    return TermsSpan.SequenceEqual(other.TermsSpan)
           && WeightsSpan.SequenceEqual(other.WeightsSpan);
  }

  public override string ToString() => $"SparseVector({Count} terms)";
}
