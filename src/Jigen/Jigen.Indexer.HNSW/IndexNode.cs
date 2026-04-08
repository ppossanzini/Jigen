using Jigen.DataStructures;
using Jigen.Persistance;

namespace Jigen.Indexer;

public class IndexNode : IStorableItem<IndexNode>
{
  public int PositionId { get; set; }
  public VectorKey Id { get; set; }
  public int MaxLevel { get; set; }
  
  

  public List<IList<int>> Connections { get; init; } = new();
  public TravelingCosts TravelingCosts { get; private set; }

  public IndexNode(SmallWorldOptions options)
  {
    TravelingCosts = new TravelingCosts(this, options);
  }

  public ReadOnlyMemory<byte> Serialize()
  {
    return null; //TODO: Need to be implemented
  }

  public static IndexNode Deserialize(ReadOnlyMemory<byte> data)
  {
    return null; //TODO: Need to be implemented
  }
}