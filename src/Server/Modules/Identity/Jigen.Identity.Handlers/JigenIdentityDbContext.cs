using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Jigen.Identity.Handlers;

public class JigenIdentityDbContext (DbContextOptions<JigenIdentityDbContext> options) : IdentityDbContext<IdentityUser, IdentityRole, string>(options)
{
  protected override void OnModelCreating(ModelBuilder builder)
  {
    base.OnModelCreating(builder);
    builder.UseOpenIddict();
  }
}
