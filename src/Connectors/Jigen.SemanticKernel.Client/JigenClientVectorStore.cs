using System.Runtime.CompilerServices;
using Jigen.Client;
using Jigen.Proto;
using Jigen.SemanticKernel.Abstractions;
using Microsoft.Extensions.VectorData;

namespace Jigen.SemanticKernel.Client;

/// <summary>
/// Semantic Kernel <see cref="VectorStore"/> over a gRPC connection to a
/// remote Jigen server. Construct with an already-configured
/// <see cref="global::Jigen.Client.Context"/> (or a subclass — the
/// recommended pattern per <c>docs/client/getting-started.md</c>); this class
/// does not own the channel's lifetime.
/// </summary>
public sealed class JigenClientVectorStore : VectorStore
{
  private readonly Context _context;
  private readonly VectorStoreMetadata _metadata;

  public JigenClientVectorStore(Context context)
  {
    ArgumentNullException.ThrowIfNull(context);
    _context = context;
    _metadata = new VectorStoreMetadata
    {
      VectorStoreSystemName = JigenClientConstants.VectorStoreSystemName,
      VectorStoreName = context.Options.DatabaseName
    };
  }

  /// <inheritdoc />
  public override VectorStoreCollection<TKey, TRecord> GetCollection<TKey, TRecord>(string name, VectorStoreCollectionDefinition? definition = null)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(name);

    // See JigenStoreVectorStore.GetCollection for why `definition` is unused
    // and why this goes through reflection rather than `new(...)`.
    return JigenGenericCollectionFactory.Create<TKey, TRecord>(typeof(JigenClientVectorStoreCollection<,>), _context, name);
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
    var result = await _context.ServiceClient
      .ListCollectionsAsync(new CollectionKey { Database = DatabaseName() }, cancellationToken: cancellationToken)
      .ConfigureAwait(false);

    foreach (var name in result.Collections)
      yield return name;
  }

  /// <inheritdoc />
  public override async Task<bool> CollectionExistsAsync(string name, CancellationToken cancellationToken = default)
  {
    var result = await _context.ServiceClient
      .ListCollectionsAsync(new CollectionKey { Database = DatabaseName() }, cancellationToken: cancellationToken)
      .ConfigureAwait(false);

    return result.Collections.Contains(name);
  }

  /// <inheritdoc />
  public override async Task EnsureCollectionDeletedAsync(string name, CancellationToken cancellationToken = default)
  {
    // Non-generic: goes straight through the RPC (no need for a typed
    // VectorCollection<T>), same "clears every entry" caveat as
    // JigenClientCollectionAdapter.EnsureCollectionDeletedAsync.
    await _context.ServiceClient
      .ClearAsync(new CollectionKey { Database = DatabaseName(), Collection = name }, cancellationToken: cancellationToken)
      .ConfigureAwait(false);
  }

  /// <inheritdoc />
  public override object? GetService(Type serviceType, object? serviceKey = null)
  {
    ArgumentNullException.ThrowIfNull(serviceType);

    return
      serviceKey is not null ? null :
      serviceType == typeof(VectorStoreMetadata) ? _metadata :
      serviceType == typeof(Context) ? _context :
      serviceType.IsInstanceOfType(this) ? this :
      null;
  }

  private string DatabaseName()
  {
    var name = _context.Options.DatabaseName;
    ArgumentException.ThrowIfNullOrWhiteSpace(name);
    return name;
  }
}
