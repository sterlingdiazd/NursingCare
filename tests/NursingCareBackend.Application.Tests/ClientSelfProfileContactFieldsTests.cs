using Microsoft.EntityFrameworkCore;
using NursingCareBackend.Application.Identity.ClientProfiles;
using NursingCareBackend.Domain.Identity;
using NursingCareBackend.Infrastructure.Identity;
using NursingCareBackend.Infrastructure.Persistence;
using NursingCareBackend.Tests.Infrastructure;
using Xunit;

namespace NursingCareBackend.Application.Tests;

// Locks the P1-4 data-loss fix: the mobile client self-profile sends
// preferredAddress / emergencyContactName / emergencyContactPhone, which the
// backend previously dropped silently. This exercises the real SQL persistence
// path (UserRepository + ClientSelfProfileService) end to end.
public sealed class ClientSelfProfileContactFieldsTests
{
  private static NursingCareDbContext CreateDbContext()
  {
    var connectionString = TestSqlConnectionResolver.CreateUniqueDatabaseConnectionString();
    var options = new DbContextOptionsBuilder<NursingCareDbContext>()
        .UseSqlServer(connectionString)
        .Options;

    var context = new NursingCareDbContext(options);
    context.Database.EnsureDeleted();
    context.Database.EnsureCreated();
    CatalogSeeding.EnsureSeededAsync(context).GetAwaiter().GetResult();

    return context;
  }

  private static async Task<Guid> SeedClientUserAsync(NursingCareDbContext context)
  {
    var clientRole = await context.Roles.FirstAsync(role => role.Name == SystemRoles.Client);

    var user = new User
    {
      Id = Guid.NewGuid(),
      ProfileType = UserProfileType.CLIENT,
      Name = "Ana",
      LastName = "Gomez",
      IdentificationNumber = "00112345678",
      Phone = "8095550100",
      Email = $"client-{Guid.NewGuid():N}@example.com",
      PasswordHash = "hashed-password",
      IsActive = true,
      CreatedAtUtc = DateTime.UtcNow,
      ClientProfile = new Client(),
    };
    user.ClientProfile.UserId = user.Id;
    user.UserRoles.Add(new UserRole
    {
      UserId = user.Id,
      RoleId = clientRole.Id,
    });

    context.Users.Add(user);
    await context.SaveChangesAsync();
    return user.Id;
  }

  [Fact]
  public async Task UpdateAsync_Should_Persist_And_Echo_Contact_Fields()
  {
    await using var dbContext = CreateDbContext();
    var userId = await SeedClientUserAsync(dbContext);

    // A fresh context + repository per operation mirrors the per-request
    // scoping in production and proves the values survive a DB round trip.
    await using (var writeContext = CreateContextFor(dbContext))
    {
      var service = new ClientSelfProfileService(new UserRepository(writeContext));
      var response = await service.UpdateAsync(
        userId,
        new UpdateClientSelfProfileRequest(
          Name: "Ana",
          LastName: "Gomez",
          IdentificationNumber: "00112345678",
          Phone: "8095550100",
          PreferredAddress: "  Calle Primera 123, Santo Domingo  ",
          EmergencyContactName: "  Maria Gomez  ",
          EmergencyContactPhone: " 8095550200 "));

      // Echoed back, trimmed.
      Assert.Equal("Calle Primera 123, Santo Domingo", response.PreferredAddress);
      Assert.Equal("Maria Gomez", response.EmergencyContactName);
      Assert.Equal("8095550200", response.EmergencyContactPhone);
    }

    // Reload through the read path (GetAsync) on a brand-new context.
    await using var readContext = CreateContextFor(dbContext);
    var readService = new ClientSelfProfileService(new UserRepository(readContext));
    var reloaded = await readService.GetAsync(userId);

    Assert.NotNull(reloaded);
    Assert.Equal("Calle Primera 123, Santo Domingo", reloaded!.PreferredAddress);
    Assert.Equal("Maria Gomez", reloaded.EmergencyContactName);
    Assert.Equal("8095550200", reloaded.EmergencyContactPhone);
  }

  [Fact]
  public async Task UpdateAsync_Should_Leave_Contact_Fields_Null_When_Omitted()
  {
    await using var dbContext = CreateDbContext();
    var userId = await SeedClientUserAsync(dbContext);

    await using (var writeContext = CreateContextFor(dbContext))
    {
      var service = new ClientSelfProfileService(new UserRepository(writeContext));
      // Request without the optional contact fields (and explicit empty values
      // for robustness) must normalize to null, not empty strings.
      await service.UpdateAsync(
        userId,
        new UpdateClientSelfProfileRequest(
          Name: "Ana",
          LastName: "Gomez",
          IdentificationNumber: "00112345678",
          Phone: "8095550100",
          PreferredAddress: "   ",
          EmergencyContactName: null,
          EmergencyContactPhone: null));
    }

    await using var readContext = CreateContextFor(dbContext);
    var readService = new ClientSelfProfileService(new UserRepository(readContext));
    var reloaded = await readService.GetAsync(userId);

    Assert.NotNull(reloaded);
    Assert.Null(reloaded!.PreferredAddress);
    Assert.Null(reloaded.EmergencyContactName);
    Assert.Null(reloaded.EmergencyContactPhone);
  }

  private static NursingCareDbContext CreateContextFor(NursingCareDbContext template)
  {
    var options = new DbContextOptionsBuilder<NursingCareDbContext>()
        .UseSqlServer(template.Database.GetConnectionString())
        .Options;
    return new NursingCareDbContext(options);
  }
}
