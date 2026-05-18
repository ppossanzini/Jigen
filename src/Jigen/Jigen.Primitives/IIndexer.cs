using Jigen.DataStructures;
using Jigen.Filtering;

namespace Jigen;

public interface IIndexer
{
  void AddToIndex(VectorEntry entry);
  void RemoveFromIndex(string collection, byte[] key);

  IEnumerable<(VectorEntry entry, float score)> Search(IStore store, string collection, float[] queryVector, int top, IFilterExpression contentFilter = null);
  IEnumerable<VectorEntry> Search(IStore store, string collection, IFilterExpression contentFilter = null);

  Task FlushAsync() => Task.CompletedTask;
}