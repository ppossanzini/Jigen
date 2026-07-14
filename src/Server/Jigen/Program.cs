using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Hikyaku.Kaido;
using Jigen.Identity.Core.Security;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Scalar.AspNetCore;
using SharedTools;

namespace Jigen
{
  public class Program
  {
    public static void Main(string[] args)
    {
      var builder = WebApplication.CreateBuilder(args);

      var httpsCertificate = ResolveHttpsCertificate(builder.Configuration);
      var grpcPort = builder.Configuration.GetValue("JigenServer:GrpcPort", 3223);
      var httpPort = builder.Configuration.GetValue("JigenServer:HttpPort", 13223);

      builder.WebHost.ConfigureKestrel(options =>
      {
        options.ListenAnyIP(grpcPort, listenOptions => { listenOptions.Protocols = HttpProtocols.Http2; });

        options.ListenAnyIP(httpPort, listenOptions =>
        {
          listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
          if (httpsCertificate != null)
            listenOptions.UseHttps(httpsCertificate);
        });
      });

      Loader.Current.Directories.Add(Directory.GetCurrentDirectory());
      Loader.Current.Compose();

      ConfigureServices(builder.Services,
        builder.Configuration,
        builder.Environment);

      var app = builder.Build();
      Configure(app, builder.Environment, builder.Configuration);


      app.Logger.LogInformation("Jigen server started on {GrpcPort} port for GRPC connections", grpcPort);
      app.Logger.LogInformation("Jigen server started on {HttpPort} port for HTTP connections", httpPort);
      app.Run();
    }


    public static void ConfigureServices(IServiceCollection services, ConfigurationManager configuration,
      IHostEnvironment environment)
    {
      services.AddCors(options =>
      {
        options.AddDefaultPolicy(opt =>
          opt.AllowAnyMethod().AllowAnyHeader().SetIsOriginAllowed(s => true).AllowCredentials());
      });

      Loader.Current.ConfigureServices(services, configuration, environment);

      services.AddControllers().AddNewtonsoftJson(options => options.SerializerSettings.ConfigureDefaults());

      services.AddHikyaku(cfg => { cfg.RegisterServicesFromAssemblies(Loader.Current.Assemblies.ToArray()); });
      services.AddMapZilla(Loader.Current.Assemblies);

      if (configuration.GetValue<bool>("Kaido:Enabled"))
      {
        services.AddKaido(options =>
        {
          options.Behaviour = HikyakuBehaviourEnum.ImplicitRemote;
          options.InferLocalRequests(Loader.Current.Assemblies);
          options.InferLocalNotifications(Loader.Current.Assemblies);
        });

        services.AddHikyakuRabbitMQMessageDispatcher(o =>
        {
          configuration.GetSection("Kaido:RabbitMQ").Bind(o);
          o.ClientName = Assembly.GetExecutingAssembly().FullName;
        }).AddRabbitMQRequestManager();
      }
      services.AddOpenApi();
    }

    public static void Configure(WebApplication app, IHostEnvironment environment, IConfiguration configuration)
    {
      if (environment.IsDevelopment())
        app.UseDeveloperExceptionPage();

      app.UseRouting();
      app.UseCors();

      app.UseResponseCaching();

      app.UseAuthentication();
      app.UseCurrentUserAccessor();
      app.UseAuthorization();

      app.MapOpenApi();

      Action<ScalarOptions> configureOptions = options =>
        options
          .WithLayout(ScalarLayout.Modern)
          .WithSidebar(true).WithClientButton(false)
          .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);

      app.MapScalarApiReference(configureOptions);

      Loader.Current.AddModules(app.Services);

      app.MapControllers();
      foreach (var m in Loader.Current.Modules.DistinctBy(x => x.GetType()))
        m.UseEndpoints(app);

      app.UseDefaultFiles();
      app.UseStaticFiles();

      string[] apipaths =
      [
        "/api", "/scalar", "/connect", "/identity"
      ];

      app.MapFallback(async context =>
      {
        var path = context.Request.Path;
        if (apipaths.Any(p => path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase)))
        {
          context.Response.StatusCode = StatusCodes.Status404NotFound;
          return;
        }

        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.SendFileAsync(Path.Combine(app.Environment.WebRootPath, "index.html"));
      });
    }

    private static X509Certificate2 ResolveHttpsCertificate(IConfiguration configuration)
    {
      var mode = configuration["JigenServer:Https:Mode"];
      if (string.Equals(mode, "Random", StringComparison.OrdinalIgnoreCase))
        return CreateSelfSignedCertificate();

      if (string.Equals(mode, "FromFile", StringComparison.OrdinalIgnoreCase))
      {
        var path = configuration["JigenServer:Https:CertificatePath"];
        if (string.IsNullOrWhiteSpace(path))
          throw new InvalidOperationException("JigenServer:Https:CertificatePath is required when Mode is FromFile.");

        var password = configuration["JigenServer:Https:CertificatePassword"];
        return string.IsNullOrEmpty(password)
          ? new X509Certificate2(path)
          : new X509Certificate2(path, password);
      }

      return null;
    }

    private static X509Certificate2 CreateSelfSignedCertificate()
    {
      using var rsa = RSA.Create(2048);
      var request = new CertificateRequest(
        "CN=Jigen",
        rsa,
        HashAlgorithmName.SHA256,
        RSASignaturePadding.Pkcs1);

      request.CertificateExtensions.Add(
        new X509KeyUsageExtension(
          X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
          critical: false));

      request.CertificateExtensions.Add(
        new X509EnhancedKeyUsageExtension(
          new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") },
          critical: false));

      var sanBuilder = new SubjectAlternativeNameBuilder();
      sanBuilder.AddDnsName("localhost");
      sanBuilder.AddIpAddress(System.Net.IPAddress.Loopback);
      request.CertificateExtensions.Add(sanBuilder.Build());

      var notBefore = DateTimeOffset.UtcNow.AddMinutes(-5);
      var notAfter = notBefore.AddYears(1);
      return request.CreateSelfSigned(notBefore, notAfter);
    }
  }
}