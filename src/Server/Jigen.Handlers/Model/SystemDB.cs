using System.Runtime.CompilerServices;
using Jigen.DataStructures;
using MessagePack;
using MessagePack.Resolvers;

namespace Jigen.Handlers.Model;

public class SystemDB : Store
{
  public DocumentCollection<SystemInfo> System { get; set; }

  const string SYSTEM = "SYSTEM";
  public const string BASEINFO = "baseinfo";

  public SystemDB(StoreOptions options) : base(options)
  {
    System = new DocumentCollection<SystemInfo>(this, new DocumentCollectionOptions<SystemInfo>()
    {
      Name = SYSTEM
    });

    if (!this.System.ContainsKey(BASEINFO))
      this.System.Add(BASEINFO, new SystemInfo() { Databases = [] });
    
    this.SaveChangesAsync().GetAwaiter().GetResult();
  }
}