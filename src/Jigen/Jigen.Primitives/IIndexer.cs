using Jigen.DataStructures;

namespace Jigen;

public interface IIndexer
{
  void AddToIndex(VectorEntry entry);
  void RemoveFromIndex(string collection, byte[] key);

  List<(VectorEntry entry, float score)> Search(IStore store, string collection, float[] queryVector, int top);

  Task FlushAsync() => Task.CompletedTask;
}