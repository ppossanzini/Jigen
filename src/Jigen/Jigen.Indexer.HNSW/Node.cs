using System.Runtime.InteropServices;

namespace Jigen.Indexer;

/// <summary>
/// A node into the small World
/// </summary>
public struct Node
{
  // Position of node in file
  public long Position;
  public int MaxLayers;

  // Id is in byte[] to allow direct connections with store ids.
  public ReadOnlyMemory<byte> Id;

  // Connections is long because it represents node connections in the graph.
  // is not an index but the file current position of connected nodes. 
  // Connections are bidirectional, meaning that if node A is connected to node B, node B is also connected to node A.
  // This decreases the number of unidirectional connections, as each connection is stored twice, but 
  // allows node removal conveniently 
  public Memory<long> Connections;

  public int Size => sizeof(long) + sizeof(int) + Connections.Length * sizeof(long) + Id.Length * sizeof(byte);

  public Node(ReadOnlyMemory<byte> id, Memory<long> connections, int maxlayers)
  {
    Id = id;
    Connections = connections;
    MaxLayers = maxlayers;
  }

  public Node(ReadOnlyMemory<byte> id, int maxconnections, int maxlayers) :
    this(id, new Memory<long>(new long[maxconnections]), maxlayers)
  {
  }
}