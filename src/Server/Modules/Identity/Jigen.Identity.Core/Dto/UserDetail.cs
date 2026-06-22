namespace Jigen.Identity.Core.Dto;

public class UserDetail
{
  public string Id { get; set; }
  public string UserName { get; set; }
  public string[] Roles { get; set; } = Array.Empty<string>();
  public string[] Permissions { get; set; } = Array.Empty<string>();
}
