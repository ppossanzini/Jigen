using System.Text.Json;

namespace Jigen.Filtering;

public class PropertyEqualsFilter : IFilterExpression
{
  public string PropertyPath { get; set; }
  public object Value { get; set; }

  public bool Matches(JsonElement document)
  {
    var element = GetElementByPath(document, PropertyPath);
    if (!element.HasValue) return false;

    return CompareValues(element.Value, Value);
  }

  public string ToDebugString() => $"{PropertyPath} == {Value}";

  private static bool CompareValues(JsonElement element, object value)
  {
    if (value == null)
      return element.ValueKind == JsonValueKind.Null;

    return value switch
    {
      string s => element.GetString() == s,
      int i => element.TryGetInt32(out var ei) && ei == i,
      long l => element.TryGetInt64(out var el) && el == l,
      double d => element.TryGetDouble(out var ed) && ed == d,
      bool b => element.GetBoolean() == b,
      _ => false
    };
  }

  private static JsonElement? GetElementByPath(JsonElement document, string path)
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

public class PropertyCollectionAnyFilter : IFilterExpression
{
  public string PropertyPath { get; set; }
  public object Value { get; set; }

  public bool Matches(JsonElement document)
  {
    var element = GetElementByPath(document, PropertyPath);
    if (!element.HasValue) return false;

    if (element.Value.ValueKind != JsonValueKind.Array)
      return false;

    foreach (var item in element.Value.EnumerateArray())
    {
      if (CompareValues(item, Value))
        return true;
    }

    return false;
  }

  public string ToDebugString() => $"{PropertyPath}.Any(x => x == {Value})";

  private static bool CompareValues(JsonElement element, object value)
  {
    if (value == null)
      return element.ValueKind == JsonValueKind.Null;

    return value switch
    {
      string s => element.GetString() == s,
      int i => element.TryGetInt32(out var ei) && ei == i,
      long l => element.TryGetInt64(out var el) && el == l,
      double d => element.TryGetDouble(out var ed) && ed == d,
      bool b => element.GetBoolean() == b,
      _ => false
    };
  }

  private static JsonElement? GetElementByPath(JsonElement document, string path)
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
