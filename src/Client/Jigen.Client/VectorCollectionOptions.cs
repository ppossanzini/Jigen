namespace Jigen.Client;

public class VectorCollectionOptions<T>
  where T : class, new()
{
  public int Dimensions = 1536;
  public string Name = nameof(T);

  public IDocumentSerializer DocumentSerializer = MessagePackDocumentSerializer.Instance;
}