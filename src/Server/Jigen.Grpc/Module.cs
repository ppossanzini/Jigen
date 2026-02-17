using System.Composition;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SharedTools;

namespace Jigen.Grpc;

[Export(typeof(IModule))]
public class Module : IModule
{
  const string JigenGrpcCorsDefaultPolicy = "JigenGrpcCorsDefaultPolicy";

  public void ConfigureServices(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostingEnvironment)
  {
    services.AddGrpc();
    //.AddServiceOptions<Server>(c => {});

    services.AddCors(o =>
      o.AddPolicy(JigenGrpcCorsDefaultPolicy, b => b
        .AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()
        .WithExposedHeaders("Grpc-Status", "Grpc-Message", "Grpc-Encoding", "Grpc-Accept-Encoding")
      ));
  }

  public void OnStartup(IServiceProvider services)
  {
  }

  public void UseEndpoints(IEndpointRouteBuilder endpoints)
  {
    endpoints
      .MapGrpcService<Server>()
      .EnableGrpcWeb()
      .RequireCors(JigenGrpcCorsDefaultPolicy);
  }

  public void PostStartup(IServiceProvider services)
  {
  }
}