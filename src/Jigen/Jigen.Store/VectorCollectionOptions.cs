namespace Jigen;

public class VectorCollectionOptions<T>
  where T : class, new()
{
  public int Dimensions = 1536;
  public string Name = typeof(T).Namespace + "." + typeof(T).Name;

  public IDocumentSerializer DocumentSerializer { get; set; } = MessagePackDocumentSerializer.Instance;
}