using System.Reflection;
using Scalar.AspNetCore;
using SharedTools;

namespace Jigen
{
  public class Program
  {
    public static void Main(string[] args)
    {
      var builder = WebApplication.CreateBuilder(args);

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
      foreach (var m in Loader.Current.Modules)
        m.UseEndpoints(app);
      app.UseStaticFiles();
    }
  }
}