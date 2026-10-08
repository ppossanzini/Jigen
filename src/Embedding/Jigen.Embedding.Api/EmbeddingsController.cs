using Hikyaku;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Jigen.Embedding.Core.Options;
using Microsoft.Extensions.Options;

namespace Jigen.Embedding.Api;

[ApiController]
[Route("~/api/embeddings")]
public class EmbeddingsController(
  IHikyaku hikyaku,
  IConfiguration configuration,
  IOptions<PassageSplittingOptions> passageOptions) : ControllerBase
{
  [HttpGet("tasks")]
  [ProducesResponseType(typeof(string[]), StatusCodes.Status200OK)]
  public IActionResult Get()
  {
    return Ok(configuration.GetSection("JigenEmbeddings:Tasks").Get<string[]>() ?? Array.Empty<string>());
  }


  [HttpPost("calculate")]
  [HttpPost("calculate/{task}")]
  [ProducesResponseType(typeof(float[]), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(typeof(string), StatusCodes.Status422UnprocessableEntity)]
  public async Task<IActionResult> CalculateEmbeddings([FromBody] string text, string task = null, string model = null,
    CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(text))
      return BadRequest("Text cannot be empty.");

    var result = await hikyaku.Send(new Core.Commands.CalculateEmbeddings { Sentence = text, Task = task, Model = model }, cancellationToken);

    if (result.Length == 0)
      return UnprocessableEntity("Unable to generate embeddings. Input may exceed model token limits.");

    return Ok(result);
  }

  [HttpPost("calculate/multi")]
  [ProducesResponseType(typeof(Dictionary<string, float[]>), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
  public async Task<IActionResult> CalculateEmbeddingsMulti([FromBody] CalculateEmbeddingsMultiRequest request,
    CancellationToken cancellationToken)
  {
    if (request == null || string.IsNullOrWhiteSpace(request.Text))
      return BadRequest("Text cannot be empty.");
    if (request.Models == null || request.Models.Length == 0 || request.Models.Any(string.IsNullOrWhiteSpace))
      return BadRequest("At least one valid model is required.");

    var result = await hikyaku.Send(new Core.Commands.CalculateEmbeddingsMulti
    {
      Sentence = request.Text,
      Task = request.Task,
      Models = request.Models
    }, cancellationToken);
    return Ok(result);
  }

  [HttpPost("passages")]
  [ProducesResponseType(typeof(Core.Dto.PassageEmbedding[]), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
  public async Task<IActionResult> CalculatePassageEmbeddings(
    [FromBody] CalculatePassageEmbeddingsRequest request,
    CancellationToken cancellationToken)
  {
    if (request == null || string.IsNullOrWhiteSpace(request.Text))
      return BadRequest("Text cannot be empty.");
    var size = request.PassageTokenSize ?? passageOptions.Value.DefaultTokenSize;
    var overlap = request.PassageOverlapSize ?? passageOptions.Value.DefaultOverlapSize;
    if (size <= 0 || size > passageOptions.Value.MaxTokenSize)
      return BadRequest($"PassageTokenSize must be between 1 and {passageOptions.Value.MaxTokenSize}.");
    if (overlap < 0 || overlap > passageOptions.Value.MaxOverlapSize || overlap >= size)
      return BadRequest($"PassageOverlapSize must be non-negative, lower than PassageTokenSize and not exceed {passageOptions.Value.MaxOverlapSize}.");

    var result = await hikyaku.Send(new Core.Commands.CalculatePassageEmbeddings
    {
      Model = request.Model,
      Task = request.Task,
      Sentence = request.Text,
      PassageTokenSize = size,
      PassageOverlapSize = overlap
    }, cancellationToken);

    return Ok(result);
  }

  [HttpPost("passages/count")]
  [ProducesResponseType(typeof(PassageSplitCountResult), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
  public async Task<IActionResult> CalculatePassageSplitCount(
    [FromBody] CalculatePassageSplitCountRequest request,
    CancellationToken cancellationToken)
  {
    if (request == null || string.IsNullOrWhiteSpace(request.Text))
      return BadRequest("Text cannot be empty.");
    var size = request.PassageTokenSize ?? passageOptions.Value.DefaultTokenSize;
    var overlap = request.PassageOverlapSize ?? passageOptions.Value.DefaultOverlapSize;
    if (size <= 0 || size > passageOptions.Value.MaxTokenSize)
      return BadRequest($"PassageTokenSize must be between 1 and {passageOptions.Value.MaxTokenSize}.");
    if (overlap < 0 || overlap > passageOptions.Value.MaxOverlapSize || overlap >= size)
      return BadRequest($"PassageOverlapSize must be non-negative, lower than PassageTokenSize and not exceed {passageOptions.Value.MaxOverlapSize}.");

    var count = await hikyaku.Send(new Core.Commands.CalculatePassageSplitCount
    {
      Model = request.Model,
      Sentence = request.Text,
      PassageTokenSize = size,
      PassageOverlapSize = overlap
    }, cancellationToken);

    return Ok(new PassageSplitCountResult { Count = count });
  }

  [HttpPost("calculate-image")]
  [Consumes("application/json")]
  [ProducesResponseType(typeof(float[]), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(typeof(string), StatusCodes.Status422UnprocessableEntity)]
  public async Task<IActionResult> CalculateImageEmbedding([FromBody] byte[] imageBytes)
  {
    if (imageBytes is null || imageBytes.Length == 0)
      return BadRequest("Image data cannot be empty.");

    var result = await hikyaku.Send(new Core.Commands.CalculateImageEmbedding { ImageBytes = imageBytes });

    if (result.Length == 0)
      return UnprocessableEntity("Unable to generate image embedding.");

    return Ok(result);
  }

  [HttpPost("calculate-image")]
  [Consumes("multipart/form-data")]
  [ProducesResponseType(typeof(float[]), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(typeof(string), StatusCodes.Status422UnprocessableEntity)]
  public async Task<IActionResult> CalculateImageEmbeddingUpload([FromForm] IFormFile file)
  {
    if (file is null || file.Length == 0)
      return BadRequest("No file uploaded.");

    using var stream = new MemoryStream();
    await file.CopyToAsync(stream);

    var result = await hikyaku.Send(new Core.Commands.CalculateImageEmbedding { ImageBytes = stream.ToArray() });

    if (result.Length == 0)
      return UnprocessableEntity("Unable to generate image embedding.");

    return Ok(result);
  }

  [HttpPost("calculate-image/batch")]
  [Consumes("application/json")]
  [ProducesResponseType(typeof(float[][]), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
  public async Task<IActionResult> CalculateImageEmbeddings([FromBody] byte[][] images)
  {
    if (images is null || images.Length == 0)
      return BadRequest("Image data cannot be empty.");

    for (var i = 0; i < images.Length; i++)
    {
      if (images[i] is null || images[i].Length == 0)
        return BadRequest($"Image at index {i} is empty.");
    }

    var result = await hikyaku.Send(new Core.Commands.CalculateImageEmbeddingBatch { Images = images });

    return Ok(result);
  }

  [HttpPost("calculate-image/batch")]
  [Consumes("multipart/form-data")]
  [ProducesResponseType(typeof(float[][]), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
  public async Task<IActionResult> CalculateImageEmbeddingsUpload([FromForm] List<IFormFile> files)
  {
    if (files is null || files.Count == 0)
      return BadRequest("No files uploaded.");

    var images = new byte[files.Count][];
    for (var i = 0; i < files.Count; i++)
    {
      if (files[i] is null || files[i].Length == 0)
        return BadRequest($"File at index {i} is empty.");

      using var stream = new MemoryStream();
      await files[i].CopyToAsync(stream);
      images[i] = stream.ToArray();
    }

    var result = await hikyaku.Send(new Core.Commands.CalculateImageEmbeddingBatch { Images = images });

    return Ok(result);
  }

  [HttpPost("calculate-image/tiles")]
  [Consumes("application/json")]
  [ProducesResponseType(typeof(float[][]), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(typeof(string), StatusCodes.Status422UnprocessableEntity)]
  public async Task<IActionResult> CalculateImageTileEmbeddings([FromBody] byte[] imageBytes)
  {
    if (imageBytes is null || imageBytes.Length == 0)
      return BadRequest("Image data cannot be empty.");

    var result = await hikyaku.Send(new Core.Commands.CalculateImageTileEmbeddings { ImageBytes = imageBytes });

    if (result.Length == 0)
      return UnprocessableEntity("Unable to generate image embedding.");

    return Ok(result);
  }

  [HttpPost("calculate-image/tiles")]
  [Consumes("multipart/form-data")]
  [ProducesResponseType(typeof(float[][]), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(typeof(string), StatusCodes.Status422UnprocessableEntity)]
  public async Task<IActionResult> CalculateImageTileEmbeddingsUpload([FromForm] IFormFile file)
  {
    if (file is null || file.Length == 0)
      return BadRequest("No file uploaded.");

    using var stream = new MemoryStream();
    await file.CopyToAsync(stream);

    var result = await hikyaku.Send(new Core.Commands.CalculateImageTileEmbeddings { ImageBytes = stream.ToArray() });

    if (result.Length == 0)
      return UnprocessableEntity("Unable to generate image embedding.");

    return Ok(result);
  }
}

public class CalculateEmbeddingsMultiRequest
{
  public string Text { get; set; }
  public string Task { get; set; }
  public string[] Models { get; set; }
}

public class CalculatePassageEmbeddingsRequest
{
  public string Text { get; set; }
  public string Model { get; set; }
  public string Task { get; set; }
  public int? PassageTokenSize { get; set; }
  public int? PassageOverlapSize { get; set; }
}

public class CalculatePassageSplitCountRequest
{
  public string Text { get; set; }
  public string Model { get; set; }
  public int? PassageTokenSize { get; set; }
  public int? PassageOverlapSize { get; set; }
}

public class PassageSplitCountResult
{
  public int Count { get; set; }
}
