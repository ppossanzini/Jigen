using System.Text;
using System.Text.Json;
using MessagePack;

namespace Jigen.Filtering;

/// <summary>
/// Evaluates the built-in filter AST directly on a MessagePack-serialized
/// document, skimming the buffer with a MessagePackReader instead of
/// converting the whole document to a JSON string and parsing it back
/// (the per-candidate cost of the JSON route dominates filtered scans).
/// Semantics mirror the JSON-based <see cref="IFilterExpression.Matches"/>:
/// case-sensitive dotted paths over maps, ordinal string equality, typed
/// numeric comparisons, And/Or short-circuit. Filter implementations other
/// than the built-in four fall back to the JSON evaluation.
/// </summary>
public static class MessagePackFilterEvaluator
{
  public static bool Matches(ReadOnlyMemory<byte> document, IFilterExpression filter)
  {
    if (filter is null) return true;

    switch (filter)
    {
      case AndFilter and:
        return Matches(document, and.Left) && Matches(document, and.Right);
      case OrFilter or:
        return Matches(document, or.Left) || Matches(document, or.Right);
      case PropertyEqualsFilter equals_:
        return MatchesLeaf(document, equals_.PropertyPath, equals_.Value, collectionAny: false);
      case PropertyCollectionAnyFilter any:
        return MatchesLeaf(document, any.PropertyPath, any.Value, collectionAny: true);
      default:
        return MatchesViaJson(document, filter);
    }
  }

  private static bool MatchesLeaf(ReadOnlyMemory<byte> document, string propertyPath, object value, bool collectionAny)
  {
    if (string.IsNullOrEmpty(propertyPath)) return false;

    try
    {
      var reader = new MessagePackReader(document);

      foreach (var part in propertyPath.Split('.'))
      {
        if (!TryDescend(ref reader, part))
          return false;
      }

      if (!collectionAny)
        return CompareAndConsume(ref reader, value);

      if (reader.NextMessagePackType != MessagePackType.Array)
        return false;

      var count = reader.ReadArrayHeader();
      for (var i = 0; i < count; i++)
      {
        if (CompareAndConsume(ref reader, value))
          return true;
      }

      return false;
    }
    catch
    {
      // Torn or malformed record: same contract as the JSON route, which
      // reports a failed conversion as "no match".
      return false;
    }
  }

  /// <summary>
  /// Positions the reader on the value of <paramref name="property"/> inside
  /// the map the reader currently points at. Sibling entries after the match
  /// are left unread — nothing past the matched value is ever needed.
  /// </summary>
  private static bool TryDescend(ref MessagePackReader reader, string property)
  {
    if (reader.NextMessagePackType != MessagePackType.Map)
      return false;

    Span<byte> utf8Property = stackalloc byte[property.Length <= 64 ? property.Length * 3 : 0];
    scoped ReadOnlySpan<byte> propertyBytes;
    if (utf8Property.Length > 0)
    {
      var written = Encoding.UTF8.GetBytes(property, utf8Property);
      propertyBytes = utf8Property[..written];
    }
    else
    {
      propertyBytes = Encoding.UTF8.GetBytes(property);
    }

    var count = reader.ReadMapHeader();
    for (var i = 0; i < count; i++)
    {
      if (reader.NextMessagePackType == MessagePackType.String &&
          reader.TryReadStringSpan(out var key))
      {
        if (key.SequenceEqual(propertyBytes))
          return true;
      }
      else
      {
        reader.Skip(); // non-string or non-contiguous key
      }

      reader.Skip(); // value of a non-matching entry
    }

    return false;
  }

  private static bool CompareAndConsume(ref MessagePackReader reader, object value)
  {
    if (value is null)
    {
      if (reader.TryReadNil()) return true;
      reader.Skip();
      return false;
    }

    switch (value)
    {
      case string text:
        if (reader.NextMessagePackType != MessagePackType.String)
        {
          reader.Skip();
          return false;
        }

        return string.Equals(reader.ReadString(), text, StringComparison.Ordinal);

      case int number:
        return CompareInteger(ref reader, number);

      case long number:
        return CompareInteger(ref reader, number);

      case double number:
        switch (reader.NextMessagePackType)
        {
          case MessagePackType.Integer:
            if (reader.NextCode == MessagePackCode.UInt64)
              return reader.ReadUInt64() == number;
            return reader.ReadInt64() == number;
          case MessagePackType.Float:
            // Float32 payloads are compared in float space: the JSON route
            // compares against the shortest-roundtrip decimal form, and a
            // double filter value like 3.14 must keep matching a stored 3.14f.
            if (reader.NextCode == MessagePackCode.Float32)
              return reader.ReadSingle() == (float)number;
            return reader.ReadDouble() == number;
          default:
            reader.Skip();
            return false;
        }

      case bool flag:
        if (reader.NextMessagePackType != MessagePackType.Boolean)
        {
          reader.Skip();
          return false;
        }

        return reader.ReadBoolean() == flag;

      default:
        // Unsupported constant type: the JSON route reports no match too.
        reader.Skip();
        return false;
    }
  }

  private static bool CompareInteger(ref MessagePackReader reader, long expected)
  {
    if (reader.NextMessagePackType != MessagePackType.Integer)
    {
      reader.Skip();
      return false;
    }

    if (reader.NextCode == MessagePackCode.UInt64)
    {
      var unsigned = reader.ReadUInt64();
      return unsigned <= long.MaxValue && (long)unsigned == expected;
    }

    return reader.ReadInt64() == expected;
  }

  /// <summary>
  /// Fallback for filter implementations outside the built-in AST: their only
  /// contract is <see cref="IFilterExpression.Matches"/> on a JSON document.
  /// </summary>
  private static bool MatchesViaJson(ReadOnlyMemory<byte> document, IFilterExpression filter)
  {
    try
    {
      var json = MessagePackSerializer.ConvertToJson(document,
        MessagePackSerializerOptions.Standard.WithResolver(MessagePack.Resolvers.ContractlessStandardResolver.Instance));
      using var parsed = JsonDocument.Parse(json);
      return filter.Matches(parsed.RootElement);
    }
    catch
    {
      return false;
    }
  }
}
