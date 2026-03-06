using Microsoft.EntityFrameworkCore;
using NursingCareBackend.Domain.CareRequests;
using NursingCareBackend.Domain.Identity;

namespace NursingCareBackend.Infrastructure.Persistence;

public sealed class NursingCareDbContext : DbContext
{
  public NursingCareDbContext(DbContextOptions<NursingCareDbContext> options)
      : base(options)
  {
  }

  public DbSet<CareRequest> CareRequests => Set<CareRequest>();
  public DbSet<User> Users => Set<User>();
  public DbSet<Role> Roles => Set<Role>();
  public DbSet<UserRole> UserRoles => Set<UserRole>();

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    modelBuilder.Entity<CareRequest>(builder =>
    {
      builder.ToTable("CareRequests");

      builder.HasKey(x => x.Id);

      builder.Property(x => x.Description)
                 .IsRequired()
                 .HasMaxLength(1000);

      builder.Property(x => x.Status)
                 .IsRequired();

      builder.Property(x => x.CreatedAtUtc)
                 .IsRequired();
    });

    modelBuilder.Entity<User>(builder =>
    {
      builder.ToTable("Users");

      builder.HasKey(x => x.Id);

      builder.Property(x => x.Email)
                 .IsRequired()
                 .HasMaxLength(256);

      builder.HasIndex(x => x.Email)
             .IsUnique();

      builder.Property(x => x.PasswordHash)
                 .IsRequired();

      builder.Property(x => x.IsActive)
                 .IsRequired();

      builder.Property(x => x.CreatedAtUtc)
                 .IsRequired();
    });

    modelBuilder.Entity<Role>(builder =>
    {
      builder.ToTable("Roles");

      builder.HasKey(x => x.Id);

      builder.Property(x => x.Name)
                 .IsRequired()
                 .HasMaxLength(100);

      builder.HasIndex(x => x.Name)
             .IsUnique();
    });

    modelBuilder.Entity<UserRole>(builder =>
    {
      builder.ToTable("UserRoles");

      builder.HasKey(x => new { x.UserId, x.RoleId });

      builder.HasOne(x => x.User)
             .WithMany(u => u.UserRoles)
             .HasForeignKey(x => x.UserId);

      builder.HasOne(x => x.Role)
             .WithMany(r => r.UserRoles)
             .HasForeignKey(x => x.RoleId);
    });
  }
}
