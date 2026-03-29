using Microsoft.EntityFrameworkCore;
using NursingCareBackend.Domain.Admin;
using NursingCareBackend.Domain.CareRequests;
using NursingCareBackend.Domain.Catalogs;
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
       public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
       public DbSet<AdminNotification> AdminNotifications => Set<AdminNotification>();

       public DbSet<CareRequestCategoryCatalog> CareRequestCategoryCatalogs => Set<CareRequestCategoryCatalog>();
       public DbSet<UnitTypeCatalog> UnitTypeCatalogs => Set<UnitTypeCatalog>();
       public DbSet<CareRequestTypeCatalog> CareRequestTypeCatalogs => Set<CareRequestTypeCatalog>();
       public DbSet<DistanceFactorCatalog> DistanceFactorCatalogs => Set<DistanceFactorCatalog>();
       public DbSet<ComplexityLevelCatalog> ComplexityLevelCatalogs => Set<ComplexityLevelCatalog>();
       public DbSet<VolumeDiscountRule> VolumeDiscountRules => Set<VolumeDiscountRule>();
       public DbSet<NurseSpecialtyCatalog> NurseSpecialtyCatalogs => Set<NurseSpecialtyCatalog>();
       public DbSet<NurseCategoryCatalog> NurseCategoryCatalogs => Set<NurseCategoryCatalog>();

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

                     builder.Property(x => x.SuggestedNurse);
                     builder.Property(x => x.AssignedNurse);

                     builder.Property(x => x.PricingCategoryCode).HasMaxLength(64);
                     builder.Property(x => x.CategoryFactorSnapshot).HasColumnType("decimal(10,4)");
                     builder.Property(x => x.DistanceFactorMultiplierSnapshot).HasColumnType("decimal(10,4)");
                     builder.Property(x => x.ComplexityMultiplierSnapshot).HasColumnType("decimal(10,4)");
              });

              modelBuilder.Entity<User>(builder =>
              {
                     builder.ToTable("Users", table =>
                     {
                            table.HasCheckConstraint(
                             "CK_Users_Name_TextOnly",
                             "[Name] IS NULL OR (LEN(LTRIM(RTRIM([Name]))) > 0 AND [Name] NOT LIKE '%[^A-Za-zÁÉÍÓÚáéíóúÑñÜü ]%')");
                            table.HasCheckConstraint(
                             "CK_Users_LastName_TextOnly",
                             "[LastName] IS NULL OR (LEN(LTRIM(RTRIM([LastName]))) > 0 AND [LastName] NOT LIKE '%[^A-Za-zÁÉÍÓÚáéíóúÑñÜü ]%')");
                            table.HasCheckConstraint(
                             "CK_Users_IdentificationNumber_ExactDigits",
                             "[IdentificationNumber] IS NULL OR (LEN([IdentificationNumber]) = 11 AND [IdentificationNumber] NOT LIKE '%[^0-9]%')");
                            table.HasCheckConstraint(
                             "CK_Users_Phone_ExactDigits",
                             "[Phone] IS NULL OR (LEN([Phone]) = 10 AND [Phone] NOT LIKE '%[^0-9]%')");
                     });

                     builder.HasKey(x => x.Id);

                     builder.Property(x => x.Email)
                      .IsRequired()
                      .HasMaxLength(256);

                     builder.Property(x => x.ProfileType)
                      .HasConversion(
                          v => v.ToString().ToUpperInvariant(),
                          v => Enum.Parse<UserProfileType>(v, true))
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
                     builder.ToTable("Nurses", table =>
                     {
                            table.HasCheckConstraint(
                             "CK_Nurses_LicenseId_DigitsOnly",
                             "[LicenseId] IS NULL OR (LEN([LicenseId]) > 0 AND [LicenseId] NOT LIKE '%[^0-9]%')");
                            table.HasCheckConstraint(
                             "CK_Nurses_BankName_TextOnly",
                             "[BankName] IS NULL OR (LEN(LTRIM(RTRIM([BankName]))) > 0 AND [BankName] NOT LIKE '%[^A-Za-zÁÉÍÓÚáéíóúÑñÜü ]%')");
                            table.HasCheckConstraint(
                             "CK_Nurses_AccountNumber_DigitsOnly",
                             "[AccountNumber] IS NULL OR (LEN([AccountNumber]) > 0 AND [AccountNumber] NOT LIKE '%[^0-9]%')");
                     });

                     builder.HasKey(x => x.UserId);

                     builder.Property(x => x.IsActive)
                      .IsRequired();

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

              modelBuilder.Entity<AuditLog>(builder =>
              {
                     builder.ToTable("AuditLogs");

                     builder.HasKey(x => x.Id);

                     builder.Property(x => x.ActorRole)
                  .IsRequired()
                  .HasMaxLength(100);

                     builder.Property(x => x.Action)
                  .IsRequired()
                  .HasMaxLength(150);

                     builder.Property(x => x.EntityType)
                  .IsRequired()
                  .HasMaxLength(100);

                     builder.Property(x => x.EntityId)
                  .IsRequired()
                  .HasMaxLength(150);

                     builder.Property(x => x.Notes)
                  .HasMaxLength(1000);

                     builder.Property(x => x.MetadataJson);

                     builder.Property(x => x.CreatedAtUtc)
                  .IsRequired();
              });

              modelBuilder.Entity<AdminNotification>(builder =>
              {
                     builder.ToTable("AdminNotifications");

                     builder.HasKey(x => x.Id);

                     builder.Property(x => x.Category)
                  .IsRequired()
                  .HasMaxLength(80);

                     builder.Property(x => x.Severity)
                  .IsRequired()
                  .HasMaxLength(20);

                     builder.Property(x => x.Title)
                  .IsRequired()
                  .HasMaxLength(220);

                     builder.Property(x => x.Body)
                  .IsRequired()
                  .HasMaxLength(2000);

                     builder.Property(x => x.EntityType)
                  .HasMaxLength(80);

                     builder.Property(x => x.EntityId)
                  .HasMaxLength(120);

                     builder.Property(x => x.DeepLinkPath)
                  .HasMaxLength(600);

                     builder.Property(x => x.Source)
                  .HasMaxLength(180);

                     builder.Property(x => x.CreatedAtUtc)
                  .IsRequired();

                     builder.HasIndex(x => new { x.RecipientUserId, x.ArchivedAtUtc, x.ReadAtUtc });
              });

              modelBuilder.Entity<CareRequestCategoryCatalog>(builder =>
              {
                     builder.ToTable("CareRequestCategoryCatalogs");

                     builder.HasKey(x => x.Id);

                     builder.Property(x => x.Code)
                  .IsRequired()
                  .HasMaxLength(64);

                     builder.Property(x => x.DisplayName)
                  .IsRequired()
                  .HasMaxLength(200);

                     builder.Property(x => x.CategoryFactor)
                  .HasColumnType("decimal(10,4)");

                     builder.HasIndex(x => x.Code)
                  .IsUnique();
              });

              modelBuilder.Entity<UnitTypeCatalog>(builder =>
              {
                     builder.ToTable("UnitTypeCatalogs");

                     builder.HasKey(x => x.Id);

                     builder.Property(x => x.Code)
                  .IsRequired()
                  .HasMaxLength(64);

                     builder.Property(x => x.DisplayName)
                  .IsRequired()
                  .HasMaxLength(200);

                     builder.HasIndex(x => x.Code)
                  .IsUnique();
              });

              modelBuilder.Entity<CareRequestTypeCatalog>(builder =>
              {
                     builder.ToTable("CareRequestTypeCatalogs");

                     builder.HasKey(x => x.Id);

                     builder.Property(x => x.Code)
                  .IsRequired()
                  .HasMaxLength(64);

                     builder.Property(x => x.DisplayName)
                  .IsRequired()
                  .HasMaxLength(200);

                     builder.Property(x => x.CareRequestCategoryCode)
                  .IsRequired()
                  .HasMaxLength(64);

                     builder.Property(x => x.UnitTypeCode)
                  .IsRequired()
                  .HasMaxLength(64);

                     builder.Property(x => x.BasePrice)
                  .HasColumnType("decimal(12,2)");

                     builder.HasIndex(x => x.Code)
                  .IsUnique();
              });

              modelBuilder.Entity<DistanceFactorCatalog>(builder =>
              {
                     builder.ToTable("DistanceFactorCatalogs");

                     builder.HasKey(x => x.Id);

                     builder.Property(x => x.Code)
                  .IsRequired()
                  .HasMaxLength(64);

                     builder.Property(x => x.DisplayName)
                  .IsRequired()
                  .HasMaxLength(200);

                     builder.Property(x => x.Multiplier)
                  .HasColumnType("decimal(10,4)");

                     builder.HasIndex(x => x.Code)
                  .IsUnique();
              });

              modelBuilder.Entity<ComplexityLevelCatalog>(builder =>
              {
                     builder.ToTable("ComplexityLevelCatalogs");

                     builder.HasKey(x => x.Id);

                     builder.Property(x => x.Code)
                  .IsRequired()
                  .HasMaxLength(64);

                     builder.Property(x => x.DisplayName)
                  .IsRequired()
                  .HasMaxLength(200);

                     builder.Property(x => x.Multiplier)
                  .HasColumnType("decimal(10,4)");

                     builder.HasIndex(x => x.Code)
                  .IsUnique();
              });

              modelBuilder.Entity<VolumeDiscountRule>(builder =>
              {
                     builder.ToTable("VolumeDiscountRules");

                     builder.HasKey(x => x.Id);

                     builder.Property(x => x.MinimumCount)
                  .IsRequired();

                     builder.Property(x => x.DiscountPercent)
                  .IsRequired();
              });

              modelBuilder.Entity<NurseSpecialtyCatalog>(builder =>
              {
                     builder.ToTable("NurseSpecialtyCatalogs");

                     builder.HasKey(x => x.Id);

                     builder.Property(x => x.Code)
                  .IsRequired()
                  .HasMaxLength(150);

                     builder.Property(x => x.DisplayName)
                  .IsRequired()
                  .HasMaxLength(200);

                     builder.Property(x => x.AlternativeCodes)
                  .HasMaxLength(1000);

                     builder.HasIndex(x => x.Code)
                  .IsUnique();
              });

              modelBuilder.Entity<NurseCategoryCatalog>(builder =>
              {
                     builder.ToTable("NurseCategoryCatalogs");

                     builder.HasKey(x => x.Id);

                     builder.Property(x => x.Code)
                  .IsRequired()
                  .HasMaxLength(100);

                     builder.Property(x => x.DisplayName)
                  .IsRequired()
                  .HasMaxLength(200);

                     builder.Property(x => x.AlternativeCodes)
                  .HasMaxLength(1000);

                     builder.HasIndex(x => x.Code)
                  .IsUnique();
              });
       }
}
