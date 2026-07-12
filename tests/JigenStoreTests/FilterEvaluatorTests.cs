using System.Text.Json;
using Jigen.Filtering;
using MessagePack;
using MessagePack.Resolvers;

namespace JigenTests;

/// <summary>
/// The MessagePack filter evaluator must behave exactly like the JSON-based
/// IFilterExpression.Matches it replaces on the hot path: every case here is
/// asserted against BOTH evaluators.
/// </summary>
public class FilterEvaluatorTests
{
  private static readonly MessagePackSerializerOptions Options =
    MessagePackSerializerOptions.Standard.WithResolver(ContractlessStandardResolver.Instance);

  private static void AssertBothWays(object document, IFilterExpression filter, bool expected)
  {
    var serialized = (ReadOnlyMemory<byte>)MessagePackSerializer.Serialize(document, Options);

    var viaMessagePack = MessagePackFilterEvaluator.Matches(serialized, filter);
    Assert.Equal(expected, viaMessagePack);

    var json = MessagePackSerializer.ConvertToJson(serialized, Options);
    using var parsed = JsonDocument.Parse(json);
    var viaJson = filter.Matches(parsed.RootElement);
    Assert.Equal(expected, viaJson);
  }

  public sealed class Doc
  {
    public string Title { get; set; }
    public int Views { get; set; }
    public long BigNumber { get; set; }
    public double Rating { get; set; }
    public float Weight { get; set; }
    public bool Published { get; set; }
    public string Missing { get; set; }
    public List<string> Tags { get; set; }
    public List<int> Numbers { get; set; }
    public Meta Metadata { get; set; }
  }

  public sealed class Meta
  {
    public string Category { get; set; }
    public Inner Nested { get; set; }
  }

  public sealed class Inner
  {
    public string Code { get; set; }
  }

  private static Doc SampleDoc() => new()
  {
    Title = "Jigen",
    Views = 42,
    BigNumber = 5_000_000_000L,
    Rating = 4.5,
    Weight = 3.14f,
    Published = true,
    Missing = null,
    Tags = ["science", "linq", "città"],
    Numbers = [1, 2, 3],
    Metadata = new Meta { Category = "article", Nested = new Inner { Code = "X9" } }
  };

  [Theory]
  // string equality (ordinal, case sensitive)
  [InlineData("Title", "Jigen", true)]
  [InlineData("Title", "jigen", false)]
  [InlineData("Title", "other", false)]
  // case-sensitive property names
  [InlineData("title", "Jigen", false)]
  // missing property
  [InlineData("DoesNotExist", "x", false)]
  // nested dotted paths
  [InlineData("Metadata.Category", "article", true)]
  [InlineData("Metadata.Nested.Code", "X9", true)]
  [InlineData("Metadata.Nested.Code", "x9", false)]
  [InlineData("Metadata.Nope.Code", "X9", false)]
  // path through a non-object
  [InlineData("Title.Sub", "x", false)]
  public void PropertyEquals_strings(string path, string value, bool expected)
  {
    AssertBothWays(SampleDoc(), new PropertyEqualsFilter { PropertyPath = path, Value = value }, expected);
  }

  [Fact]
  public void PropertyEquals_numbers_and_booleans()
  {
    var doc = SampleDoc();

    AssertBothWays(doc, new PropertyEqualsFilter { PropertyPath = "Views", Value = 42 }, true);
    AssertBothWays(doc, new PropertyEqualsFilter { PropertyPath = "Views", Value = 43 }, false);
    AssertBothWays(doc, new PropertyEqualsFilter { PropertyPath = "Views", Value = 42L }, true);
    AssertBothWays(doc, new PropertyEqualsFilter { PropertyPath = "Views", Value = 42d }, true);

    AssertBothWays(doc, new PropertyEqualsFilter { PropertyPath = "BigNumber", Value = 5_000_000_000L }, true);
    // int filter against a value outside int range: both evaluators say no
    AssertBothWays(doc, new PropertyEqualsFilter { PropertyPath = "BigNumber", Value = 5 }, false);

    AssertBothWays(doc, new PropertyEqualsFilter { PropertyPath = "Rating", Value = 4.5 }, true);
    AssertBothWays(doc, new PropertyEqualsFilter { PropertyPath = "Rating", Value = 4.6 }, false);
    // int filter against a fractional value
    AssertBothWays(doc, new PropertyEqualsFilter { PropertyPath = "Rating", Value = 4 }, false);
    // float32-stored value against its decimal literal as double
    AssertBothWays(doc, new PropertyEqualsFilter { PropertyPath = "Weight", Value = 3.14 }, true);

    AssertBothWays(doc, new PropertyEqualsFilter { PropertyPath = "Published", Value = true }, true);
    AssertBothWays(doc, new PropertyEqualsFilter { PropertyPath = "Published", Value = false }, false);
    // type mismatches
    AssertBothWays(doc, new PropertyEqualsFilter { PropertyPath = "Published", Value = "true" }, false);
    AssertBothWays(doc, new PropertyEqualsFilter { PropertyPath = "Views", Value = "42" }, false);
  }

