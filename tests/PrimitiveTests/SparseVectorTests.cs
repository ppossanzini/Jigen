using Jigen.DataStructures;

namespace JigenTests;

public class SparseVectorTests
{
  [Fact]
  public void Create_KeepsTermsAndWeights()
  {
    var vector = SparseVector.Create([1, 3, 5], [0.5f, 1.5f, 2.5f]);

    Assert.Equal(3, vector.Count);
    Assert.Equal([1, 3, 5], vector.Terms.ToArray());
    Assert.Equal([0.5f, 1.5f, 2.5f], vector.Weights.ToArray());
  }

  [Fact]
  public void Create_WithDuplicateTerms_IsRejected()
  {
    var error = Assert.Throws<ArgumentException>(() => SparseVector.Create([2, 2], [1f, 2f]));
    Assert.Contains("not greater than", error.Message);
  }

  [Fact]
  public void Create_WithDescendingTerms_IsRejected()
  {
    Assert.Throws<ArgumentException>(() => SparseVector.Create([9, 4], [1f, 2f]));
  }

  [Fact]
  public void Create_WithNegativeTerm_IsRejected()
  {
    Assert.Throws<ArgumentException>(() => SparseVector.Create([-1], [1f]));
  }

  [Fact]
  public void Create_WithNegativeWeight_IsRejected()
  {
    Assert.Throws<ArgumentException>(() => SparseVector.Create([1], [-0.1f]));
  }

  [Fact]
  public void Create_WithNonFiniteWeight_IsRejected()
  {
    Assert.Throws<ArgumentException>(() => SparseVector.Create([1], [float.NaN]));
    Assert.Throws<ArgumentException>(() => SparseVector.Create([1], [float.PositiveInfinity]));
  }

  [Fact]
  public void Create_WithAllWeightsZero_IsRejected()
  {
    Assert.Throws<ArgumentException>(() => SparseVector.Create([1, 2], [0f, 0f]));
  }

  [Fact]
  public void Create_WithNoTerms_IsRejected()
  {
    Assert.Throws<ArgumentException>(() => SparseVector.Create([], []));
  }

  [Fact]
  public void Create_WithUnequalLengths_IsRejected()
  {
    Assert.Throws<ArgumentException>(() => SparseVector.Create([1, 2], [1f]));
  }

  [Fact]
  public void Create_BeyondMaxTerms_IsRejected()
  {
    var terms = Enumerable.Range(0, 5).ToArray();
    var weights = Enumerable.Repeat(1f, 5).ToArray();

    Assert.Throws<ArgumentException>(() => SparseVector.Create(terms, weights, maxTerms: 4));
    Assert.Equal(5, SparseVector.Create(terms, weights, maxTerms: 5).Count);
  }

  [Fact]
  public void FromPairs_OrdersTermsAndRejectsDuplicates()
  {
    var vector = SparseVector.FromPairs(
    [
      new KeyValuePair<int, float>(7, 2f),
      new KeyValuePair<int, float>(2, 1f)
    ]);

    Assert.Equal([2, 7], vector.Terms.ToArray());

    Assert.Throws<ArgumentException>(() => SparseVector.FromPairs(
    [
      new KeyValuePair<int, float>(2, 1f),
      new KeyValuePair<int, float>(2, 3f)
    ]));
  }

  [Fact]
  public void SequenceEqual_ComparesPairs()
  {
    var first = SparseVector.Create([1, 2], [1f, 2f]);
    var same = SparseVector.Create([1, 2], [1f, 2f]);
    var different = SparseVector.Create([1, 2], [1f, 3f]);

    Assert.True(first.SequenceEqual(same));
    Assert.False(first.SequenceEqual(different));
    Assert.False(first.SequenceEqual(null));
  }
}
