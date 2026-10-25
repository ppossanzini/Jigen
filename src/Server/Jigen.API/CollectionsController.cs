using Jigen.DataStructures;
using Hikyaku;
using Jigen.API.Dto;
using Jigen.Core.Dto.collections;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jigen.API;

[ApiController]
[Route("~/api/database/{dbname}/collections")]
[Authorize]
public class CollectionsController(IHikyaku mediator, IDocumentSerializer serializer) : ControllerBase
{
  // ── Collection listing & metadata ──

  [HttpGet]
  [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
  public async Task<IActionResult> GetCollections(string dbname)
  {
    var result = await mediator.Send(new Core.Query.collections.ListCollections()
    {
      Database = dbname
    });
    return Ok(result);
  }

  [HttpGet("info")]
  [ProducesResponseType(typeof(IEnumerable<CollectionInfo>), StatusCodes.Status200OK)]
  public async Task<IActionResult> GetCollectionsInfo(string dbname)
  {
    var result = await mediator.Send(new Core.Query.collections.GetCollectionsInfo()
    {
      Database = dbname
    });
    return Ok(result);
  }

  [HttpGet("{collection}/info")]
  [ProducesResponseType(typeof(CollectionInfo), StatusCodes.Status200OK)]
  public async Task<IActionResult> GetCollectionInfo(string dbname, string collection)
  {
    var result = await mediator.Send(new Core.Query.collections.GetCollectionInfo()
    {
      Database = dbname,
      Collection = collection
    });
    return Ok(result);
  }

  [HttpGet("{collection}/count")]
  [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
  public async Task<IActionResult> Count(string dbname, string collection)
  {
    var result = await mediator.Send(new Core.Command.collections.Count()
    {
      Database = dbname,
      Collection = collection
    });
    return Ok(result);
  }

  [HttpGet("{collection}/graph")]
  [ProducesResponseType(typeof(IndexGraphSnapshot), StatusCodes.Status200OK)]
  public async Task<IActionResult> GetCollectionGraph(string dbname, string collection,
    [FromQuery] int dimensions = 2,
    [FromQuery] int limit = 2000,
    [FromQuery] int? level = null,
    CancellationToken cancellationToken = default)
  {
    var result = await mediator.Send(new Core.Query.collections.GetCollectionGraph
    {
      Database = dbname,
      Collection = collection,
      Dimensions = dimensions,
      Limit = limit,
      Level = level
    }, cancellationToken);
    return Ok(result);
  }

  [HttpDelete("{collection}")]
  [ProducesResponseType(StatusCodes.Status200OK)]
  public async Task<IActionResult> Clear(string dbname, string collection)
  {
    await mediator.Send(new Core.Command.collections.Clear()
    {
      Database = dbname,
      Collection = collection
    });
    return Ok();
  }

  [HttpGet("{collection}/keys")]
  [ProducesResponseType(typeof(IEnumerable<byte[]>), StatusCodes.Status200OK)]
  public async Task<IActionResult> GetAllKeys(string dbname, string collection)
  {
    var result = await mediator.Send(new Core.Query.collections.GetAllKeys()
    {
      Database = dbname,
      Collection = collection
    });
    return Ok(result.Select(k => k.Value));
  }

  // ── Sparse terms ──

  // The sparse payloads are validated the same way as the gRPC ones: a
  // negative term id cannot name a term, and more term lists than vectors
  // would attach terms to vectors that do not exist.
  private static bool TryToSparseVectors(Dictionary<int, float>[] sparse, float[][] vectors,
    out SparseTerm[][] result, out string error)
  {
    result = null;
    error = null;
    if (sparse is not { Length: > 0 })
      return true;

    if (sparse.Length > (vectors?.Length ?? 0))
    {
      error = "Sparse carries more term lists than vectors; they are aligned by index.";
      return false;
    }

    result = new SparseTerm[sparse.Length][];
    for (var i = 0; i < sparse.Length; i++)
    {
      if (!TryToSparseTerms(sparse[i], out var terms, out error))
        return false;
      result[i] = terms ?? [];
    }

    return true;
  }

  /// <summary>
  /// Sparse half of a query derived from its text: the same tokenizer pass that
  /// yields the dense embedding yields these terms, so a caller can search in
  /// hybrid with a sentence alone.
  /// </summary>
  private async Task<IReadOnlyDictionary<int, float>> SparseQueryFromSentence(string sentence,
    CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(sentence))
      return null;

    var sparse = await mediator.Send(new Embedding.Core.Commands.CalculateSparseEmbedding
    {
      Sentence = sentence
    }, cancellationToken);

    if (sparse.Terms.Length == 0)
      return null;

    var query = new Dictionary<int, float>(sparse.Terms.Length);
    for (var i = 0; i < sparse.Terms.Length; i++)
      query[sparse.Terms[i]] = sparse.Weights[i];

    return query;
  }

  private static bool TryToSparseTerms(Dictionary<int, float> sparse, out SparseTerm[] result, out string error)
  {
    result = null;
    error = null;
    if (sparse is not { Count: > 0 })
      return true;

    result = new SparseTerm[sparse.Count];
    var index = 0;
    foreach (var (term, weight) in sparse)
    {
      if (term < 0)
      {
        error = "Sparse term ids cannot be negative.";
        return false;
      }

      result[index++] = new SparseTerm(term, weight);
    }

    return true;
  }

  // ── Document CRUD ──

  [Route("{collection}/documents/{key}")]
  [HttpPost, HttpPut, HttpPatch]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  public async Task<IActionResult> SetDocument(string dbname, string collection, string key,
    [FromBody] DocumentPayload payload, [FromQuery] string keyType = null)
  {
    if (payload == null)
      return BadRequest("Payload cannot be null");

    if (!TryResolveKey(key, keyType, out var keyVector))
      return BadRequest($"Key '{key}' is not valid for key type '{keyType}'");

    if (!TryToSparseTerms(payload.Sparse, out var sparse, out var sparseError))
      return BadRequest(sparseError);

    await mediator.Send(new Core.Command.collections.SetDocument()
    {
      Database = dbname, Collection = collection,
      Key = keyVector.Value,
      Content = payload.Payload != null
        ? serializer.FromJson(Newtonsoft.Json.JsonConvert.SerializeObject(payload.Payload)).ToArray()
        : null,
      Sentence = payload.Sentence,
      SparseTerms = sparse,
      SparseFromSentence = payload.SparseFromSentence
    });
    return Ok();
  }

  [Route("{collection}/documents/{key}")]
  [HttpDelete]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  public async Task<IActionResult> DeleteDocument(string dbname, string collection, string key,
    [FromQuery] string keyType = null)
  {
    if (!TryResolveKey(key, keyType, out var keyVector))
      return BadRequest($"Key '{key}' is not valid for key type '{keyType}'");

    await mediator.Send(new Core.Command.collections.DeleteVector()
    {
      Database = dbname,
      Collection = collection,
      Key = keyVector.Value,
    });
    return Ok();
  }

  [Route("{collection}/documents/{key}")]
  [HttpGet]
  [ProducesResponseType(typeof(byte[]), StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  public async Task<IActionResult> GetDocument(string dbname, string collection, string key,
    [FromQuery] string keyType = null)
  {
    if (!TryResolveKey(key, keyType, out var keyVector))
      return BadRequest($"Key '{key}' is not valid for key type '{keyType}'");

    var result = await mediator.Send(new Core.Query.collections.GetRawContent()
    {
      Database = dbname,
      Collection = collection,
      Key = keyVector.Value
    });

    return Ok(result);
  }

  [Route("{collection}/documents/{key}/json")]
  [HttpGet]
  [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  public async Task<IActionResult> GetDocumentJson(string dbname, string collection, string key,
    [FromQuery] string keyType = null)
  {
    if (!TryResolveKey(key, keyType, out var keyVector))
      return BadRequest($"Key '{key}' is not valid for key type '{keyType}'");

    var result = await mediator.Send(new Core.Query.collections.GetRawContent()
    {
      Database = dbname,
      Collection = collection,
      Key = keyVector.Value,
    });

    return Ok(new
    {
      key, collection,
      content = serializer.ToJson(result)
    });
  }

  [Route("{collection}/documents/{key}")]
  [HttpHead]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  public async Task<IActionResult> ContainsDocument(string dbname, string collection, string key,
    [FromQuery] string keyType = null)
  {
    if (!TryResolveKey(key, keyType, out var keyVector))
      return BadRequest($"Key '{key}' is not valid for key type '{keyType}'");

    var exists = await mediator.Send(new Core.Command.collections.Contains()
    {
      Database = dbname,
      Collection = collection,
      Key = keyVector.Value,
    });

    return exists ? Ok() : NotFound();
  }

  [HttpGet("{collection}/documents/{key}/embeddings")]
  [ProducesResponseType(typeof(float[][]), StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  public async Task<IActionResult> GetEmbeddings(string dbname, string collection, string key,
    [FromQuery] string keyType = null)
  {
    if (!TryResolveKey(key, keyType, out var keyVector))
      return BadRequest($"Key '{key}' is not valid for key type '{keyType}'");

    var result = await mediator.Send(new Core.Query.collections.GetEmbeddings()
    {
      Database = dbname,
      Collection = collection,
      Key = keyVector.Value
    });

    return Ok(result ?? []);
  }

  // ── Vector CRUD (pre-computed embeddings) ──

  [Route("{collection}/vectors/{key}")]
  [HttpPost, HttpPut]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  public async Task<IActionResult> SetVector(string dbname, string collection, string key,
    [FromBody] VectorPayload payload, [FromQuery] string keyType = null)
  {
    if (payload?.Vectors is not { Length: > 0 })
      return BadRequest("Vector payload with a vectors array is required");

    if (!TryResolveKey(key, keyType, out var keyVector))
      return BadRequest($"Key '{key}' is not valid for key type '{keyType}'");

    if (!TryToSparseVectors(payload.Sparse, payload.Vectors, out var sparse, out var sparseError))
      return BadRequest(sparseError);

    await mediator.Send(new Core.Command.collections.SetVector()
    {
      Database = dbname, Collection = collection,
      Key = keyVector.Value,
      Content = payload.Payload != null
        ? serializer.FromJson(Newtonsoft.Json.JsonConvert.SerializeObject(payload.Payload)).ToArray()
        : null,
      Vectors = payload.Vectors,
      SparseVectors = sparse
    });
    return Ok();
  }

  // ── Append-only variants (reject on existing key) ──

  [HttpPost("{collection}/documents/{key}/append")]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status409Conflict)]
  public async Task<IActionResult> AppendDocument(string dbname, string collection, string key,
    [FromBody] DocumentPayload payload, [FromQuery] string keyType = null)
  {
    if (payload == null)
      return BadRequest("Payload cannot be null");

    if (!TryResolveKey(key, keyType, out var keyVector))
      return BadRequest($"Key '{key}' is not valid for key type '{keyType}'");

    if (!TryToSparseTerms(payload.Sparse, out var sparse, out var sparseError))
      return BadRequest(sparseError);

    await mediator.Send(new Core.Command.collections.AppendDocument()
    {
      Database = dbname, Collection = collection,
      Key = keyVector.Value,
      Content = payload.Payload != null
        ? serializer.FromJson(Newtonsoft.Json.JsonConvert.SerializeObject(payload.Payload)).ToArray()
        : null,
      Sentence = payload.Sentence,
      SparseTerms = sparse,
      SparseFromSentence = payload.SparseFromSentence
    });
    return Ok();
  }

  [HttpPost("{collection}/vectors/{key}/append")]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status409Conflict)]
  public async Task<IActionResult> AppendVector(string dbname, string collection, string key,
    [FromBody] VectorPayload payload, [FromQuery] string keyType = null)
  {
    if (payload?.Vectors is not { Length: > 0 })
      return BadRequest("Vector payload with a vectors array is required");

    if (!TryResolveKey(key, keyType, out var keyVector))
      return BadRequest($"Key '{key}' is not valid for key type '{keyType}'");

    if (!TryToSparseVectors(payload.Sparse, payload.Vectors, out var sparse, out var sparseError))
      return BadRequest(sparseError);

    await mediator.Send(new Core.Command.collections.AppendVector()
    {
      Database = dbname, Collection = collection,
      Key = keyVector.Value,
      Content = payload.Payload != null
        ? serializer.FromJson(Newtonsoft.Json.JsonConvert.SerializeObject(payload.Payload)).ToArray()
        : null,
      Vectors = payload.Vectors,
      SparseVectors = sparse
    });
    return Ok();
  }

  [HttpPost("{collection}/vectors/{key}/raw")]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  public async Task<IActionResult> SetRawVector(string dbname, string collection, string key,
    [FromBody] VectorPayload payload, [FromQuery] string keyType = null)
  {
    if (payload?.Vectors is not { Length: > 0 })
      return BadRequest("Vector payload with a vectors array is required");

    if (!TryResolveKey(key, keyType, out var keyVector))
      return BadRequest($"Key '{key}' is not valid for key type '{keyType}'");

    if (!TryToSparseVectors(payload.Sparse, payload.Vectors, out var sparse, out var sparseError))
      return BadRequest(sparseError);

    await mediator.Send(new Core.Command.collections.SetRawVector()
    {
      Database = dbname, Collection = collection,
      Key = keyVector.Value,
      Content = payload.Payload != null
        ? serializer.FromJson(Newtonsoft.Json.JsonConvert.SerializeObject(payload.Payload)).ToArray()
        : null,
      Vectors = payload.Vectors,
      SparseVectors = sparse
    });
    return Ok();
  }

  [HttpPost("{collection}/vectors/bulk")]
  [ProducesResponseType(typeof(BulkResult), StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  public async Task<IActionResult> SetVectorsBulk(string dbname, string collection,
    [FromBody] IEnumerable<BulkVectorItem> items)
  {
    if (items == null)
      return BadRequest("Items array is required");

    var accepted = 0;
    foreach (var item in items)
    {
      if (!TryResolveKey(item.Key, item.KeyType, out var keyVector))
        continue;

      // An invalid sparse payload skips the item: storing it without its
      // terms would silently index a document that no sparse query can match.
      if (!TryToSparseVectors(item.Sparse, item.Vectors, out var sparse, out _))
        continue;

      await mediator.Send(new Core.Command.collections.SetVector()
      {
        Database = dbname, Collection = collection,
        Key = keyVector.Value,
        Content = item.Payload != null
          ? serializer.FromJson(Newtonsoft.Json.JsonConvert.SerializeObject(item.Payload)).ToArray()
          : null,
        Vectors = item.Vectors,
        SparseVectors = sparse
      });
      accepted++;
    }

    return Ok(new BulkResult { Accepted = accepted });
  }

  [HttpPost("{collection}/documents/bulk")]
  [ProducesResponseType(typeof(BulkResult), StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  public async Task<IActionResult> SetDocumentsBulk(string dbname, string collection,
    [FromBody] IEnumerable<BulkDocumentItem> items)
  {
    if (items == null)
      return BadRequest("Items array is required");

    var accepted = 0;
    foreach (var item in items)
    {
      if (!TryResolveKey(item.Key, item.KeyType, out var keyVector))
        continue;

      if (!TryToSparseTerms(item.Sparse, out var sparse, out _))
        continue;

      await mediator.Send(new Core.Command.collections.SetDocument()
      {
        Database = dbname, Collection = collection,
        Key = keyVector.Value,
        Content = item.Payload != null
          ? serializer.FromJson(Newtonsoft.Json.JsonConvert.SerializeObject(item.Payload)).ToArray()
          : null,
        Sentence = item.Sentence,
        SparseTerms = sparse,
        SparseFromSentence = item.SparseFromSentence
      });
      accepted++;
    }

    return Ok(new BulkResult { Accepted = accepted });
  }

  // ── Search ──

  [HttpPost("search")]
  [ProducesResponseType(typeof(SearchCollectionsResult), StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  public async Task<IActionResult> Search(string dbname, [FromBody] SearchCollectionsData request,
    CancellationToken cancellationToken)
  {
    if (request == null)
      return BadRequest("Request payload is required");

    if (request.Collections == null || !request.Collections.Any())
      return BadRequest("At least one collection is required");

    if (string.IsNullOrWhiteSpace(request.Sentence) && (request.Embeddings == null || !request.Embeddings.Any()))
      return BadRequest("Provide either sentence or embeddings");

    var result = await mediator.Send(new Core.Query.collections.SearchCollections
    {
      Database = dbname,
      Data = request
    }, cancellationToken);

    return Ok(result);
  }

  [HttpPost("{collection}/search")]
  [ProducesResponseType(typeof(IEnumerable<Core.Query.collections.SearchVectorResultItem>), StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  public async Task<IActionResult> SearchCollection(string dbname, string collection,
    [FromBody] SearchData request, CancellationToken cancellationToken)
  {
    if (request == null)
      return BadRequest("Request payload is required");

    if (string.IsNullOrWhiteSpace(request.Sentence)
        && (request.Embeddings == null || request.Embeddings.Length == 0)
        && request.SparseQuery == null)
      return BadRequest("Provide either sentence, embeddings or sparseQuery");

    float[] queryEmbeddings = request.Embeddings;

    // Sentence-based search: compute embedding first, then search.
    if (!string.IsNullOrWhiteSpace(request.Sentence))
    {
      queryEmbeddings = await mediator.Send(new Embedding.Core.Commands.CalculateEmbeddings
      {
        Sentence = request.Sentence,
        Task = "search_query"
      }, cancellationToken);
    }

    // An explicit sparse query always wins; asking for the derived one fills the
    // half the caller did not send, from the same sentence that produced the
    // dense embedding.
    var sparseQuery = request.SparseQuery ?? (request.SparseFromSentence
      ? await SparseQueryFromSentence(request.Sentence, cancellationToken)
      : null);

    var result = await mediator.Send(new Core.Query.collections.SearchVector
    {
      Database = dbname,
      Collection = collection,
      Embeddings = queryEmbeddings,
      Top = request.Top > 0 ? request.Top : 10,
      SparseQuery = sparseQuery,
      MinDenseScore = request.MinDenseScore,
      MinSparseScore = request.MinSparseScore
    }, cancellationToken);

    return Ok(result);
  }

  [HttpPost("search-filter")]
  [ProducesResponseType(typeof(IEnumerable<Core.Query.collections.SearchVectorResultItem>), StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  public async Task<IActionResult> SearchFilter(string dbname,
    [FromBody] SearchFilterData request, CancellationToken cancellationToken)
  {
    if (request == null)
      return BadRequest("Request payload is required");

    // Filter-only enumeration: dispatch SearchVector with empty embeddings.
    var result = await mediator.Send(new Core.Query.collections.SearchVector
    {
      Database = dbname,
      Collection = request.Collection,
      Embeddings = [],
      Top = int.MaxValue,
      Filter = null // TODO: filter translation from request
    }, cancellationToken);

    return Ok(result);
  }

  // ── Key resolution helper ──

  /// <summary>
  /// Converts the {key} route segment to the byte layout of the VectorKey it
  /// was stored with. The layout depends on the CLR type used at insert time
  /// (int = 4 bytes, long = 8, guid = 16, string = UTF-8), so reads must use
  /// the same type: pass ?keyType=string|int|long|guid to force it, or rely
  /// on detection (guid, then long for integers, then string).
  /// </summary>
  private static bool TryResolveKey(string key, string keyType, out VectorKey result)
  {
    result = default;
    if (string.IsNullOrEmpty(key))
      return false;

    switch (keyType?.ToLowerInvariant())
    {
      case "string":
        result = VectorKey.From(key);
        return true;
      case "int":
        if (!int.TryParse(key, out var intKey)) return false;
        result = VectorKey.From(intKey);
        return true;
      case "long":
        if (!long.TryParse(key, out var longKey)) return false;
        result = VectorKey.From(longKey);
        return true;
      case "guid":
        if (!Guid.TryParse(key, out var guidKey)) return false;
        result = VectorKey.From(guidKey);
        return true;
      case null or "":
        result = Guid.TryParse(key, out var guid) ? VectorKey.From(guid)
          : long.TryParse(key, out var number) ? VectorKey.From(number)
          : VectorKey.From(key);
        return true;
      default:
        return false;
    }
  }
}