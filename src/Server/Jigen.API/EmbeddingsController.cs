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
  public IActionResult CalculateEmbeddings([FromBody] string text)
  {
    var result = generator.GenerateEmbedding(text);
    return Ok(result);
  }
}