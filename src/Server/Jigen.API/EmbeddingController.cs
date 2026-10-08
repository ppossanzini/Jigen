using Hikyaku;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Jigen.Embedding.Core.Options;
using Microsoft.Extensions.Options;

namespace Jigen.API;

[ApiController]
[Route("~/api/embeddings")]
[Authorize]
public class EmbeddingController(IHikyaku mediator, IOptions<PassageSplittingOptions> passageOptions) : ControllerBase
{
  /// <summary>Compute embeddings for a single sentence.</summary>
  [HttpPost]
  [ProducesResponseType(typeof(float[]), StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  public async Task<IActionResult> CalculateEmbeddings([FromBody] CalculateEmbeddingsRequest request,
    CancellationToken cancellationToken)
  {
    if (request == null || string.IsNullOrWhiteSpace(request.Message))
      return BadRequest("Message is required");

    var result = await mediator.Send(new Embedding.Core.Commands.CalculateEmbeddings
    {
      Model = request.Model,
      Task = request.Task,
      Sentence = request.Message
    }, cancellationToken);

    return Ok(result);
  }

  /// <summary>Compute the same text in multiple configured embedding spaces.</summary>
  [HttpPost("multi")]
  [ProducesResponseType(typeof(Dictionary<string, float[]>), StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  public async Task<IActionResult> CalculateEmbeddingsMulti([FromBody] CalculateEmbeddingsMultiRequest request,
    CancellationToken cancellationToken)
  {
    if (request == null || string.IsNullOrWhiteSpace(request.Message))
      return BadRequest("Message is required");
    if (request.Models == null || request.Models.Length == 0 || request.Models.Any(string.IsNullOrWhiteSpace))
      return BadRequest("At least one valid model is required");

    var result = await mediator.Send(new Embedding.Core.Commands.CalculateEmbeddingsMulti
    {
      Sentence = request.Message,
      Task = request.Task,
      Models = request.Models
    }, cancellationToken);
    return Ok(result);
  }

  /// <summary>Compute embeddings for multiple sentences in one call.</summary>
  [HttpPost("batch")]
  [ProducesResponseType(typeof(EmbeddingBatchResult), StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  public async Task<IActionResult> CalculateEmbeddingsBatch([FromBody] CalculateEmbeddingsBatchRequest request,
    CancellationToken cancellationToken)
  {
    if (request == null || request.Messages == null || request.Messages.Length == 0)
      return BadRequest("Messages array is required");

    // Filter out blank inputs, preserving positions with empty rows
    var indexes = new List<int>(request.Messages.Length);
    for (var i = 0; i < request.Messages.Length; i++)
      if (!string.IsNullOrWhiteSpace(request.Messages[i]))
        indexes.Add(i);

    var results = new float[request.Messages.Length][];

    if (indexes.Count > 0)
    {
      var vectors = await mediator.Send(new Embedding.Core.Commands.CalculateEmbeddingsBatch
      {
        Model = request.Model,
        Task = request.Task,
        Sentences = indexes.Select(i => request.Messages[i]).ToArray()
      }, cancellationToken);

      for (var i = 0; i < indexes.Count; i++)
        results[indexes[i]] = vectors[i];
    }

    // Fill empty rows for blank inputs
    for (var i = 0; i < results.Length; i++)
      results[i] ??= [];

    return Ok(new EmbeddingBatchResult { Results = results });
  }

  /// <summary>Split text into overlapping token passages and compute one embedding per passage.</summary>
  /// <param name="request">Text, model and passage sizing options.</param>
  /// <param name="cancellationToken">Request cancellation token.</param>
  /// <returns>The ordered passages and their embeddings.</returns>
  [HttpPost("passages")]
  [ProducesResponseType(typeof(Embedding.Core.Dto.PassageEmbedding[]), StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  public async Task<IActionResult> CalculatePassageEmbeddings(
    [FromBody] CalculatePassageEmbeddingsRequest request,
    CancellationToken cancellationToken)
  {
    if (request == null || string.IsNullOrWhiteSpace(request.Message))
      return BadRequest("Message is required");
    var size = request.PassageTokenSize ?? passageOptions.Value.DefaultTokenSize;
    var overlap = request.PassageOverlapSize ?? passageOptions.Value.DefaultOverlapSize;
    if (size <= 0 || size > passageOptions.Value.MaxTokenSize)
      return BadRequest($"PassageTokenSize must be between 1 and {passageOptions.Value.MaxTokenSize}");
    if (overlap < 0 || overlap > passageOptions.Value.MaxOverlapSize || overlap >= size)
      return BadRequest($"PassageOverlapSize must be non-negative, lower than PassageTokenSize and not exceed {passageOptions.Value.MaxOverlapSize}");

    var result = await mediator.Send(new Embedding.Core.Commands.CalculatePassageEmbeddings
    {
      Model = request.Model,
      Task = request.Task,
      Sentence = request.Message,
      PassageTokenSize = size,
      PassageOverlapSize = overlap
    }, cancellationToken);

    return Ok(result);
  }

  /// <summary>Return how many passages the server would produce without calculating embeddings.</summary>
  [HttpPost("passages/count")]
  [ProducesResponseType(typeof(PassageSplitCountResult), StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  public async Task<IActionResult> CalculatePassageSplitCount(
    [FromBody] CalculatePassageSplitCountRequest request,
    CancellationToken cancellationToken)
  {
    if (request == null || string.IsNullOrWhiteSpace(request.Message))
      return BadRequest("Message is required");
    var size = request.PassageTokenSize ?? passageOptions.Value.DefaultTokenSize;
    var overlap = request.PassageOverlapSize ?? passageOptions.Value.DefaultOverlapSize;
    if (size <= 0 || size > passageOptions.Value.MaxTokenSize)
      return BadRequest($"PassageTokenSize must be between 1 and {passageOptions.Value.MaxTokenSize}");
    if (overlap < 0 || overlap > passageOptions.Value.MaxOverlapSize || overlap >= size)
      return BadRequest($"PassageOverlapSize must be non-negative, lower than PassageTokenSize and not exceed {passageOptions.Value.MaxOverlapSize}");

    var count = await mediator.Send(new Embedding.Core.Commands.CalculatePassageSplitCount
    {
      Model = request.Model,
      Sentence = request.Message,
      PassageTokenSize = size,
      PassageOverlapSize = overlap
    }, cancellationToken);

    return Ok(new PassageSplitCountResult { Count = count });
  }
}

public class CalculateEmbeddingsRequest
{
  public string Message { get; set; }
  public string Model { get; set; }
  public string Task { get; set; }
}

public class CalculateEmbeddingsBatchRequest
{
  public string[] Messages { get; set; }
  public string Model { get; set; }
  public string Task { get; set; }
}

public class CalculateEmbeddingsMultiRequest
{
  public string Message { get; set; }
  public string Task { get; set; }
  public string[] Models { get; set; }
}

public class EmbeddingBatchResult
{
  public float[][] Results { get; set; }
}

public class CalculatePassageEmbeddingsRequest
{
  public string Message { get; set; }
  public string Model { get; set; }
  public string Task { get; set; }
  public int? PassageTokenSize { get; set; }
  public int? PassageOverlapSize { get; set; }
}

public class CalculatePassageSplitCountRequest
{
  public string Message { get; set; }
  public string Model { get; set; }
  public int? PassageTokenSize { get; set; }
  public int? PassageOverlapSize { get; set; }
}

public class PassageSplitCountResult
{
  public int Count { get; set; }
}
