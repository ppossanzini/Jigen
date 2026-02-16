namespace Jigen;

public interface IDocumentSerializer
{
  ReadOnlyMemory<byte> Serialize(object document);
  object Deserialize(Type t, ReadOnlyMemory<byte> data);

  ReadOnlyMemory<byte> Serialize<T>(T document);
  T Deserialize<T>(ReadOnlyMemory<byte> data);

  string ToJson(ReadOnlyMemory<byte> data);
}