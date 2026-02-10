namespace Jigen;

public class VectorCollectionOptions<T>
  where T : class, new()
{
  public int Dimensions = 1536;
  public string Name = nameof(T);

  public Func<T, byte[]> Serialize;
  public Func<byte[], T> Deserialize;
}