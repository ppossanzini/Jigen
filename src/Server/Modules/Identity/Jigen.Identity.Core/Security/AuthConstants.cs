namespace Jigen.Identity.Core.Security;

public static class AuthConstants
{
  public static class ClaimTypes
  {
    public const string Permission = "permissions";
  }

  public static class Claims
  {
    public const string DatabaseCreate = "database.create";
    public const string DatabaseDelete = "database.delete";
    public const string DatabaseAdmin = "database.admin";

    public const string CollectionCreate = "collection.create";
    public const string CollectionRead = "collection.read";
    public const string CollectionUpdate = "collection.update";

    public const string UserCreate = "user.create";
    public const string UserUpdate = "user.update";
  }

  public static class Roles
  {
    public const string DatabaseAdmin = "DatabaseAdmin";
    public const string SecurityAdmin = "SecurityAdmin";
  }
}
