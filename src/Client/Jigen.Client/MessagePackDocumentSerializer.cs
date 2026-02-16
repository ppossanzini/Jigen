using MessagePack;
using MessagePack.Resolvers;

namespace Jigen.Client;

public class MessagePackDocumentSerializer : IDocumentSerializer
{
  public static MessagePackDocumentSerializer Instance { get; } = new();

  readonly MessagePackSerializerOptions _serializerOptions = MessagePackSerializerOptions.Standard.WithResolver(ContractlessStandardResolver.Instance);

  public ReadOnlyMemory<byte> Serialize(object document)
  {
    return MessagePackSerializer.Serialize(document, _serializerOptions);
  }

  public object Deserialize(Type t, ReadOnlyMemory<byte> data)
  {
    return MessagePackSerializer.Deserialize(t, data, _serializerOptions);
  }

  public ReadOnlyMemory<byte> Serialize<T>(T document)
  {
    return MessagePackSerializer.Serialize(document, _serializerOptions);
  }

  public T Deserialize<T>(ReadOnlyMemory<byte> data)
  {
    return MessagePackSerializer.Deserialize<T>(data, _serializerOptions);
  }
}