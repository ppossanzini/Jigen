namespace Jigen;


public interface IDocumentSerializer<T> 
{
  byte[] Serialize(T document);
  T Deserialize(byte[] data);
}