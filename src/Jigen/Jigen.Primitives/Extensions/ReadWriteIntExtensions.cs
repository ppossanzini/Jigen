using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace Jigen.Extensions;

public static class ReadWriteIntExtensions
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
  public static void WriteLEInt(this ArrayBufferWriter<byte> writer, int value)
  {
    
    Span<byte> buffer = stackalloc byte[sizeof(int)];
    BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
    buffer.CopyTo(writer.GetSpan(buffer.Length));
    writer.Advance(buffer.Length);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static byte ReadByte(this ReadOnlySpan<byte> source, ref int offset)
  {
    if (offset >= source.Length)
      throw new InvalidDataException("Unexpected end of data while deserializing IndexNode.");

    return source[offset++];
  }


  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static int ReadLEInt32(this ReadOnlySpan<byte> handle, ref int offset)
  {
    var buffer = handle.Slice(offset, sizeof(int));
    offset += sizeof(int);
    return BinaryPrimitives.ReadInt32LittleEndian(buffer);
  }
}