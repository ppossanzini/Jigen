using Jigen.SemanticKernel.Abstractions;

namespace Jigen.SemanticKernel.Store;

/// <summary>
/// Semantic Kernel <c>VectorStoreCollection</c> over an in-process
/// <c>Jigen.Store</c> <see cref="global::Jigen.VectorCollection{T}"/>.
/// </summary>
public sealed class JigenStoreVectorStoreCollection<TKey, TRecord> : JigenVectorStoreCollection<TKey, TRecord>
  where TKey : notnull
  where TRecord : class, new()
{
  /// <summary>
  /// Wraps <paramref name="store"/>'s <paramref name="name"/> collection. Usually obtained via
  /// <see cref="JigenStoreVectorStore.GetCollection{TKey,TRecord}"/> rather than constructed directly.
  /// </summary>
  public JigenStoreVectorStoreCollection(global::Jigen.Store store, string name)
    : base(name, JigenStoreConstants.VectorStoreSystemName, new JigenStoreCollectionAdapter<TRecord>(store, name))
  {
  }
}
