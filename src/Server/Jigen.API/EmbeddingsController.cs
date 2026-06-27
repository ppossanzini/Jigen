using Hikyaku;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace Jigen.TextEmbedding.Api;

[ApiController]
[Route("~/api/embeddings")]
public class EmbeddingsController(IHikyaku hikyaku, IConfiguration configuration) : ControllerBase
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
  public async Task<IActionResult> CalculateEmbeddings([FromBody] string text, string task = null)
  {
    if (string.IsNullOrWhiteSpace(text))
      return BadRequest("Text cannot be empty.");

    var result = await hikyaku.Send(new Jigen.TextEmbedding.Core.Commands.CalculateEmbeddings() { Sentence = text, Task = task });

    if (result.Length == 0)
      return UnprocessableEntity("Unable to generate embeddings. Input may exceed model token limits.");

    return Ok(result);
  }
}