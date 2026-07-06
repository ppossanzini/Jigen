using System.Numerics;
using System.Text;

namespace Jigen.DataStructures;

public class VectorEntry
{
  public byte[] Id { get; set; } = Guid.NewGuid().ToByteArray();
  public string CollectionName { get; set; }
  public ReadOnlyMemory<byte> Content { get; set; }
  public ReadOnlyMemory<float> Embedding { get; set; }

  public static VectorEntry Empty => new VectorEntry(){
    Id = Guid.NewGuid().ToByteArray(),
    CollectionName = string.Empty,
    Content = ReadOnlyMemory<byte>.Empty,
    Embedding = ReadOnlyMemory<float>.Empty
  };  
  
}

public class VectorEntry<T>
  where T : class, new()
{
  public VectorKey Key;
  public T Content { get; set; }
  public float[] Embedding { get; set; }
}

public record struct VectorKey
{
  public byte[] Value { get; init; }

  // The compiler-generated equality would compare Value by reference
  // (EqualityComparer<byte[]>.Default): two keys built from the same source
  // would never be equal. Compare and hash the bytes instead.
  public readonly bool Equals(VectorKey other)
  {
    if (Value is null) return other.Value is null;
    return other.Value is not null && Value.AsSpan().SequenceEqual(other.Value);
  }

  public readonly override int GetHashCode() =>
    Value is null ? 0 : unchecked((int)System.IO.Hashing.XxHash32.HashToUInt32(Value));

  public static VectorKey From(ulong value)
  {
    return new VectorKey { Value = BitConverter.GetBytes(value) };
  }

  public static VectorKey From(uint value)
  {
    return new VectorKey { Value = BitConverter.GetBytes(value) };
  }

  public static VectorKey From(int value)
  {
    return new VectorKey { Value = BitConverter.GetBytes(value) };
  }

  public static VectorKey From(long value)
  {
    return new VectorKey { Value = BitConverter.GetBytes(value) };
  }

  public static VectorKey From(Guid value)
  {
    return new VectorKey { Value = value.ToByteArray() };
  }

  public static VectorKey From(string value)
  {
    return new VectorKey { Value = Encoding.UTF8.GetBytes(value) };
  }


  public static implicit operator VectorKey(ulong value)
  {
    return new VectorKey { Value = BitConverter.GetBytes(value) };
  }

  public static implicit operator VectorKey(uint value)
  {
    return new VectorKey { Value = BitConverter.GetBytes(value) };
  }

  public static implicit operator VectorKey(int value)
  {
    return new VectorKey { Value = BitConverter.GetBytes(value) };
  }

  public static implicit operator VectorKey(long value)
  {
    return new VectorKey { Value = BitConverter.GetBytes(value) };
  }

  public static implicit operator VectorKey(Guid value)
  {
    return new VectorKey { Value = value.ToByteArray() };
  }

  public static implicit operator VectorKey(string value)
  {
    return new VectorKey { Value = Encoding.UTF8.GetBytes(value) };
  }

  public static implicit operator VectorKey(byte[] value)
  {
    return new VectorKey { Value = value };
  }

  public static implicit operator ReadOnlySpan<byte>(VectorKey item)
  {
    return new ReadOnlySpan<byte>(item.Value);
  }
}