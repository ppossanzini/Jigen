using Jigen.DataStructures;
using Hikyaku;
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

  [HttpPost("search")]
  [ProducesResponseType(typeof(SearchCollectionsResult), StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  public async Task<IActionResult> Search(string dbname, [FromBody] SearchCollectionsData request, CancellationToken cancellationToken)
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

  [Route("{collection}/documents/{key}")]
  [HttpPost, HttpPut, HttpPatch]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  public async Task<IActionResult> SetDocument(string dbname, string collection, string key, [FromBody] Dto.DocumentPayload payload, [FromQuery] string keyType = null)
  {
    if (payload == null)
      return BadRequest("Payload cannot be null");

    if (!TryResolveKey(key, keyType, out var keyVector))
      return BadRequest($"Key '{key}' is not valid for key type '{keyType}'");

    // The body is bound by the JSON input formatter, so Payload is a JSON
    // token (JObject/JValue), not a CLR contract: serialize it through the
    // JSON bridge instead of the typed path.
    await mediator.Send(new Core.Command.collections.SetDocument()
    {
      Database = dbname, Collection = collection,
      Key = keyVector.Value,
      Content = payload.Payload != null
        ? serializer.FromJson(Newtonsoft.Json.JsonConvert.SerializeObject(payload.Payload)).ToArray()
        : null,
      Sentence = payload.Sentence
    });
    return Ok();
  }

  [Route("{collection}/documents/{key}")]
  [HttpDelete]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  public async Task<IActionResult> DeleteDocument(string dbname, string collection, string key, [FromQuery] string keyType = null)
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
  public async Task<IActionResult> GetDocument(string dbname, string collection, string key, [FromQuery] string keyType = null)
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
  public async Task<IActionResult> GetDocumentJson(string dbname, string collection, string key, [FromQuery] string keyType = null)
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