namespace Jigen.Identity.Core.Dto;

public class UpdateUserData
{
  public string UserName { get; set; }
  public string Password { get; set; }
  public string[] Roles { get; set; }
}