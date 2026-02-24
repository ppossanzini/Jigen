using Jigen.DataStructures;

namespace Jigen;

public interface IIndexer
{
  void OpenOrCreateIndex(string collection);

  void AddToIndex(VectorEntry entry);
  void UpdateIndex(VectorEntry entry);
  void RemoveFromIndex(VectorEntry entry);

  List<(VectorEntry entry, float score)> Search(IStore store, string collection, float[] queryVector, int top);
}