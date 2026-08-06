namespace Jigen.Client;

public class ShardedVectorCollection<T>(Context store, VectorCollectionOptions<T> options = null) where T : class, new()
{
  public VectorCollection<T> GetShard(Func<string> shardName)
  {
    return new VectorCollection<T>(store, new VectorCollectionOptions<T>()
    {
      Name = $"{options?.Name}_{shardName() ?? "default"}",
      DocumentSerializer = options?.DocumentSerializer
    });
  }
}