using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace Jigen.Extensions;

public static class StreamExtensions
{
  public static void WriteInt32Le(this FileStream stream, int value)
  {
    Span<byte> buf = stackalloc byte[sizeof(int)];
    BinaryPrimitives.WriteInt32LittleEndian(buf, value);
    stream.Write(buf);
  }

  public static void WriteInt64Le(this FileStream stream, long value)
  {
    Span<byte> buf = stackalloc byte[sizeof(long)];
    BinaryPrimitives.WriteInt64LittleEndian(buf, value);
    stream.Write(buf);
  }

  public static void WriteByteArray(this FileStream stream, ReadOnlySpan<float> embeddings)
  {
    stream.Write(MemoryMarshal.AsBytes(embeddings));
  }
}