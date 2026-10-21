namespace Jigen.Client.BaseTypes;

/// <summary>
/// A sparse lexical vector: a set of (term, weight) pairs where absent terms are
/// zero. Terms are caller-defined ids — the server never analyzes text — and
/// must be unique, strictly increasing and non-negative; weights must be finite,
/// non-negative, with at least one above zero.
///
/// A <c>null</c> reference means "no lexical slot"; an empty vector is rejected,
/// so "absent" and "empty" stay distinguishable at the client boundary too.
/// </summary>
public sealed class SparseVector
{
  private readonly int[] _terms;
  private readonly float[] _weights;

  private SparseVector(int[] terms, float[] weights)
  {
    _terms = terms;
    _weights = weights;
  }

  public int Count => _terms.Length;

  public ReadOnlySpan<int> Terms => _terms;

  public ReadOnlySpan<float> Weights => _weights;

  /// <summary>
  /// Creates a vector from term/weight arrays, in the order given. The arrays
  /// are copied, so the caller may reuse them afterwards.
  /// </summary>
  public static SparseVector Create(int[] terms, float[] weights)
  {
    Validate(terms, weights);
    return new SparseVector((int[])terms.Clone(), (float[])weights.Clone());
  }

  /// <summary>
  /// Creates a vector from term/weight pairs, sorting them by term so the result
  /// is canonical. Duplicate terms are rejected: summing them silently would
  /// store a vector different from the one the caller described.
  /// </summary>
  public static SparseVector FromPairs(IEnumerable<KeyValuePair<int, float>> pairs)
  {
    ArgumentNullException.ThrowIfNull(pairs);

    var ordered = pairs.OrderBy(pair => pair.Key).ToArray();
    var terms = new int[ordered.Length];
    var weights = new float[ordered.Length];
    for (var i = 0; i < ordered.Length; i++)
    {
      terms[i] = ordered[i].Key;
      weights[i] = ordered[i].Value;
    }

    Validate(terms, weights);
    return new SparseVector(terms, weights);
  }

  /// <summary>
  /// Validates the representation. The client repeats the server's rules so an
  /// obviously invalid vector never reaches the network; the server validates
  /// again, and its term cap is the one that decides.
  /// </summary>
  private static void Validate(int[] terms, float[] weights)
  {
    if (terms is null) throw new ArgumentNullException(nameof(terms));
    if (weights is null) throw new ArgumentNullException(nameof(weights));

    if (terms.Length != weights.Length)
      throw new ArgumentException(
        $"A sparse vector needs one weight per term; received {terms.Length} terms and {weights.Length} weights.",
        nameof(weights));

    if (terms.Length == 0)
      throw new ArgumentException("A sparse vector needs at least one term.", nameof(terms));

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
        throw new ArgumentException($"Weight of term {term} is negative; weights are non-negative.", nameof(weights));
      if (weight > 0) anyPositive = true;
    }

    if (!anyPositive)
      throw new ArgumentException("A sparse vector needs at least one weight greater than zero.", nameof(weights));
  }
}
