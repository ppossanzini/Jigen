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

  public ReadOnlyMemory<byte> Serialize(object document) => _baseSerializer.Serialize(document);
  public object Deserialize(Type t, ReadOnlyMemory<byte> data) => _baseSerializer.Deserialize(t, data);
  public ReadOnlyMemory<byte> Serialize<T>(T document) => _baseSerializer.Serialize(document);
  public T Deserialize<T>(ReadOnlyMemory<byte> data) => _baseSerializer.Deserialize<T>(data);
  public string ToJson(ReadOnlyMemory<byte> data) => _baseSerializer.ToJson(data);

  public bool MatchesFilter(ReadOnlyMemory<byte> serializedData, IFilterExpression filter)
  {
    if (filter == null) return true;

    try
    {
      var json = MessagePackSerializer.ConvertToJson(serializedData, _serializerOptions);
      using var doc = JsonDocument.Parse(json);
      return filter.Matches(doc.RootElement);
    }
    catch
    {
      return false;
    }
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
