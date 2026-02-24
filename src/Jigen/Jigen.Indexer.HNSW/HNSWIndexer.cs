using HNSW.Net;
using Jigen.DataStructures;

namespace Jigen.Indexer;

public class HNSWIndexer: IIndexer
{
  public void OpenOrCreateIndex(string collection)
  {
    new SmallWorld<float[], float>().
  }

  public void AddToIndex(VectorEntry entry)
  {
    throw new NotImplementedException();
  }

  public void UpdateIndex(VectorEntry entry)
  {
    throw new NotImplementedException();
  }

  public void RemoveFromIndex(VectorEntry entry)
  {
    throw new NotImplementedException();
  }

  public List<(VectorEntry entry, float score)> Search(IStore store, string collection, float[] queryVector, int top)
  {
    throw new NotImplementedException();
  }
}