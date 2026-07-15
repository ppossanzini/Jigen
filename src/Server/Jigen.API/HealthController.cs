using Jigen.DataStructures;
using Hikyaku;
using Jigen.Core.Dto.collections;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jigen.API;


[Route("~/healtz")]
[ApiController]
public class HealthController() : ControllerBase
{

  [HttpGet, HttpHead, AllowAnonymous]
  public IActionResult Get()
  {
    return Ok(new { status = "healthy" });
  }

}