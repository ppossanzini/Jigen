using System.Text;
using Jigen.DataStructures;

namespace JigenTests;

public class RankFusionTests
{
  private static byte[] K(string value) => Encoding.UTF8.GetBytes(value);

  [Fact]
  public void Fuse_PrefersKeysRankedWellInBothLists()
  {
    var dense = new[] { K("a"), K("b"), K("c") };
    var sparse = new[] { K("c"), K("a"), K("d") };

    var fused = RankFusion.Fuse(dense, sparse, top: 4);

    Assert.Equal("a", Encoding.UTF8.GetString(fused[0].Key));
  }

  [Fact]
  public void Fuse_WithDenseWeightZero_ReturnsTheSparseOrder()
  {
    var dense = new[] { K("a"), K("b") };
    var sparse = new[] { K("b"), K("a") };

    var fused = RankFusion.Fuse(dense, sparse, denseWeight: 0f, sparseWeight: 1f);

    Assert.Equal(new[] { "b", "a" }, fused.Select(r => Encoding.UTF8.GetString(r.Key)).ToArray());
    Assert.All(fused, r => Assert.False(r.InDense));
    Assert.All(fused, r => Assert.True(r.InSparse));
  }

  [Fact]
  public void Fuse_WithSparseWeightZero_ReturnsTheDenseOrder()
  {
    var dense = new[] { K("a"), K("b") };
    var sparse = new[] { K("b"), K("a") };

    var fused = RankFusion.Fuse(dense, sparse, denseWeight: 1f, sparseWeight: 0f);

    Assert.Equal(new[] { "a", "b" }, fused.Select(r => Encoding.UTF8.GetString(r.Key)).ToArray());
  }

  [Fact]
  public void Fuse_WithBothWeightsZero_IsRejected()
  {
    Assert.Throws<ArgumentException>(() =>
      RankFusion.Fuse([K("a")], [K("a")], denseWeight: 0f, sparseWeight: 0f));
  }

  [Fact]
  public void Fuse_WithNegativeWeight_IsRejected()
  {
    Assert.Throws<ArgumentException>(() =>
      RankFusion.Fuse([K("a")], [K("a")], denseWeight: -1f));
  }

  [Fact]
  public void Fuse_IsDeterministicOnTies()
  {
    var dense = new[] { K("b"), K("a") };
    var sparse = new[] { K("a"), K("b") };

    var first = RankFusion.Fuse(dense, sparse, top: 2);
    var second = RankFusion.Fuse([K("b"), K("a")], [K("a"), K("b")], top: 2);

    Assert.Equal(
      first.Select(r => Convert.ToBase64String(r.Key)),
      second.Select(r => Convert.ToBase64String(r.Key)));
  }

  [Fact]
  public void Fuse_WithSingleResultList_DoesNotReorderTheOther()
  {
    var dense = new[] { K("a"), K("b"), K("c") };
    var sparse = new[] { K("c") };

    var fused = RankFusion.Fuse(dense, sparse, top: 3);

    Assert.Equal(new[] { "c", "a", "b" }, fused.Select(r => Encoding.UTF8.GetString(r.Key)).ToArray());
  }

  [Fact]
  public void Fuse_WithOneEmptyList_ReturnsTheOther()
  {
    var fused = RankFusion.Fuse([K("a"), K("b")], []);

    Assert.Equal(2, fused.Count);
    Assert.Equal("a", Encoding.UTF8.GetString(fused[0].Key));
  }
}
