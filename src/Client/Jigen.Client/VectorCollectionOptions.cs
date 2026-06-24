namespace Jigen.Client;

public class VectorCollectionOptions<T>
  where T : class, new()
{
  public string Name = typeof(T).Namespace + "." + typeof(T).Name;

  public IDocumentSerializer DocumentSerializer = MessagePackDocumentSerializer.Instance;
}