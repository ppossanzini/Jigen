using System.Runtime.CompilerServices;
using Jigen.Extensions;

namespace Jigen.Indexer.Extensions;

public static class WritingExtensions
{
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  internal static void WriteNode<TItem, TDistance>(this Graph<TItem, TDistance> graph, Node node, long position) where TDistance : struct, IComparable<TDistance>
  {
    var handle = graph.GraphFileStream.SafeFileHandle!;
    var p = position;

    var nodesize = sizeof(byte) + node.Id.Length + sizeof(byte) + node.Connections.Length;
    RandomAccess.Write(handle, BitConverter.GetBytes(nodesize), position);
    p += sizeof(int);
    RandomAccess.Write(handle, [(byte)node.Id.Length], p);
    p++;
    RandomAccess.Write(handle, node.Id.Span, p);
    p += node.Id.Length;
    RandomAccess.Write(handle, [node.MaxLayers], p);
    p++;
    RandomAccess.Write(handle, node.Connections.ToByteSpan(), p);
  }
}