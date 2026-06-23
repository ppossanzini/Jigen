using Jigen.SemanticTools;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jigen.API;

[ApiController]
[Route("embeddings")]
public class EmbeddingsController(IEmbeddingGenerator generator) : ControllerBase
{
  [HttpPost("calculate")]
  [ProducesResponseType(typeof(float[]), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(typeof(string), StatusCodes.Status422UnprocessableEntity)]
  public IActionResult CalculateEmbeddings([FromBody] string text)
  {
    if (string.IsNullOrWhiteSpace(text))
      return BadRequest("Text cannot be empty.");

    var result = generator.GenerateEmbedding(text);

    if (result.Length == 0)
      return UnprocessableEntity("Unable to generate embeddings. Input may exceed model token limits.");

    return Ok(result);
  }
}