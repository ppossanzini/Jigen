using System.Buffers;
using System.Runtime.CompilerServices;
using MessagePack;
using MessagePack.Resolvers;

namespace Jigen;

public class MessagePackDocumentSerializer : IDocumentSerializer
{
  public static MessagePackDocumentSerializer Instance { get; } = new();

  readonly MessagePackSerializerOptions _serializerOptions = MessagePackSerializerOptions.Standard.WithResolver(ContractlessStandardResolver.Instance);

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public ReadOnlyMemory<byte> Serialize(object document) => MessagePackSerializer.Serialize(document, _serializerOptions);

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public object Deserialize(Type t, ReadOnlyMemory<byte> data)  => MessagePackSerializer.Deserialize(t, data, _serializerOptions);

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public ReadOnlyMemory<byte> Serialize<T>(T document) => MessagePackSerializer.Serialize(document, _serializerOptions);

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public T Deserialize<T>(ReadOnlyMemory<byte> data) => MessagePackSerializer.Deserialize<T>(data, _serializerOptions);

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public string ToJson(ReadOnlyMemory<byte> data) => MessagePackSerializer.ConvertToJson(data, _serializerOptions);

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public object ToJsonObject(ReadOnlyMemory<byte> data) => Newtonsoft.Json.JsonConvert.DeserializeObject(ToJson(data));

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public ReadOnlyMemory<byte> FromJson(string json) => MessagePackSerializer.ConvertFromJson(json, _serializerOptions);
}