namespace Jigen;

public interface IDocumentSerializer
{
  ReadOnlyMemory<byte> Serialize(object document);
  object Deserialize(Type t, ReadOnlyMemory<byte> data);


  ReadOnlyMemory<byte> Serialize<T>(T document);
  T Deserialize<T>(ReadOnlyMemory<byte> data);

  string ToJson(ReadOnlyMemory<byte> data);
  object ToJsonObject(ReadOnlyMemory<byte> data);

  /// <summary>
  /// Serializes a JSON document to the storage format. Counterpart of
  /// <see cref="ToJson"/>, for payloads that only exist as JSON (e.g. REST
  /// request bodies) and have no CLR contract to serialize from.
  /// </summary>
  ReadOnlyMemory<byte> FromJson(string json);
}