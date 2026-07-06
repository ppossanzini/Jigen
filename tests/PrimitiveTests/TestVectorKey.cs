using Jigen.DataStructures;

namespace PrimitiveTests;

public class TestVectorKey
{
  [Fact]
  public void KeysBuiltFromTheSameSource_AreEqual()
  {
    Assert.Equal(VectorKey.From(42), VectorKey.From(42));
    Assert.Equal(VectorKey.From(42L), VectorKey.From(42L));
    Assert.Equal(VectorKey.From("chiave"), VectorKey.From("chiave"));

    var guid = Guid.NewGuid();
    Assert.Equal(VectorKey.From(guid), VectorKey.From(guid));

    Assert.True(VectorKey.From(42) == VectorKey.From(42));
    Assert.NotEqual(VectorKey.From(42), VectorKey.From(43));
  }

  [Fact]
  public void GetHashCode_MatchesForEqualKeys()
  {
    Assert.Equal(VectorKey.From("abc").GetHashCode(), VectorKey.From("abc").GetHashCode());
  }

  [Fact]
  public void WorksAsDictionaryKey()
  {
    var map = new Dictionary<VectorKey, string> { [VectorKey.From(7)] = "sette" };

    Assert.True(map.TryGetValue(VectorKey.From(7), out var value));
    Assert.Equal("sette", value);
    Assert.False(map.ContainsKey(VectorKey.From(8)));
  }

  [Fact]
  public void NullAndDefaultValues_AreHandled()
  {
    var empty = new VectorKey();

    Assert.Equal(default, empty);
    Assert.Equal(0, empty.GetHashCode());
    Assert.NotEqual(empty, VectorKey.From(1));
  }
}
