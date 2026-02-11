namespace Jigen;

public class DocumentCollectionOptions<T>
  where T : class, new()
{
  public string Name = nameof(T);
}