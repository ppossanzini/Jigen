namespace Jigen;

public interface IDocumentSerializer
{
  byte[] Serialize(object document);
  object Deserialize(byte[] data);
}

public interface IDocumentSerializer<T> : IDocumentSerializer
{
  byte[] Serialize(T document);
  new T Deserialize(byte[] data);
}