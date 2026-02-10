using Microsoft.Extensions.VectorData;

namespace Jigen.SemanticKernel.Connector;

public sealed class JigenVectorStore(JigenVectorStoreOptions options) : VectorStore
{
  
  
  public override VectorStoreCollection<TKey, TRecord> GetCollection<TKey, TRecord>(string name, VectorStoreCollectionDefinition definition = null)
  {
    throw new NotImplementedException();
  }

  public override VectorStoreCollection<object, Dictionary<string, object>> GetDynamicCollection(string name, VectorStoreCollectionDefinition definition)
  {
    throw new NotImplementedException();
  }

  public override IAsyncEnumerable<string> ListCollectionNamesAsync(CancellationToken cancellationToken = new CancellationToken())
  {
    throw new NotImplementedException();
  }

  public override Task<bool> CollectionExistsAsync(string name, CancellationToken cancellationToken = new CancellationToken())
  {
    throw new NotImplementedException();
  }

  public override Task EnsureCollectionDeletedAsync(string name, CancellationToken cancellationToken = new CancellationToken())
  {
    throw new NotImplementedException();
  }

  public override object GetService(Type serviceType, object serviceKey = null)
  {
    throw new NotImplementedException();
  }
}