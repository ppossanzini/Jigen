using System.Text.Json;

namespace Jigen.Filtering;

/// <summary>
/// Represents a filter condition that can be applied to serialized documents.
/// This interface enables filters to be transferable via GRPC and applicable at the storage level.
/// </summary>
public interface IFilterExpression
{
  /// <summary>
  /// Evaluates the filter against a JSON document.
  /// </summary>
  bool Matches(JsonElement document);

  /// <summary>
  /// Converts the filter to a string representation for debugging/logging.
  /// </summary>
  string ToDebugString();
}
