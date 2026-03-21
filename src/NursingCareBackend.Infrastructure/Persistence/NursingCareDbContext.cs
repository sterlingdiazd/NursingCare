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
       public DbSet<Nurse> Nurses => Set<Nurse>();
       public DbSet<Client> Clients => Set<Client>();
       public DbSet<Role> Roles => Set<Role>();
       public DbSet<UserRole> UserRoles => Set<UserRole>();
       public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

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

                     builder.Property(x => x.UpdatedAtUtc)
                      .IsRequired();

                     builder.Property(x => x.ApprovedAtUtc);
                     builder.Property(x => x.RejectedAtUtc);
                     builder.Property(x => x.CompletedAtUtc);

                     // Removed custom column mappings – now using default property names
                     builder.Property(x => x.UserID)
                  .IsRequired(); // column name will be "UserID"

                     builder.Property(x => x.CareRequestReason); // column: "CareRequestReason"

                     builder.Property(x => x.CareRequestType)
                  .IsRequired(); // column: "CareRequestType"

                     builder.Property(x => x.Unit)
                  .IsRequired();

                     builder.Property(x => x.UnitType)
                  .IsRequired();

                     builder.Property(x => x.Price)
                  .HasColumnType("decimal(10,2)");

                     builder.Property(x => x.Total)
                  .HasColumnType("decimal(10,2)");

                     builder.Property(x => x.DistanceFactor);
                     builder.Property(x => x.ComplexityLevel);

                     builder.Property(x => x.ClientBasePrice)
                  .HasColumnType("decimal(10,2)");

                     builder.Property(x => x.MedicalSuppliesCost)
                  .HasColumnType("decimal(10,2)");

                     builder.Property(x => x.CareRequestDate); // column: "CareRequestDate"

                     builder.Property(x => x.NurseId);
                     builder.Property(x => x.SuggestedNurse);
                     builder.Property(x => x.AssignedNurse);
              });

              modelBuilder.Entity<User>(builder =>
              {
                     builder.ToTable("Users");

                     builder.HasKey(x => x.Id);

                     builder.Property(x => x.Email)
                      .IsRequired()
                      .HasMaxLength(256);

                     builder.Property(x => x.ProfileType)
                      .HasConversion<string>()
                      .IsRequired()
                      .HasMaxLength(20);

                     builder.HasIndex(x => x.Email)
                  .IsUnique();

                     builder.Property(x => x.Name)
                      .HasMaxLength(150);

                     builder.Property(x => x.LastName)
                      .HasMaxLength(150);

                     builder.Property(x => x.IdentificationNumber)
                      .HasMaxLength(50);

                     builder.Property(x => x.Phone)
                      .HasMaxLength(30);

                     builder.Property(x => x.DisplayName)
                      .HasMaxLength(256);

                     builder.Property(x => x.GoogleSubjectId)
                      .HasMaxLength(256);

                     builder.HasIndex(x => x.GoogleSubjectId)
                  .IsUnique()
                  .HasFilter("[GoogleSubjectId] IS NOT NULL");

                     builder.Property(x => x.PasswordHash)
                      .IsRequired();

                     builder.Property(x => x.IsActive)
                      .IsRequired();

                     builder.Property(x => x.CreatedAtUtc)
                      .IsRequired();

                     builder.HasOne(x => x.NurseProfile)
                      .WithOne(x => x.User)
                      .HasForeignKey<Nurse>(x => x.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                     builder.HasOne(x => x.ClientProfile)
                      .WithOne(x => x.User)
                      .HasForeignKey<Client>(x => x.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
              });

              modelBuilder.Entity<Nurse>(builder =>
              {
                     builder.ToTable("Nurses");

                     builder.HasKey(x => x.UserId);

                     builder.Property(x => x.HireDate)
                      .HasColumnType("date");

                     builder.Property(x => x.Specialty)
                      .HasMaxLength(150);

                     builder.Property(x => x.LicenseId)
                      .HasMaxLength(100);

                     builder.Property(x => x.BankName)
                      .HasMaxLength(150);

                     builder.Property(x => x.AccountNumber)
                      .HasMaxLength(50);

                     builder.Property(x => x.Category)
                      .HasMaxLength(100);
              });

              modelBuilder.Entity<Client>(builder =>
              {
                     builder.ToTable("Clients");

                     builder.HasKey(x => x.UserId);
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

              modelBuilder.Entity<RefreshToken>(builder =>
              {
                     builder.ToTable("RefreshTokens");

                     builder.HasKey(x => x.Id);

                     builder.Property(x => x.Token)
                  .IsRequired()
                  .HasMaxLength(512);

                     builder.HasIndex(x => x.Token)
                  .IsUnique();

                     builder.Property(x => x.CreatedAtUtc)
                  .IsRequired();

                     builder.Property(x => x.ExpiresAtUtc)
                  .IsRequired();

                     builder.Property(x => x.RevokedAtUtc);

                     builder.HasOne(x => x.User)
                  .WithMany(user => user.RefreshTokens)
                  .HasForeignKey(x => x.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
              });
       }
}
