using System.Buffers;
using System.Runtime.CompilerServices;

namespace Jigen.Extensions;


public static class VarIntExtensions
{
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static void WriteByte(this ArrayBufferWriter<byte> writer, byte value)
  {
    var span = writer.GetSpan(1);
    span[0] = value;
    writer.Advance(1);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static void WriteBytes(this ArrayBufferWriter<byte> writer, ReadOnlySpan<byte> source)
  {
    if (source.Length == 0) return;

    source.CopyTo(writer.GetSpan(source.Length));
    writer.Advance(source.Length);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static void WriteVarUInt(this ArrayBufferWriter<byte> writer, uint value)
  {
    while (value >= 0x80)
    {
      writer.WriteByte((byte)((value & 0x7F) | 0x80));
      value >>= 7;
    }

    writer.WriteByte((byte)value);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static byte ReadByte(this ReadOnlySpan<byte> source, ref int offset)
  {
    if (offset >= source.Length)
      throw new InvalidDataException("Unexpected end of data while deserializing IndexNode.");

    return source[offset++];
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static uint ReadVarUInt(this ReadOnlySpan<byte> source, ref int offset)
  {
    uint result = 0;
    int shift = 0;

    while (true)
    {
      var next = source.ReadByte(ref offset);
      result |= (uint)(next & 0x7F) << shift;

      if ((next & 0x80) == 0)
        return result;

      shift += 7;
      if (shift >= 35)
        throw new InvalidDataException("Invalid varint value in IndexNode payload.");
    }
  }
}