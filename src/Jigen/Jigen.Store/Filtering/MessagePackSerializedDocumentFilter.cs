using System.Runtime.CompilerServices;
using System.Text.Json;
using Jigen.Filtering;
using MessagePack;
using MessagePack.Resolvers;

namespace Jigen;

/// <summary>
/// MessagePack serializer with support for filtering at the serialized level.
/// Enables filters to be applied before deserialization.
/// </summary>
public class MessagePackSerializedDocumentFilter : ISerializedDocumentFilter
{
  private static readonly MessagePackDocumentSerializer _baseSerializer = MessagePackDocumentSerializer.Instance;
  public static MessagePackSerializedDocumentFilter Instance { get; } = new();

  readonly MessagePackSerializerOptions _serializerOptions = MessagePackSerializerOptions.Standard.WithResolver(ContractlessStandardResolver.Instance);

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public ReadOnlyMemory<byte> Serialize(object document) => _baseSerializer.Serialize(document);
  
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public object Deserialize(Type t, ReadOnlyMemory<byte> data) => _baseSerializer.Deserialize(t, data);
  
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public ReadOnlyMemory<byte> Serialize<T>(T document) => _baseSerializer.Serialize(document);
  
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public T Deserialize<T>(ReadOnlyMemory<byte> data) => _baseSerializer.Deserialize<T>(data);
  
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public string ToJson(ReadOnlyMemory<byte> data) => _baseSerializer.ToJson(data);
  
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public object ToJsonObject(ReadOnlyMemory<byte> data) => Newtonsoft.Json.JsonConvert.DeserializeObject(ToJson( data));

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public ReadOnlyMemory<byte> FromJson(string json) => _baseSerializer.FromJson(json);

  public bool MatchesFilter(ReadOnlyMemory<byte> serializedData, IFilterExpression filter)
  {
    return MessagePackFilterEvaluator.Matches(serializedData, filter);
  }

  public T DeserializeIfMatches<T>(ReadOnlyMemory<byte> serializedData, IFilterExpression filter) where T : class, new()
  {
    if (filter == null)
      return Deserialize<T>(serializedData);

    if (!MatchesFilter(serializedData, filter))
      return null;

    return Deserialize<T>(serializedData);
  }
}
