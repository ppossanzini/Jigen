namespace Jigen.Handlers.Model;

public class SystemInfo
{
  public List<string> Databases { get; set; }
  public List<DatabaseSystemInfo> DatabaseInfos { get; set; }
}

public class DatabaseSystemInfo
{
  public string Database { get; set; }
  public DateTime? CreatedAtUtc { get; set; }
  public List<DatabaseUserAssociation> Users { get; set; }
}

public class DatabaseUserAssociation
{
  public string UserId { get; set; }
  public string UserName { get; set; }
}