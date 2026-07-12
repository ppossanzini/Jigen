using System.Text.Json;

namespace Jigen.Filtering;

public class PropertyEqualsFilter : IFilterExpression
{
  public string PropertyPath { get; set; }
  public object Value { get; set; }

  public bool Matches(JsonElement document)
  {
    var element = JsonFilterHelpers.GetElementByPath(document, PropertyPath);
    if (!element.HasValue) return false;

    return JsonFilterHelpers.CompareValues(element.Value, Value);
  }

  public string ToDebugString() => $"{PropertyPath} == {Value}";
}

public class PropertyCollectionAnyFilter : IFilterExpression
{
  public string PropertyPath { get; set; }
  public object Value { get; set; }

  public bool Matches(JsonElement document)
  {
    var element = JsonFilterHelpers.GetElementByPath(document, PropertyPath);
    if (!element.HasValue) return false;

    if (element.Value.ValueKind != JsonValueKind.Array)
      return false;

    foreach (var item in element.Value.EnumerateArray())
    {
      if (JsonFilterHelpers.CompareValues(item, Value))
        return true;
    }

    return false;
  }

  public string ToDebugString() => $"{PropertyPath}.Any(x => x == {Value})";
}

internal static class JsonFilterHelpers
{
  internal static bool CompareValues(JsonElement element, object value)
  {
    if (value == null)
      return element.ValueKind == JsonValueKind.Null;

    // Every accessor is gated on the ValueKind: GetString/GetBoolean/TryGetInt32
    // THROW on a mismatched kind, and an exception here would abort the whole
    // filter tree instead of reporting a non-matching leaf.
    return value switch
    {
      string s => element.ValueKind == JsonValueKind.String && element.GetString() == s,
      int i => element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var ei) && ei == i,
      long l => element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out var el) && el == l,
      double d => element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out var ed) && ed == d,
      bool b => (element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False) && element.GetBoolean() == b,
      _ => false
    };
  }

  internal static JsonElement? GetElementByPath(JsonElement document, string path)
  {
    var parts = path.Split('.');
    JsonElement current = document;

    foreach (var part in parts)
    {
      if (current.ValueKind != JsonValueKind.Object)
        return null;

      if (!current.TryGetProperty(part, out var next))
        return null;

      current = next;
    }

    return current;
  }
}

public class AndFilter : IFilterExpression
{
  public IFilterExpression Left { get; set; }
  public IFilterExpression Right { get; set; }

  public bool Matches(JsonElement document)
  {
    return Left.Matches(document) && Right.Matches(document);
  }

  public string ToDebugString() => $"({Left.ToDebugString()} && {Right.ToDebugString()})";
}

public class OrFilter : IFilterExpression
{
  public IFilterExpression Left { get; set; }
  public IFilterExpression Right { get; set; }

  public bool Matches(JsonElement document)
  {
    return Left.Matches(document) || Right.Matches(document);
  }

  public string ToDebugString() => $"({Left.ToDebugString()} || {Right.ToDebugString()})";
}
