using System.Text.Json;
using Jigen.API.Dto;

namespace Jigen.API.Tests;

/// <summary>
/// The sparse payloads travel as JSON: their shape must survive the web
/// serializer the controllers use, including the integer term ids used as keys.
/// </summary>
public class SparsePayloadBindingTests
{
  private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

  [Fact]
  public void Vector_payload_binds_sparse_term_maps()
  {
    const string json = """
      {
        "payload": { "title": "one" },
        "vectors": [[0.1, 0.2]],
        "sparse": [{ "7": 1.5, "11": 0.5 }]
      }
      """;

    var payload = JsonSerializer.Deserialize<VectorPayload>(json, Options);

    Assert.NotNull(payload);
    Assert.Single(payload.Sparse);
    Assert.Equal(1.5f, payload.Sparse[0][7]);
    Assert.Equal(0.5f, payload.Sparse[0][11]);
  }

  [Fact]
  public void Document_payload_binds_a_single_sparse_term_map()
  {
    const string json = """
      {
        "payload": { "title": "one" },
        "sentence": "hello",
        "sparse": { "7": 2.0 }
      }
      """;

    var payload = JsonSerializer.Deserialize<DocumentPayload>(json, Options);

    Assert.NotNull(payload);
    Assert.NotNull(payload.Sparse);
    Assert.Equal(2f, payload.Sparse[7]);
  }

  [Fact]
  public void Bulk_payloads_bind_sparse_terms_case_insensitively()
  {
    const string json = """
      [
        {
          "key": "a",
          "keyType": "string",
          "vectors": [[0.1]],
          "Sparse": [{ "7": 1.0 }]
        }
      ]
      """;

    var items = JsonSerializer.Deserialize<BulkVectorItem[]>(json, Options);

    Assert.NotNull(items);
    Assert.Single(items);
    Assert.Equal(1f, items[0].Sparse[0][7]);
  }
}
