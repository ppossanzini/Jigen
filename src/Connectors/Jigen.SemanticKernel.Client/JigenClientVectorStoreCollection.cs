using Jigen.SemanticKernel.Abstractions;

namespace Jigen.SemanticKernel.Client;

/// <summary>
/// Semantic Kernel <c>VectorStoreCollection</c> over a gRPC
/// <see cref="global::Jigen.Client.VectorCollection{T}"/> (talks to a remote Jigen server).
/// </summary>
public sealed class JigenClientVectorStoreCollection<TKey, TRecord> : JigenVectorStoreCollection<TKey, TRecord>
  where TKey : notnull
  where TRecord : class, new()
{
  /// <summary>
  /// Wraps <paramref name="context"/>'s <paramref name="name"/> collection. Usually obtained via
  /// <see cref="JigenClientVectorStore.GetCollection{TKey,TRecord}"/> rather than constructed directly.
  /// </summary>
  public JigenClientVectorStoreCollection(global::Jigen.Client.Context context, string name)
    : base(name, JigenClientConstants.VectorStoreSystemName, new JigenClientCollectionAdapter<TRecord>(context, name))
  {
  }
}
