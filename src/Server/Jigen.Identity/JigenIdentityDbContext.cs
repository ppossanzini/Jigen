using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Jigen.Identity;

public class JigenIdentityDbContext : IdentityDbContext<IdentityUser, IdentityRole, string>
{
  public JigenIdentityDbContext(DbContextOptions<JigenIdentityDbContext> options) : base(options)
  {
  }

  protected override void OnModelCreating(ModelBuilder builder)
  {
    base.OnModelCreating(builder);
    builder.UseOpenIddict();
  }
}
