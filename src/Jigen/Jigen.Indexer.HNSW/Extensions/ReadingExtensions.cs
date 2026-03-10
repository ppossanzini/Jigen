using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using Jigen.Extensions;

namespace Jigen.Indexer.Extensions;

public static class ReadingExtensions
{
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static Node ReadNode(this FileStream filestream, long position)
  {
    var result = new Node() { Position = position };
    var handle = filestream.SafeFileHandle!;

    var size = handle.ReadLEInt32(position);
    var p = position + sizeof(int);

    Span<byte> buffer = stackalloc byte[size];
    RandomAccess.Read(handle, buffer, p);

    result.Id = buffer.Slice(1, (int)buffer[0]).ToArray();
    result.MaxLayers = buffer[1 + (int)buffer[0]];
    result.Connections = buffer.Slice(2 + (int)buffer[0], size - 2 + (int)buffer[0]).ToInt64Span().ToArray();

    return result;
  }


}