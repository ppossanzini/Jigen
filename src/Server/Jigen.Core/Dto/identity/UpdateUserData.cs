namespace Jigen.Core.Dto.identity;

public class UpdateUserData
{
  public string UserName { get; set; }
  public string Password { get; set; }
  public string[] Roles { get; set; }
}