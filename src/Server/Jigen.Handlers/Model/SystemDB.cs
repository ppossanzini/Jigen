using System.Runtime.CompilerServices;
using Jigen.DataStructures;
using MessagePack;
using MessagePack.Resolvers;

namespace Jigen.Handlers.Model;

public class SystemDB : Store
{
  readonly MessagePackSerializerOptions _serializerOptions = MessagePackSerializerOptions.Standard.WithResolver(ContractlessStandardResolver.Instance);

  public VectorCollection<SystemInfo> System { get; set; }

  public const string SYSTEM = "SYSTEM";
  public const string BASEINFO = "baseinfo";

  public SystemDB(StoreOptions options) : base(options)
  {
    System = new VectorCollection<SystemInfo>(this, new VectorCollectionOptions<SystemInfo>()
    {
      Name = SYSTEM, Dimensions = 0,
      Serialize = i => MessagePackSerializer.Serialize(i, _serializerOptions),
      Deserialize = b => MessagePackSerializer.Deserialize<SystemInfo>(b, _serializerOptions)
    });
    
    if (!this.System.ContainsKey(BASEINFO))
      this.System.Add(BASEINFO, new VectorEntry<SystemInfo>()
      {
        Key = BASEINFO,
        Content = new SystemInfo()
        {
          Databases = []
        }
      });
  }
}