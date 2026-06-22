using System.Security.Claims;
using Jigen.Handlers.Model;
using Jigen.Identity.Core.Security;

namespace Jigen.Handlers.CQRS;

public sealed class DatabaseOwnershipGuard(
  SystemDB master,
  ICurrentUserAccessor currentUserAccessor)
{
  public SystemInfo GetNormalizedSystemInfo()
  {
    var info = master.System[SystemDB.BASEINFO];
    return NormalizeInfo(info);
  }

  public IEnumerable<string> GetReadableDatabases(SystemInfo info)
  {
    return info.Databases.Where(database => CanReadDatabase(info, database));
  }

  public void EnsureCanReadDatabase(string database)
  {
    var info = GetNormalizedSystemInfo();
    if (!info.Databases.Contains(database, StringComparer.OrdinalIgnoreCase))
      throw new ArgumentException("Database not found");

    if (!CanReadDatabase(info, database))
      throw new ArgumentException("Database not found");
  }

  public bool CanReadDatabase(SystemInfo info, string database)
  {
    if (IsDatabaseAdmin())
      return true;

    var userId = GetCurrentUserId();
    var userName = GetCurrentUserName();
    if (string.IsNullOrWhiteSpace(userId) && string.IsNullOrWhiteSpace(userName))
      return false;

    var databaseInfo = info.DatabaseInfos.FirstOrDefault(i =>
      string.Equals(i.Database, database, StringComparison.OrdinalIgnoreCase));

    var users = databaseInfo?.Users ?? [];
    if (users.Count == 0)
      return false;

    return users.Any(u =>
      (!string.IsNullOrWhiteSpace(userId) && string.Equals(u.UserId, userId, StringComparison.OrdinalIgnoreCase)) ||
      (!string.IsNullOrWhiteSpace(userName) && string.Equals(u.UserName, userName, StringComparison.OrdinalIgnoreCase)));
  }

  private bool IsDatabaseAdmin()
  {
    var user = currentUserAccessor.User;
    if (user?.Identity?.IsAuthenticated != true)
      return false;

    return user.Claims.Any(c =>
             c.Type == ClaimTypes.Role &&
             string.Equals(c.Value, AuthConstants.Roles.DatabaseAdmin, StringComparison.OrdinalIgnoreCase)) ||
           user.Claims.Any(c =>
             string.Equals(c.Type, AuthConstants.ClaimTypes.Permission, StringComparison.OrdinalIgnoreCase) &&
             string.Equals(c.Value, AuthConstants.Claims.DatabaseAdmin, StringComparison.OrdinalIgnoreCase));
  }

  public DatabaseUserAssociation GetCurrentUserAssociation()
  {
    var user = currentUserAccessor.User;
    if (user?.Identity?.IsAuthenticated != true)
      return null;

    var userId = GetCurrentUserId();
    var userName = GetCurrentUserName();
    if (string.IsNullOrWhiteSpace(userId) && string.IsNullOrWhiteSpace(userName))
      return null;

    return new DatabaseUserAssociation
    {
      UserId = userId,
      UserName = userName
    };
  }

  private string GetCurrentUserId()
  {
    return currentUserAccessor.User.FindFirst("sub")?.Value
      ?? currentUserAccessor.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
  }

  private string GetCurrentUserName()
  {
    return currentUserAccessor.User.Identity?.Name
      ?? currentUserAccessor.User.FindFirst("preferred_username")?.Value
      ?? currentUserAccessor.User.FindFirst(ClaimTypes.Name)?.Value;
  }

  private static SystemInfo NormalizeInfo(SystemInfo info)
  {
    info.Databases ??= [];
    info.DatabaseInfos ??= [];

    foreach (var db in info.Databases)
    {
      var exists = info.DatabaseInfos.Any(i => string.Equals(i.Database, db, StringComparison.OrdinalIgnoreCase));
      if (!exists)
      {
        info.DatabaseInfos.Add(new DatabaseSystemInfo
        {
          Database = db,
          Users = []
        });
      }
    }

    foreach (var databaseInfo in info.DatabaseInfos)
      databaseInfo.Users ??= [];

    return info;
  }
}