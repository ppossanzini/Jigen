using System.Runtime.CompilerServices;
using Jigen.Extensions;
using Jigen.SemanticKernel.Abstractions;
using Microsoft.Extensions.VectorData;

namespace Jigen.SemanticKernel.Store;

/// <summary>
/// Semantic Kernel <see cref="VectorStore"/> over an in-process <c>Jigen.Store</c>
/// database. Construct with an already-open <see cref="global::Jigen.Store"/> —
/// this class does not own its lifetime (does not close it on dispose).
/// </summary>
public sealed class JigenStoreVectorStore : VectorStore
{
  private readonly global::Jigen.Store _store;
  private readonly VectorStoreMetadata _metadata;

  public JigenStoreVectorStore(global::Jigen.Store store)
  {
    ArgumentNullException.ThrowIfNull(store);
    _store = store;
    _metadata = new VectorStoreMetadata { VectorStoreSystemName = JigenStoreConstants.VectorStoreSystemName };
  }

  /// <inheritdoc />
  public override VectorStoreCollection<TKey, TRecord> GetCollection<TKey, TRecord>(string name, VectorStoreCollectionDefinition? definition = null)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(name);

    // `definition` (explicit property overrides / a custom embedding
    // generator) is intentionally not consumed: the reflective mapper in
    // Jigen.SemanticKernel.Abstractions reads [VectorStoreKey]/[VectorStoreVector]
    // straight off TRecord, and embedding generation is out of scope for v1
    // (see JigenVectorStoreCollection.ToFloatArray).
    //
    // Built via reflection, not `new(...)` directly: this override's TRecord
    // constraint is fixed to `class` by the base VectorStore class, but
    // JigenStoreVectorStoreCollection requires `class, new()` (Jigen.VectorCollection<T>
    // does). See JigenGenericCollectionFactory.
    return JigenGenericCollectionFactory.Create<TKey, TRecord>(typeof(JigenStoreVectorStoreCollection<,>), _store, name);
  }

  /// <inheritdoc />
  public override VectorStoreCollection<object, Dictionary<string, object?>> GetDynamicCollection(string name, VectorStoreCollectionDefinition definition)
  {
    throw new NotSupportedException(
      "Dynamic (schema-less Dictionary<string,object?>) collections are not supported by the Jigen " +
      "Semantic Kernel connector in v1. Use a strongly-typed record annotated with " +
      "[VectorStoreKey]/[VectorStoreData]/[VectorStoreVector] instead.");
  }

  /// <inheritdoc />
  public override async IAsyncEnumerable<string> ListCollectionNamesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
  {
    foreach (var name in _store.GetCollections())
    {
      cancellationToken.ThrowIfCancellationRequested();
      yield return name;
    }
  }

  /// <inheritdoc />
  public override Task<bool> CollectionExistsAsync(string name, CancellationToken cancellationToken = default)
    => Task.FromResult(_store.GetCollections().Contains(name));

  /// <inheritdoc />
  public override Task EnsureCollectionDeletedAsync(string name, CancellationToken cancellationToken = default)
    // Non-generic: Store.ClearContent doesn't need TRecord and, like
    // JigenStoreCollectionAdapter.EnsureCollectionDeletedAsync, also drops the
    // collection's key so it stops appearing in GetCollections().
    => _store.ClearContent(name);

  /// <inheritdoc />
  public override object? GetService(Type serviceType, object? serviceKey = null)
  {
    ArgumentNullException.ThrowIfNull(serviceType);

    return
      serviceKey is not null ? null :
      serviceType == typeof(VectorStoreMetadata) ? _metadata :
      serviceType == typeof(global::Jigen.Store) ? _store :
      serviceType.IsInstanceOfType(this) ? this :
      null;
  }
}
