using Jigen.Identity;
using Jigen.Identity.Core.Command;
using Jigen.Identity.Core.Dto;
using Jigen.Identity.Handlers;
using Jigen.Identity.Handlers.CQRS;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OpenIddict.Abstractions;

namespace Jigen.IdentityTests;

public class IdentityCommandHandlersUserRoleTests
{
  [Fact]
  public async Task CreateRole_creates_new_role_successfully()
  {
    await using var fixture = await TestFixture.CreateAsync();
    using var scope = fixture.Provider.CreateScope();

    var handler = CreateHandler(scope.ServiceProvider);
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

    var result = await handler.Handle(new CreateRole
    {
      Data = new CreateRoleData
      {
        Name = "SecurityAdmin"
      }
    }, CancellationToken.None);

    Assert.Equal(IdentityActionStatus.Success, result.Status);

    var createdRole = await roleManager.FindByNameAsync("SecurityAdmin");
    Assert.NotNull(createdRole);
  }

  [Fact]
  public async Task CreateUser_creates_new_user_successfully()
  {
    await using var fixture = await TestFixture.CreateAsync();
    using var scope = fixture.Provider.CreateScope();

    var handler = CreateHandler(scope.ServiceProvider);
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

    var result = await handler.Handle(new CreateUser
    {
      Data = new CreateUserData
      {
        UserName = "mario",
        Password = "Str0ng!Pass123",
        Roles = Array.Empty<string>()
      }
    }, CancellationToken.None);

    Assert.Equal(IdentityActionStatus.Success, result.Status);

    var createdUser = await userManager.FindByNameAsync("mario");
    Assert.NotNull(createdUser);
  }

  [Fact]
  public async Task UpdateUser_assigns_role_to_existing_user_successfully()
  {
    await using var fixture = await TestFixture.CreateAsync();
    using var scope = fixture.Provider.CreateScope();

    var handler = CreateHandler(scope.ServiceProvider);
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

    var createRoleResult = await handler.Handle(new CreateRole
    {
      Data = new CreateRoleData
      {
        Name = "Operator"
      }
    }, CancellationToken.None);
    Assert.Equal(IdentityActionStatus.Success, createRoleResult.Status);

    var createUserResult = await handler.Handle(new CreateUser
    {
      Data = new CreateUserData
      {
        UserName = "luigi",
        Password = "Str0ng!Pass123",
        Roles = Array.Empty<string>()
      }
    }, CancellationToken.None);
    Assert.Equal(IdentityActionStatus.Success, createUserResult.Status);

    var user = await userManager.FindByNameAsync("luigi");
    Assert.NotNull(user);

    var associationResult = await handler.Handle(new UpdateUser
    {
      Id = user.Id,
      Data = new UpdateUserData
      {
        UserName = "luigi",
        Password = string.Empty,
        Roles = new[] { "Operator" }
      }
    }, CancellationToken.None);

    Assert.Equal(IdentityActionStatus.Success, associationResult.Status);

    var roles = await userManager.GetRolesAsync(user);
    Assert.Contains("Operator", roles);
  }

  private static IdentityCommandHandlers CreateHandler(IServiceProvider provider)
  {
    var signInManager = provider.GetRequiredService<SignInManager<IdentityUser>>();
    var userManager = provider.GetRequiredService<UserManager<IdentityUser>>();
    var roleManager = provider.GetRequiredService<RoleManager<IdentityRole>>();

    var applicationManager = new Mock<IOpenIddictApplicationManager>();

    return new IdentityCommandHandlers(signInManager, userManager, roleManager, applicationManager.Object);
  }

  private sealed class TestFixture : IAsyncDisposable
  {
    private TestFixture(SqliteConnection connection, ServiceProvider provider)
    {
      Connection = connection;
      Provider = provider;
    }

    public SqliteConnection Connection { get; }
    public ServiceProvider Provider { get; }

    public static async Task<TestFixture> CreateAsync()
    {
      var connection = new SqliteConnection("DataSource=:memory:");
      await connection.OpenAsync();

      var services = new ServiceCollection();
      services.AddLogging();

      services.AddDbContext<JigenIdentityDbContext>(options =>
      {
        options.UseSqlite(connection);
        options.UseOpenIddict();
      });

      services.AddIdentity<IdentityUser, IdentityRole>()
        .AddEntityFrameworkStores<JigenIdentityDbContext>()
        .AddDefaultTokenProviders();

      services.AddAuthentication();
      services.AddAuthorization();

      var provider = services.BuildServiceProvider();

      using (var scope = provider.CreateScope())
      {
        var db = scope.ServiceProvider.GetRequiredService<JigenIdentityDbContext>();
        await db.Database.EnsureCreatedAsync();
      }

      return new TestFixture(connection, provider);
    }

    public async ValueTask DisposeAsync()
    {
      await Provider.DisposeAsync();
      await Connection.DisposeAsync();
    }
  }
}
