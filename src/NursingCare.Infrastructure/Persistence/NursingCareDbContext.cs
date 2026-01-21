using Microsoft.EntityFrameworkCore;
using NursingCare.Domain.CareRequests;

namespace NursingCare.Infrastructure.Persistence;

public sealed class NursingCareDbContext : DbContext
{
  public NursingCareDbContext(DbContextOptions<NursingCareDbContext> options)
      : base(options)
  {
  }

  public DbSet<CareRequest> CareRequests => Set<CareRequest>();

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
  }
}
