using System.Text;

namespace Jigen.Client.BaseTypes;

public struct VectorKey
{
  public byte[] Value;

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

  public static implicit operator VectorKey(ReadOnlySpan<byte> value) =>
    new VectorKey { Value = value.ToArray() };

  public static implicit operator ReadOnlySpan<byte>(VectorKey item) =>
    new ReadOnlySpan<byte>(item.Value);
}
