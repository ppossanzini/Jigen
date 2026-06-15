namespace Jigen.Core.Dto.identity;

public class CreateUserData
{
  public string UserName { get; set; }
  public string Password { get; set; }
  public string[] Roles { get; set; }
}