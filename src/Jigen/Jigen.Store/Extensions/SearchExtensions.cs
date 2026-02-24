using System.Collections.Concurrent;
using System.IO.MemoryMappedFiles;
using System.Numerics;
using System.Text;
using Jigen.DataStructures;

// ReSharper disable SuggestVarOrType_Elsewhere
// ReSharper disable SuggestVarOrType_BuiltInTypes

namespace Jigen.Extensions;

public static class SearchExtensions
{
  public static List<(VectorEntry entry, float score)> Search(this Store store, string collection, float[] queryVector, int top)
  {
    return store.Options.Indexer.Search(store, collection, queryVector, top);
  }
}