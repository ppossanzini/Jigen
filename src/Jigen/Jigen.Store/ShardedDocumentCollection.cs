namespace Jigen;

public class ShardedDocumentCollection<T>(Store store, DocumentCollectionOptions<T> options = null) where T : class, new()
{
  public DocumentCollection<T> GetShard(Func<string> shardName)
  {
    return new DocumentCollection<T>(store, new DocumentCollectionOptions<T>()
    {
      Name = $"{options?.Name}_{shardName() ?? "default"}",
      DocumentSerializer = options?.DocumentSerializer
    });
  }
}