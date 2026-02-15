using Jigen.DataStructures;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Jigen.API;

[ApiController]
[Route("database/{dbname}/collections")]
public class CollectionsController(IMediator mediator) : ControllerBase
{
  [HttpGet]
  public async Task<IActionResult> GetCollections(string dbname)
  {
    var result = await mediator.Send(new Core.Query.collections.ListCollections()
    {
      Database = dbname
    });
    return Ok(result);
  }

  [HttpGet("{collection}/info")]
  public async Task<IActionResult> GetCollectionInfo(string dbname, string collection)
  {
    var result = await mediator.Send(new Core.Query.collections.GetCollectionInfo()
    {
      Database = dbname
    });
    return Ok(result);
  }

  [Route("{collection}/documents/{key}")]
  [Route("{collection}/documents/{key:int}")]
  [Route("{collection}/documents/{key:guid}")]
  [Route("{collection}/documents/{key:long}")]
  [HttpPost, HttpPut, HttpPatch]
  public async Task<IActionResult> SetDocument(string dbname, string collection, [FromQuery]object key, [FromBody] Dto.DocumentPayload payload)
  {
    if (payload == null)
      return BadRequest("Payload cannot be null");

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
      Content = payload.Payload,
      Sentence = payload.Sentence
    });
    return Ok();
  }

  [Route("{collection}/documents/{key}")]
  [Route("{collection}/documents/{key:int}")]
  [Route("{collection}/documents/{key:guid}")]
  [Route("{collection}/documents/{key:long}")]
  [HttpDelete]
  public async Task<IActionResult> DeleteDocument(string dbname, string collection, [FromQuery]object key)
  {
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
  public async Task<IActionResult> GetDocument(string dbname, string collection, [FromQuery]object key)
  {
    VectorKey keyVector = key switch
    {
      long l => VectorKey.From(l),
      Guid guid => VectorKey.From(guid),
      int i => VectorKey.From(i),
      string s => VectorKey.From(s),
      _ => default
    };

    var result = await mediator.Send(new Core.Query.collections.GetContent()
    {
      Database = dbname,
      Collection = collection,
      Key = keyVector.Value,
      ResultType = typeof(string)
    });

    return Ok(result);
  }
}