  [Fact]
  public void PropertyEquals_null_and_unsupported_values()
  {
    var doc = SampleDoc();

    AssertBothWays(doc, new PropertyEqualsFilter { PropertyPath = "Missing", Value = null }, true);
    AssertBothWays(doc, new PropertyEqualsFilter { PropertyPath = "Title", Value = null }, false);
    // unsupported constant type: both evaluators report no match
    AssertBothWays(doc, new PropertyEqualsFilter { PropertyPath = "Title", Value = new DateTime(2020, 1, 1) }, false);
  }

  [Fact]
  public void CollectionAny()
  {
    var doc = SampleDoc();

    AssertBothWays(doc, new PropertyCollectionAnyFilter { PropertyPath = "Tags", Value = "science" }, true);
    AssertBothWays(doc, new PropertyCollectionAnyFilter { PropertyPath = "Tags", Value = "città" }, true);
    AssertBothWays(doc, new PropertyCollectionAnyFilter { PropertyPath = "Tags", Value = "SCIENCE" }, false);
    AssertBothWays(doc, new PropertyCollectionAnyFilter { PropertyPath = "Tags", Value = "nope" }, false);
    AssertBothWays(doc, new PropertyCollectionAnyFilter { PropertyPath = "Numbers", Value = 2 }, true);
    AssertBothWays(doc, new PropertyCollectionAnyFilter { PropertyPath = "Numbers", Value = 9 }, false);
    // Any over a non-array and over a missing property
    AssertBothWays(doc, new PropertyCollectionAnyFilter { PropertyPath = "Title", Value = "Jigen" }, false);
    AssertBothWays(doc, new PropertyCollectionAnyFilter { PropertyPath = "Nope", Value = "x" }, false);
  }

  [Fact]
  public void And_or_composition()
  {
    var doc = SampleDoc();

    var titleOk = new PropertyEqualsFilter { PropertyPath = "Title", Value = "Jigen" };
    var titleKo = new PropertyEqualsFilter { PropertyPath = "Title", Value = "other" };
    var tagOk = new PropertyCollectionAnyFilter { PropertyPath = "Tags", Value = "linq" };

    AssertBothWays(doc, new AndFilter { Left = titleOk, Right = tagOk }, true);
    AssertBothWays(doc, new AndFilter { Left = titleOk, Right = titleKo }, false);
    AssertBothWays(doc, new OrFilter { Left = titleKo, Right = tagOk }, true);
    AssertBothWays(doc, new OrFilter { Left = titleKo, Right = titleKo }, false);
    AssertBothWays(doc,
      new AndFilter
      {
        Left = new OrFilter { Left = titleKo, Right = titleOk },
        Right = new PropertyEqualsFilter { PropertyPath = "Metadata.Category", Value = "article" }
      }, true);
  }

  public sealed class AlwaysTrueFilter : IFilterExpression
  {
    public bool Matches(JsonElement document) => true;
    public string ToDebugString() => "true";
  }

  [Fact]
  public void Custom_filter_implementations_fall_back_to_json()
  {
    var serialized = (ReadOnlyMemory<byte>)MessagePackSerializer.Serialize(SampleDoc(), Options);
    Assert.True(MessagePackFilterEvaluator.Matches(serialized, new AlwaysTrueFilter()));

    // ...also when nested inside the built-in AST
    Assert.True(MessagePackFilterEvaluator.Matches(serialized, new AndFilter
    {
      Left = new AlwaysTrueFilter(),
      Right = new PropertyEqualsFilter { PropertyPath = "Title", Value = "Jigen" }
    }));
  }

  [Fact]
  public void Null_filter_matches_everything_and_garbage_matches_nothing()
  {
    var serialized = (ReadOnlyMemory<byte>)MessagePackSerializer.Serialize(SampleDoc(), Options);
    Assert.True(MessagePackFilterEvaluator.Matches(serialized, null));

    var garbage = new ReadOnlyMemory<byte>([0xC1, 0xFF, 0x00]); // 0xC1 is never used in MessagePack
    Assert.False(MessagePackFilterEvaluator.Matches(garbage, new PropertyEqualsFilter { PropertyPath = "Title", Value = "x" }));
  }

  [Fact]
  public void Long_property_names_beyond_the_stackalloc_threshold()
  {
    var name = new string('p', 100);
    var document = new Dictionary<string, object> { [name] = "v" };
    var serialized = (ReadOnlyMemory<byte>)MessagePackSerializer.Serialize(document, Options);

    Assert.True(MessagePackFilterEvaluator.Matches(serialized,
      new PropertyEqualsFilter { PropertyPath = name, Value = "v" }));
  }
}
