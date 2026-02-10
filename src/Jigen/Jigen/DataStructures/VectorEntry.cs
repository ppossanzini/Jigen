using System.Numerics;
using System.Text;

namespace Jigen.DataStructures;

public class VectorEntry
{
  public byte[] Id { get; set; } = Guid.NewGuid().ToByteArray();
  public string CollectionName { get; set; }
  public byte[] Content { get; set; }
  public float[] Embedding { get; set; }
}

public class VectorEntry<T>
  where T : class, new()
{
  public VectorKey Key;
  public T Content { get; set; }
  public float[] Embedding { get; set; }
}

public struct VectorKey
{
  public byte[] Key;

  public static implicit operator VectorKey(ulong value)
  {
    return new VectorKey { Key = BitConverter.GetBytes(value) };
  }

  public static implicit operator VectorKey(uint value)
  {
    return new VectorKey { Key = BitConverter.GetBytes(value) };
  }

  public static implicit operator VectorKey(int value)
  {
    return new VectorKey { Key = BitConverter.GetBytes(value) };
  }

  public static implicit operator VectorKey(long value)
  {
    return new VectorKey { Key = BitConverter.GetBytes(value) };
  }

  public static implicit operator VectorKey(Guid value)
  {
    return new VectorKey { Key = value.ToByteArray() };
  }

  public static implicit operator VectorKey(string value)
  {
    return new VectorKey { Key = Encoding.UTF8.GetBytes(value) };
  }

  public static implicit operator VectorKey(byte[] value)
  {
    return new VectorKey { Key = value };
  }

  public static implicit operator Span<byte>(VectorKey item)
  {
    return (Span<byte>)item.Key;
  }
}