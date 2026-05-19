using System.Text.Json;
using Jigen.Filtering;

namespace Jigen;

/// <summary>
/// Extended document serializer that supports filtering at the serialized level.
/// This enables applying filters before deserialization for better performance and decoupling.
/// </summary>
public interface ISerializedDocumentFilter : IDocumentSerializer
{
  /// <summary>
  /// Checks if a serialized document matches the given filter without deserializing.
  /// </summary>
  bool MatchesFilter(ReadOnlyMemory<byte> serializedData, IFilterExpression filter);

  /// <summary>
  /// Deserializes the document only if it matches the filter.
  /// Returns null if the filter doesn't match.
  /// </summary>
  T DeserializeIfMatches<T>(ReadOnlyMemory<byte> serializedData, IFilterExpression filter) where T : class, new();
}
