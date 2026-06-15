using System.Security.Cryptography;
using Hikyaku;
using Jigen.Identity.Core.Dto;
using Microsoft.AspNetCore.Identity;
using OpenIddict.Abstractions;

namespace Jigen.Identity.Handlers.CQRS;

public class IdentityCommandHandlers(
  SignInManager<IdentityUser> signInManager,
  UserManager<IdentityUser> userManager,
  RoleManager<IdentityRole> roleManager,
  IOpenIddictApplicationManager applicationManager) :
  IRequestHandler<Core.Command.Login, IdentityCommandResult>,
  IRequestHandler<Core.Command.Logout>,
  IRequestHandler<Core.Command.CreateClient, CreateClientResult>,
  IRequestHandler<Core.Command.ExchangeToken, ExchangeTokenResult>,
  IRequestHandler<Core.Command.CreateUser, IdentityCommandResult>,
  IRequestHandler<Core.Command.UpdateUser, IdentityCommandResult>,
  IRequestHandler<Core.Command.DeleteUser, IdentityCommandResult>,
  IRequestHandler<Core.Command.CreateRole, IdentityCommandResult>,
  IRequestHandler<Core.Command.UpdateRole, IdentityCommandResult>,
  IRequestHandler<Core.Command.DeleteRole, IdentityCommandResult>
{
  public async Task<IdentityCommandResult> Handle(Core.Command.Login request, CancellationToken cancellationToken)
  {
    if (request?.Data == null ||
        string.IsNullOrWhiteSpace(request.Data.UserName) ||
        string.IsNullOrWhiteSpace(request.Data.Password))
    {
      return new IdentityCommandResult
      {
        Status = IdentityActionStatus.InvalidRequest,
        Message = "Username and password are required."
      };
    }

    var result = await signInManager.PasswordSignInAsync(request.Data.UserName, request.Data.Password, false, false);
    if (!result.Succeeded)
    {
      return new IdentityCommandResult
      {
        Status = IdentityActionStatus.Unauthorized
      };
    }

    return new IdentityCommandResult
    {
      Status = IdentityActionStatus.Success
    };
  }

  public Task Handle(Core.Command.Logout request, CancellationToken cancellationToken)
  {
    return signInManager.SignOutAsync();
  }

  public async Task<CreateClientResult> Handle(Core.Command.CreateClient request, CancellationToken cancellationToken)
  {
    if (request?.Data == null)
    {
      return new CreateClientResult
      {
        Status = IdentityActionStatus.InvalidRequest,
        Message = "Request body is required."
      };
    }

    var clientId = string.IsNullOrWhiteSpace(request.Data.ClientId)
      ? GenerateSecret(24)
      : request.Data.ClientId;

    var clientSecret = GenerateSecret(48);

    if (await applicationManager.FindByClientIdAsync(clientId, cancellationToken) != null)
    {
      return new CreateClientResult
      {
        Status = IdentityActionStatus.Conflict,
        Message = "ClientId already exists."
      };
    }

    if (request.Data.AllowAuthorizationCode &&
        (request.Data.RedirectUris == null || request.Data.RedirectUris.Length == 0))
    {
      return new CreateClientResult
      {
        Status = IdentityActionStatus.InvalidRequest,
        Message = "RedirectUris are required when Authorization Code flow is enabled."
      };
    }

    var descriptor = new OpenIddictApplicationDescriptor
    {
      ClientId = clientId,
      ClientSecret = clientSecret,
      DisplayName = string.IsNullOrWhiteSpace(request.Data.DisplayName) ? clientId : request.Data.DisplayName
    };

    descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Token);
    descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Introspection);
    descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Revocation);
    descriptor.Permissions.Add("endpoints:userinfo");

    if (request.Data.AllowAuthorizationCode)
    {
      descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Authorization);
      descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode);
      descriptor.Permissions.Add(OpenIddictConstants.Permissions.ResponseTypes.Code);
    }

    if (request.Data.AllowClientCredentials)
      descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.ClientCredentials);

    if (request.Data.AllowRefreshToken)
      descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.RefreshToken);

    var scopes = request.Data.Scopes?.Length > 0
      ? request.Data.Scopes
      : new[] { "jigen_api", "openid" };

    foreach (var scope in scopes)
      descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + scope);

    if (request.Data.RedirectUris != null)
    {
      foreach (var uri in request.Data.RedirectUris)
        if (Uri.TryCreate(uri, UriKind.Absolute, out var parsed))
          descriptor.RedirectUris.Add(parsed);
    }

    if (request.Data.PostLogoutRedirectUris != null)
    {
      foreach (var uri in request.Data.PostLogoutRedirectUris)
        if (Uri.TryCreate(uri, UriKind.Absolute, out var parsed))
          descriptor.PostLogoutRedirectUris.Add(parsed);
    }

    await applicationManager.CreateAsync(descriptor, cancellationToken);

    return new CreateClientResult
    {
      Status = IdentityActionStatus.Success,
      ClientId = clientId,
      ClientSecret = clientSecret
    };
  }

  public Task<ExchangeTokenResult> Handle(Core.Command.ExchangeToken request, CancellationToken cancellationToken)
  {
    if (request?.Data == null)
    {
      return Task.FromResult(new ExchangeTokenResult
      {
        Status = IdentityActionStatus.InvalidRequest,
        Message = "Token request data is required."
      });
    }

    if (request.Data.GrantType == OpenIddictConstants.GrantTypes.ClientCredentials)
    {
      if (string.IsNullOrWhiteSpace(request.Data.ClientId))
      {
        return Task.FromResult(new ExchangeTokenResult
        {
          Status = IdentityActionStatus.InvalidRequest,
          Message = "ClientId is required for client_credentials grant type."
        });
      }

      return Task.FromResult(new ExchangeTokenResult
      {
        Status = IdentityActionStatus.Success,
        Subject = request.Data.ClientId,
        ClientId = request.Data.ClientId,
        Scopes = request.Data.Scopes ?? Array.Empty<string>()
      });
    }

    if (request.Data.GrantType == OpenIddictConstants.GrantTypes.AuthorizationCode ||
        request.Data.GrantType == OpenIddictConstants.GrantTypes.RefreshToken)
    {
      return Task.FromResult(new ExchangeTokenResult
      {
        Status = IdentityActionStatus.Success,
        UseAuthenticatedPrincipal = true,
        Scopes = request.Data.Scopes ?? Array.Empty<string>()
      });
    }

    return Task.FromResult(new ExchangeTokenResult
    {
      Status = IdentityActionStatus.InvalidRequest,
      Message = "The specified grant type is not supported."
    });
  }

  public async Task<IdentityCommandResult> Handle(Core.Command.CreateUser request, CancellationToken cancellationToken)
  {
    if (request?.Data == null ||
        string.IsNullOrWhiteSpace(request.Data.UserName) ||
        string.IsNullOrWhiteSpace(request.Data.Password))
    {
      return new IdentityCommandResult
      {
        Status = IdentityActionStatus.InvalidRequest,
        Message = "UserName and Password are required."
      };
    }

    var existingUser = await userManager.FindByNameAsync(request.Data.UserName);
    if (existingUser != null)
    {
      return new IdentityCommandResult
      {
        Status = IdentityActionStatus.Conflict,
        Message = "UserName already exists."
      };
    }

    if (request.Data.Roles != null && request.Data.Roles.Length > 0)
    {
      var missingRoles = await FindMissingRolesAsync(request.Data.Roles);
      if (missingRoles.Length > 0)
      {
        return new IdentityCommandResult
        {
          Status = IdentityActionStatus.InvalidRequest,
          Message = $"Roles not found: {string.Join(", ", missingRoles)}"
        };
      }
    }

    var user = new IdentityUser
    {
      UserName = request.Data.UserName,
      Email = request.Data.UserName
    };

    var createResult = await userManager.CreateAsync(user, request.Data.Password);
    if (!createResult.Succeeded)
    {
      return new IdentityCommandResult
      {
        Status = IdentityActionStatus.InvalidRequest,
        Message = FormatErrors(createResult.Errors)
      };
    }

    if (request.Data.Roles != null && request.Data.Roles.Length > 0)
    {
      var roleResult = await userManager.AddToRolesAsync(user, request.Data.Roles);
      if (!roleResult.Succeeded)
      {
        return new IdentityCommandResult
        {
          Status = IdentityActionStatus.InvalidRequest,
          Message = FormatErrors(roleResult.Errors)
        };
      }
    }

    return new IdentityCommandResult
    {
      Status = IdentityActionStatus.Success
    };
  }

  public async Task<IdentityCommandResult> Handle(Core.Command.UpdateUser request, CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(request?.Id) || request.Data == null)
    {
      return new IdentityCommandResult
      {
        Status = IdentityActionStatus.InvalidRequest,
        Message = "User id and payload are required."
      };
    }

    var user = await userManager.FindByIdAsync(request.Id);
    if (user == null)
    {
      return new IdentityCommandResult
      {
        Status = IdentityActionStatus.NotFound,
        Message = "User not found."
      };
    }

    if (!string.IsNullOrWhiteSpace(request.Data.UserName))
    {
      var byName = await userManager.FindByNameAsync(request.Data.UserName);
      if (byName != null && byName.Id != user.Id)
      {
        return new IdentityCommandResult
        {
          Status = IdentityActionStatus.Conflict,
          Message = "UserName already exists."
        };
      }

      user.UserName = request.Data.UserName;
      user.Email = request.Data.UserName;
    }

    var updateResult = await userManager.UpdateAsync(user);
    if (!updateResult.Succeeded)
    {
      return new IdentityCommandResult
      {
        Status = IdentityActionStatus.InvalidRequest,
        Message = FormatErrors(updateResult.Errors)
      };
    }

    if (!string.IsNullOrWhiteSpace(request.Data.Password))
    {
      var removePasswordResult = await userManager.RemovePasswordAsync(user);
      if (!removePasswordResult.Succeeded &&
          !removePasswordResult.Errors.Any(e => e.Code == "UserPasswordNotFound"))
      {
        return new IdentityCommandResult
        {
          Status = IdentityActionStatus.InvalidRequest,
          Message = FormatErrors(removePasswordResult.Errors)
        };
      }

      var addPasswordResult = await userManager.AddPasswordAsync(user, request.Data.Password);
      if (!addPasswordResult.Succeeded)
      {
        return new IdentityCommandResult
        {
          Status = IdentityActionStatus.InvalidRequest,
          Message = FormatErrors(addPasswordResult.Errors)
        };
      }
    }

    if (request.Data.Roles != null)
    {
      var missingRoles = await FindMissingRolesAsync(request.Data.Roles);
      if (missingRoles.Length > 0)
      {
        return new IdentityCommandResult
        {
          Status = IdentityActionStatus.InvalidRequest,
          Message = $"Roles not found: {string.Join(", ", missingRoles)}"
        };
      }

      var currentRoles = await userManager.GetRolesAsync(user);
      var removeRolesResult = await userManager.RemoveFromRolesAsync(user, currentRoles);
      if (!removeRolesResult.Succeeded)
      {
        return new IdentityCommandResult
        {
          Status = IdentityActionStatus.InvalidRequest,
          Message = FormatErrors(removeRolesResult.Errors)
        };
      }

      if (request.Data.Roles.Length > 0)
      {
        var addRolesResult = await userManager.AddToRolesAsync(user, request.Data.Roles);
        if (!addRolesResult.Succeeded)
        {
          return new IdentityCommandResult
          {
            Status = IdentityActionStatus.InvalidRequest,
            Message = FormatErrors(addRolesResult.Errors)
          };
        }
      }
    }

    return new IdentityCommandResult
    {
      Status = IdentityActionStatus.Success
    };
  }

  public async Task<IdentityCommandResult> Handle(Core.Command.DeleteUser request, CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(request?.Id))
    {
      return new IdentityCommandResult
      {
        Status = IdentityActionStatus.InvalidRequest,
        Message = "User id is required."
      };
    }

    var user = await userManager.FindByIdAsync(request.Id);
    if (user == null)
    {
      return new IdentityCommandResult
      {
        Status = IdentityActionStatus.NotFound,
        Message = "User not found."
      };
    }

    var result = await userManager.DeleteAsync(user);
    if (!result.Succeeded)
    {
      return new IdentityCommandResult
      {
        Status = IdentityActionStatus.InvalidRequest,
        Message = FormatErrors(result.Errors)
      };
    }

    return new IdentityCommandResult
    {
      Status = IdentityActionStatus.Success
    };
  }

  public async Task<IdentityCommandResult> Handle(Core.Command.CreateRole request, CancellationToken cancellationToken)
  {
    if (request?.Data == null || string.IsNullOrWhiteSpace(request.Data.Name))
    {
      return new IdentityCommandResult
      {
        Status = IdentityActionStatus.InvalidRequest,
        Message = "Role name is required."
      };
    }

    var existingRole = await roleManager.FindByNameAsync(request.Data.Name);
    if (existingRole != null)
    {
      return new IdentityCommandResult
      {
        Status = IdentityActionStatus.Conflict,
        Message = "Role already exists."
      };
    }

    var result = await roleManager.CreateAsync(new IdentityRole(request.Data.Name));
    if (!result.Succeeded)
    {
      return new IdentityCommandResult
      {
        Status = IdentityActionStatus.InvalidRequest,
        Message = FormatErrors(result.Errors)
      };
    }

    return new IdentityCommandResult
    {
      Status = IdentityActionStatus.Success
    };
  }

  public async Task<IdentityCommandResult> Handle(Core.Command.UpdateRole request, CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(request?.Id) || request.Data == null || string.IsNullOrWhiteSpace(request.Data.Name))
    {
      return new IdentityCommandResult
      {
        Status = IdentityActionStatus.InvalidRequest,
        Message = "Role id and name are required."
      };
    }

    var role = await roleManager.FindByIdAsync(request.Id);
    if (role == null)
    {
      return new IdentityCommandResult
      {
        Status = IdentityActionStatus.NotFound,
        Message = "Role not found."
      };
    }

    var byName = await roleManager.FindByNameAsync(request.Data.Name);
    if (byName != null && byName.Id != role.Id)
    {
      return new IdentityCommandResult
      {
        Status = IdentityActionStatus.Conflict,
        Message = "Role already exists."
      };
    }

    role.Name = request.Data.Name;
    role.NormalizedName = request.Data.Name.ToUpperInvariant();

    var result = await roleManager.UpdateAsync(role);
    if (!result.Succeeded)
    {
      return new IdentityCommandResult
      {
        Status = IdentityActionStatus.InvalidRequest,
        Message = FormatErrors(result.Errors)
      };
    }

    return new IdentityCommandResult
    {
      Status = IdentityActionStatus.Success
    };
  }

  public async Task<IdentityCommandResult> Handle(Core.Command.DeleteRole request, CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(request?.Id))
    {
      return new IdentityCommandResult
      {
        Status = IdentityActionStatus.InvalidRequest,
        Message = "Role id is required."
      };
    }

    var role = await roleManager.FindByIdAsync(request.Id);
    if (role == null)
    {
      return new IdentityCommandResult
      {
        Status = IdentityActionStatus.NotFound,
        Message = "Role not found."
      };
    }

    var result = await roleManager.DeleteAsync(role);
    if (!result.Succeeded)
    {
      return new IdentityCommandResult
      {
        Status = IdentityActionStatus.InvalidRequest,
        Message = FormatErrors(result.Errors)
      };
    }

    return new IdentityCommandResult
    {
      Status = IdentityActionStatus.Success
    };
  }

  private static string GenerateSecret(int byteLength)
  {
    var bytes = RandomNumberGenerator.GetBytes(byteLength);
    return Base64UrlEncode(bytes);
  }

  private static string Base64UrlEncode(byte[] bytes)
  {
    return Convert.ToBase64String(bytes)
      .TrimEnd('=')
      .Replace('+', '-')
      .Replace('/', '_');
  }

  private static string FormatErrors(IEnumerable<IdentityError> errors)
  {
    return string.Join("; ", errors.Select(e => e.Description));
  }

  private async Task<string[]> FindMissingRolesAsync(IEnumerable<string> roles)
  {
    var missingRoles = new List<string>();

    foreach (var role in roles.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct())
    {
      if (!await roleManager.RoleExistsAsync(role))
        missingRoles.Add(role);
    }

    return missingRoles.ToArray();
  }
}
