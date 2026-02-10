using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SharedTools;

namespace SharedTools
{
  public class Loader
  {
    private Loader()
    {
    }

    private readonly static Loader _current = new Loader();

    public static Loader Current
    {
      get { return _current; }
    }

    public List<string> Directories { get; private set; } = new List<string>();

    public IEnumerable<IModule> Modules { get; private set; }
    public IEnumerable<Assembly> Assemblies { get; private set; }

    public void Compose()
    {
      // Catalogs does not exists in Dotnet Core, so you need to manage your own.
      var assemblies = new List<Assembly>() { Assembly.GetEntryAssembly() };
      var modules = new List<IModule>();

      // All dlls in given directories
      foreach (var dir in this.Directories)
      {
        var files = Directory.GetFiles(dir, "*.dll", SearchOption.AllDirectories);
        foreach (var f in files)
        {
          try
          {
            ModuleLoader loadContext = new ModuleLoader(dir);

            var s = loadContext.LoadFromAssemblyName(new AssemblyName(Path.GetFileNameWithoutExtension(f)));
            if (s.GetTypes().Where(p => typeof(IModule).IsAssignableFrom(p)).Any()) assemblies.Add(s);
          }
          catch (Exception ex)
          {
            Console.WriteLine(ex.Message);
            Console.WriteLine(f);
          }
        }
      }

      foreach (var a in assemblies)
      {
        foreach (var m in a.GetTypes().Where(p => typeof(IModule).IsAssignableFrom(p)))
        {
          try
          {
            modules.Add(Activator.CreateInstance(m) as IModule);
          }
          catch (Exception ex)
          {
            Console.WriteLine(ex.Message);
            Console.WriteLine(m);
          }
        }
      }

      this.Assemblies = assemblies;
      this.Modules = modules;
    }

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostingEnvironment)
    {
      foreach (var m in this.Modules)
        m.ConfigureServices(services, configuration, hostingEnvironment);
    }

    public void AddModules(IServiceProvider services)
    {
      foreach (var m in this.Modules)
        m.OnStartup(services);

      foreach (var module in this.Modules)
        module.PostStartup(services);
    }
  }
}

public class ModuleLoader : AssemblyLoadContext
{
  private AssemblyDependencyResolver _resolver;

  public ModuleLoader(string pluginPath)
  {
    _resolver = new AssemblyDependencyResolver(pluginPath);
  }

  protected override Assembly Load(AssemblyName assemblyName)
  {
    string assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
    if (assemblyPath != null)
    {
      return LoadFromAssemblyPath(assemblyPath);
    }

    return null;
  }

  protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
  {
    string libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
    if (libraryPath != null)
    {
      return LoadUnmanagedDllFromPath(libraryPath);
    }

    return IntPtr.Zero;
  }
}