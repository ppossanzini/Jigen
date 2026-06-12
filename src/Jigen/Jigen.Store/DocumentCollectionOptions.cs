namespace Jigen;

public class DocumentCollectionOptions<T>
  where T : class, new()
{
  public string Name = typeof(T).Namespace + "." + typeof(T).Name;

  public IDocumentSerializer DocumentSerializer { get; set; } = MessagePackSerializedDocumentFilter.Instance;
}