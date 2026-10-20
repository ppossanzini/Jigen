using Hikyaku;
using Jigen.API.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jigen.API;

/// <summary>
/// Shared metadata channels. Metadata content is global to the database and is
/// addressed by an opaque, stable integer index. The association between an
/// embedding key and a metadata record is collection-scoped.
/// </summary>
[ApiController]
[Authorize]
public class MetadataController(IHikyaku mediator, IDocumentSerializer serializer) : ControllerBase
{
  // ── Metadata content (database-scoped) ──

  [HttpPost("~/api/database/{dbname}/metadata")]
  [ProducesResponseType(StatusCodes.Status201Created)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  public async Task<IActionResult> Create(string dbname, [FromBody] MetadataPayload payload,
    CancellationToken cancellationToken)
  {
    if (payload?.Payload is null)
      return BadRequest("Payload cannot be null");

    int metadataIndex;
    try
    {
      metadataIndex = await mediator.Send(new Core.Command.metadata.CreateMetadata
      {
        Database = dbname,
        Content = serializer.FromJson(Newtonsoft.Json.JsonConvert.SerializeObject(payload.Payload)).ToArray()
      }, cancellationToken);
    }
    catch (ArgumentException ex)
    {
      return BadRequest(ex.Message);
    }

    return StatusCode(StatusCodes.Status201Created, new { metadataIndex });
  }

  [HttpPut("~/api/database/{dbname}/metadata/{metadataIndex:int}")]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> Update(string dbname, int metadataIndex,
    [FromBody] MetadataPayload payload, CancellationToken cancellationToken)
  {
    if (payload?.Payload is null)
      return BadRequest("Payload cannot be null");

    bool updated;
    try
    {
      updated = await mediator.Send(new Core.Command.metadata.UpdateMetadata
      {
        Database = dbname,
        MetadataIndex = metadataIndex,
        Content = serializer.FromJson(Newtonsoft.Json.JsonConvert.SerializeObject(payload.Payload)).ToArray()
      }, cancellationToken);
    }
    catch (ArgumentException ex)
    {
      return BadRequest(ex.Message);
    }

    return updated ? Ok() : NotFound(new { updated = false });
  }

  // ── Association (collection-scoped) ──

  [HttpGet("~/api/database/{dbname}/collections/{collection}/documents/{key}/metadata")]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> GetAssociation(string dbname, string collection, string key,
    [FromQuery] string keyType = null)
  {
    if (!CollectionsController.TryResolveKey(key, keyType, out var keyVector))
      return BadRequest($"Key '{key}' is not valid for key type '{keyType}'");

    var metadataIndex = await mediator.Send(new Core.Query.metadata.GetMetadataIndex
    {
      Database = dbname,
      Collection = collection,
      Key = keyVector.Value
    });

    return metadataIndex < 0 ? NotFound() : Ok(new { metadataIndex });
  }

  [HttpPut("~/api/database/{dbname}/collections/{collection}/documents/{key}/metadata")]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  public async Task<IActionResult> Associate(string dbname, string collection, string key,
    [FromBody] MetadataAssociationPayload payload, [FromQuery] string keyType = null)
  {
    if (payload is null)
      return BadRequest("Payload cannot be null");

    if (!CollectionsController.TryResolveKey(key, keyType, out var keyVector))
      return BadRequest($"Key '{key}' is not valid for key type '{keyType}'");

    try
    {
      await mediator.Send(new Core.Command.metadata.AssociateEmbedding
      {
        Database = dbname,
        Collection = collection,
        Key = keyVector.Value,
        MetadataIndex = payload.MetadataIndex
      });
    }
    catch (ArgumentException ex)
    {
      return BadRequest(ex.Message);
    }

    return Ok();
  }

  // ── Grouped search ──

  [HttpPost("~/api/database/{dbname}/collections/{collection}/search/metadata")]
  [ProducesResponseType(typeof(IEnumerable<Core.Query.metadata.SearchByMetadataResultItem>), StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  public async Task<IActionResult> Search(string dbname, string collection,
    [FromBody] MetadataSearchData request, CancellationToken cancellationToken)
  {
    if (request is null)
      return BadRequest("Request payload is required");

    if (request.Top <= 0)
      return BadRequest("Top must be greater than zero");

    if (request.CandidateBudget <= 0)
      return BadRequest("CandidateBudget must be greater than zero");

    if (string.IsNullOrWhiteSpace(request.Sentence) && (request.Embeddings is null || request.Embeddings.Length == 0))
      return BadRequest("Provide either sentence or embeddings");

    IEnumerable<Core.Query.metadata.SearchByMetadataResultItem> result;
    try
    {
      result = await mediator.Send(new Core.Query.metadata.SearchByMetadata
      {
        Database = dbname,
        Collection = collection,
        Sentence = request.Sentence,
        Embeddings = request.Embeddings,
        Top = request.Top,
        CandidateBudget = request.CandidateBudget,
        EfSearch = request.EfSearch,
        NoContent = request.NoContent,
        MinScore = request.MinScore
      }, cancellationToken);
    }
    catch (ArgumentException ex)
    {
      return BadRequest(ex.Message);
    }

    return Ok(result);
  }
}
