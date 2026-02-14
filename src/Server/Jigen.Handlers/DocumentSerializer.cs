using MessagePack;
using MessagePack.Resolvers;

namespace Jigen.Handlers;

public class DocumentSerializer<T>: IDocumentSerializer<T>
where T : class, new()
{
  readonly MessagePackSerializerOptions _serializerOptions = MessagePackSerializerOptions.Standard.WithResolver(ContractlessStandardResolver.Instance);
  public byte[] Serialize(T document)
  {
    return MessagePackSerializer.Serialize(document, _serializerOptions);
  }

  public byte[] Serialize(object document)
  {
    return Serialize((T)document);
  }

  object IDocumentSerializer.Deserialize(byte[] data)
  {
    return Deserialize(data);
  }

  public T Deserialize(byte[] data)
  {
    return MessagePackSerializer.Deserialize<T>(data, _serializerOptions);
  }
}