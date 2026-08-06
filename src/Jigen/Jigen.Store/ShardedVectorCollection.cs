namespace Jigen;

public class ShardedVectorCollection<T>(Store store, VectorCollectionOptions<T> options = null) where T : class, new()
{
  public VectorCollection<T> GetShard(Func<string> shardName)
  {
    return new VectorCollection<T>(store, new VectorCollectionOptions<T>()
    {
      Name = $"{options?.Name}_{shardName() ?? "default"}",
      DocumentSerializer = options?.DocumentSerializer,
      Dimensions = options?.Dimensions ?? 1536,
      SentenceEmbedder = options?.SentenceEmbedder, 
    });
  }
}