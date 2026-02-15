using System.Text;

namespace Jigen.Client.BaseTypes;

public struct VectorKey
{
  public byte[] Value;

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

  public static implicit operator Span<byte>(VectorKey item)
  {
    return (Span<byte>)item.Value;
  }
}