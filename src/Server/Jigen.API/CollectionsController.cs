using Jigen.DataStructures;
using Hikyaku;
using Jigen.Core.Dto.collections;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jigen.API;

[ApiController]
[Route("~/api/database/{dbname}/collections")]
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
  [Route("{collection}/documents/{key:int}")]
  [Route("{collection}/documents/{key:guid}")]
  [Route("{collection}/documents/{key:long}")]
  [HttpPost, HttpPut, HttpPatch]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  public async Task<IActionResult> SetDocument(string dbname, string collection, [FromQuery] object key, [FromBody] Dto.DocumentPayload payload)
  {
    if (payload == null)
      return BadRequest("Payload cannot be null");

    if (dbname is null || collection  is null || key is null)
      return BadRequest();

    VectorKey keyVector = key switch
    {
      long l => VectorKey.From(l),
      Guid guid => VectorKey.From(guid),
      int i => VectorKey.From(i),
      string s => VectorKey.From(s),
      _ => default
    };

    await mediator.Send(new Core.Command.collections.SetDocument()
    {
      Database = dbname, Collection = collection,
      Key = keyVector.Value,
      Content = payload.Payload != null ? serializer.Serialize(payload.Payload).ToArray() : null,
      Sentence = payload.Sentence
    });
    return Ok();
  }

  [Route("{collection}/documents/{key}")]
  [Route("{collection}/documents/{key:int}")]
  [Route("{collection}/documents/{key:guid}")]
  [Route("{collection}/documents/{key:long}")]
  [HttpDelete]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  public async Task<IActionResult> DeleteDocument(string dbname, string collection, [FromQuery] object key)
  {
    if (dbname is null || collection  is null || key is null)
      return BadRequest();

    VectorKey keyVector = key switch
    {
      long l => VectorKey.From(l),
      Guid guid => VectorKey.From(guid),
      int i => VectorKey.From(i),
      string s => VectorKey.From(s),
      _ => default
    };

    await mediator.Send(new Core.Command.collections.DeleteVector()
    {
      Database = dbname,
      Collection = collection,
      Key = keyVector.Value,
    });
    return Ok();
  }


  [Route("{collection}/documents/{key}")]
  [Route("{collection}/documents/{key:int}")]
  [Route("{collection}/documents/{key:guid}")]
  [Route("{collection}/documents/{key:long}")]
  [HttpGet]
  [ProducesResponseType(typeof(byte[]), StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  public async Task<IActionResult> GetDocument(string dbname, string collection, [FromQuery] object key)
  {
    if (dbname is null || collection  is null || key is null)
      return BadRequest();

    VectorKey keyVector = key switch
    {
      long l => VectorKey.From(l),
      Guid guid => VectorKey.From(guid),
      int i => VectorKey.From(i),
      string s => VectorKey.From(s),
      _ => default
    };

    var result = await mediator.Send(new Core.Query.collections.GetRawContent()
    {
      Database = dbname,
      Collection = collection,
      Key = keyVector.Value
    });

    return Ok(result);
  }

  [Route("{collection}/documents/{key}/json")]
  [Route("{collection}/documents/{key:int}/json")]
  [Route("{collection}/documents/{key:guid}/json")]
  [Route("{collection}/documents/{key:long}/json")]
  [HttpGet]
  [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  public async Task<IActionResult> GetDocumentJson(string dbname, string collection, [FromQuery] object key)
  {
    if (dbname is null || collection  is null || key is null)
      return BadRequest();

    VectorKey keyVector = key switch
    {
      long l => VectorKey.From(l),
      Guid guid => VectorKey.From(guid),
      int i => VectorKey.From(i),
      string s => VectorKey.From(s),
      _ => default
    };

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
}