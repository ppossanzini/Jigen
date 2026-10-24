using MessagePack;

namespace Jigen.DataStructures;

/// <summary>
/// One lexical term and its weight inside a document's sparse vector.
/// Stored as a two-element array, which keeps the payload the same size as a
/// tuple while remaining a named type.
/// </summary>
[MessagePackObject]
public readonly struct SparseTerm(int term, float weight)
{
  [Key(0)]
  public int Term { get; } = term;

  [Key(1)]
  public float Weight { get; } = weight;
}
