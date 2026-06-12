using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
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

      builder.WebHost.ConfigureKestrel(options =>
      {
        options.ListenAnyIP(3223, listenOptions =>
        {
          listenOptions.Protocols = HttpProtocols.Http2;
        });

        options.ListenAnyIP(13223, listenOptions =>
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
      
      app.UseStaticFiles();
    }

    private static X509Certificate2? ResolveHttpsCertificate(IConfiguration configuration)
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
