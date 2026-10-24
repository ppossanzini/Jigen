using System.Numerics;
using System.Text;

namespace Jigen.DataStructures;

public class VectorEntry
{
  public byte[] Id { get; set; } = Guid.NewGuid().ToByteArray();
  public string CollectionName { get; set; }
  public ReadOnlyMemory<byte> Content { get; set; }

  /// <summary>
  /// Every vector of the entry, all with the same dimensions as the collection.
  /// An entry with one vector is a list of one: there is no separate
  /// single-vector field.
  /// </summary>
  public IReadOnlyList<ReadOnlyMemory<float>> Embeddings { get; set; } = [];

  /// <summary>
  /// Sparse vectors of the entry, one per vector and ordered by ascending term.
  /// This is the authoritative copy of the term/weight pairs: the per-term
  /// posting lists are an index derived from it and can be rebuilt from it.
  /// The vector at position i corresponds to <see cref="Embeddings"/> at position i.
  /// </summary>
  public IReadOnlyList<SparseTerm[]> SparseVectors { get; set; } = [];

  public static VectorEntry Empty => new VectorEntry()
  {
    Id = Guid.NewGuid().ToByteArray(),
    CollectionName = string.Empty,
    Content = ReadOnlyMemory<byte>.Empty
  };
}

public class VectorEntry<T>
  where T : class, new()
{
  public VectorKey Key;
  public T Content { get; set; }

  /// <summary>Every vector of the entry; a single-vector entry is a list of one.</summary>
  public IReadOnlyList<float[]> Embeddings { get; set; } = [];

  /// <summary>
  /// Sparse vectors of the entry, one per vector and ordered by ascending term.
  /// The authoritative copy of the term/weight pairs that the posting lists are
  /// derived from; the vector at position i matches <see cref="Embeddings"/> at i.
  /// </summary>
  public IReadOnlyList<SparseTerm[]> SparseVectors { get; set; } = [];
}

public record struct VectorKey
{
  public byte[] Value { get; init; }

  // The compiler-generated equality would compare Value by reference
  // (EqualityComparer<byte[]>.Default): two keys built from the same source
  // would never be equal. Compare and hash the bytes instead.
  public readonly bool Equals(VectorKey other) =>
    Value is null
      ? other.Value is null
      : other.Value is not null && Value.AsSpan().SequenceEqual(other.Value);

  public readonly override int GetHashCode() =>
    Value is null ? 0 : unchecked((int)System.IO.Hashing.XxHash32.HashToUInt32(Value));

  public static ulong ToUlong(VectorKey key) => BitConverter.ToUInt64(key.Value, 0);

  public static uint ToUint(VectorKey key) => BitConverter.ToUInt32(key.Value, 0);

  public static int ToInt(VectorKey key) => BitConverter.ToInt32(key.Value, 0);

  public static long ToLong(VectorKey key) => BitConverter.ToInt64(key.Value, 0);

  public static Guid ToGuid(VectorKey key) => new Guid(key.Value);

  public static string ToString(VectorKey key) => Encoding.UTF8.GetString(key.Value);

  public static VectorKey From(ulong value) => new VectorKey { Value = BitConverter.GetBytes(value) };
  
  public static VectorKey From(uint value) => new VectorKey { Value = BitConverter.GetBytes(value) };

  public static VectorKey From(int value) => new VectorKey { Value = BitConverter.GetBytes(value) };

  public static VectorKey From(long value) => new VectorKey { Value = BitConverter.GetBytes(value) };

  public static VectorKey From(Guid value) => new VectorKey { Value = value.ToByteArray() };

  public static VectorKey From(string value) => new VectorKey { Value = Encoding.UTF8.GetBytes(value) };

  public static implicit operator VectorKey(ulong value) =>
    new VectorKey { Value = BitConverter.GetBytes(value) };

  public static implicit operator VectorKey(uint value) =>
    new VectorKey { Value = BitConverter.GetBytes(value) };

  public static implicit operator VectorKey(int value) =>
    new VectorKey { Value = BitConverter.GetBytes(value) };

  public static implicit operator VectorKey(long value) =>
    new VectorKey { Value = BitConverter.GetBytes(value) };

  public static implicit operator VectorKey(Guid value) =>
    new VectorKey { Value = value.ToByteArray() };

  public static implicit operator VectorKey(string value) =>
    new VectorKey { Value = Encoding.UTF8.GetBytes(value) };

  public static implicit operator VectorKey(byte[] value) => new VectorKey { Value = value };

  public static implicit operator ReadOnlySpan<byte>(VectorKey item) =>
    new ReadOnlySpan<byte>(item.Value);
}
