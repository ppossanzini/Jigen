using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Jigen.Extensions;

public static class StreamExtensions
{
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static void WriteInt32Le(this FileStream stream, int value)
  {
    Span<byte> buf = stackalloc byte[sizeof(int)];
    BinaryPrimitives.WriteInt32LittleEndian(buf, value);
    stream.Write(buf);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static void WriteInt64Le(this FileStream stream, long value)
  {
    Span<byte> buf = stackalloc byte[sizeof(long)];
    BinaryPrimitives.WriteInt64LittleEndian(buf, value);
    stream.Write(buf);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static void WriteByteArray(this FileStream stream, ReadOnlySpan<float> embeddings)
  {
    stream.Write(MemoryMarshal.AsBytes(embeddings));
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static byte[] ToByteArray(this long[] items)
  {
    var result = new byte[items.Length * sizeof(long)];
    Buffer.BlockCopy(items, 0, result, 0, result.Length);
    return result;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static byte[] ToByteArray(this Memory<long> items)
  {
    return MemoryMarshal.AsBytes(items.Span).ToArray();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static Span<byte> ToByteSpan(this Memory<long> items)
  {
    return MemoryMarshal.AsBytes(items.Span);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static ReadOnlySpan<long> ToInt64Span(this ReadOnlySpan<byte> bytes)
  {
    return MemoryMarshal.Cast<byte, long>(bytes);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static int ReadLEInt32(this SafeFileHandle handle, long position)
  {
    Span<byte> buffer = stackalloc byte[sizeof(int)];
    RandomAccess.Read(handle, buffer, position);
    return BinaryPrimitives.ReadInt32LittleEndian(buffer);
  }
}