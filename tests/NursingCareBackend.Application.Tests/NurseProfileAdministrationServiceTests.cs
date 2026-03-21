using NursingCareBackend.Application.Identity.Commands;
using NursingCareBackend.Application.Identity.Repositories;
using NursingCareBackend.Application.Identity.Services;
using NursingCareBackend.Domain.Identity;

namespace NursingCareBackend.Application.Tests;

public sealed class NurseProfileAdministrationServiceTests
{
  [Fact]
  public async Task GetPendingNurseProfilesAsync_Should_Return_Only_Nurses_Still_Under_Admin_Completion()
  {
    var pendingNurse = CreateNurseUser("pending@example.com", nurseProfileIsActive: false);
    var completedNurse = CreateNurseUser("completed@example.com", nurseProfileIsActive: true);
    var client = CreateClientUser("client@example.com");

    var service = new NurseProfileAdministrationService(
      new FakeUserRepository(pendingNurse, completedNurse, client));

    var response = await service.GetPendingNurseProfilesAsync();

    var pending = Assert.Single(response);
    Assert.Equal(pendingNurse.Id, pending.UserId);
    Assert.Equal("pending@example.com", pending.Email);
  }

  [Fact]
  public async Task CompleteNurseProfileCreationAsync_Should_Update_User_And_Nurse_And_Activate_Both()
  {
    var nurse = CreateNurseUser("pending@example.com", nurseProfileIsActive: false);
    nurse.IsActive = false;
    var repository = new FakeUserRepository(nurse);
    var service = new NurseProfileAdministrationService(repository);

    var response = await service.CompleteNurseProfileCreationAsync(
      nurse.Id,
      new AdminCompleteNurseProfileRequest(
        Name: "Laura",
        LastName: "Gomez",
        IdentificationNumber: "001-1111111-1",
        Phone: "8095550199",
        Email: "laura.gomez@example.com",
        HireDate: new DateOnly(2026, 3, 21),
        Specialty: "Critical Care",
        LicenseId: "LIC-55",
        BankName: "Banco Central",
        AccountNumber: "123456",
        Category: "Senior"),
      CancellationToken.None);

    Assert.True(nurse.IsActive);
    Assert.True(nurse.NurseProfile!.IsActive);
    Assert.Equal("laura.gomez@example.com", nurse.Email);
    Assert.Equal("Critical Care", nurse.NurseProfile.Specialty);
    Assert.Equal("Senior", nurse.NurseProfile.Category);
    Assert.Equal("laura.gomez@example.com", response.Email);
    Assert.True(response.NurseProfileIsActive);
  }

  [Fact]
  public async Task CompleteNurseProfileCreationAsync_Should_Reject_Duplicate_Email()
  {
    var nurse = CreateNurseUser("pending@example.com", nurseProfileIsActive: false);
    var otherUser = CreateClientUser("used@example.com");

    var service = new NurseProfileAdministrationService(
      new FakeUserRepository(nurse, otherUser));

    var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
      service.CompleteNurseProfileCreationAsync(
        nurse.Id,
        new AdminCompleteNurseProfileRequest(
          Name: "Laura",
          LastName: "Gomez",
          IdentificationNumber: "001-1111111-1",
          Phone: "8095550199",
          Email: "used@example.com",
          HireDate: new DateOnly(2026, 3, 21),
          Specialty: "Critical Care",
          LicenseId: null,
          BankName: "Banco Central",
          AccountNumber: null,
          Category: "Senior")));

    Assert.Equal("User with this email already exists.", exception.Message);
  }

  private static User CreateNurseUser(string email, bool nurseProfileIsActive)
  {
    var role = new Role
    {
      Id = Guid.NewGuid(),
      Name = SystemRoles.Nurse
    };

    var user = new User
    {
      Id = Guid.NewGuid(),
      Email = email,
      ProfileType = UserProfileType.Nurse,
      Name = "Pending",
      LastName = "Nurse",
      IdentificationNumber = "001-0000000-1",
      Phone = "8095550100",
      PasswordHash = "hash",
      IsActive = true,
      CreatedAtUtc = DateTime.UtcNow,
      NurseProfile = new Nurse
      {
        IsActive = nurseProfileIsActive
      }
    };

    user.NurseProfile.UserId = user.Id;
    user.UserRoles.Add(new UserRole
    {
      UserId = user.Id,
      RoleId = role.Id,
      Role = role,
      User = user
    });

    return user;
  }

  private static User CreateClientUser(string email)
  {
    var role = new Role
    {
      Id = Guid.NewGuid(),
      Name = SystemRoles.Client
    };

    var user = new User
    {
      Id = Guid.NewGuid(),
      Email = email,
      ProfileType = UserProfileType.Client,
      Name = "Client",
      LastName = "User",
      IdentificationNumber = "001-0000000-2",
      Phone = "8095550101",
      PasswordHash = "hash",
      IsActive = true,
      CreatedAtUtc = DateTime.UtcNow,
      ClientProfile = new Client()
    };

    user.ClientProfile.UserId = user.Id;
    user.UserRoles.Add(new UserRole
    {
      UserId = user.Id,
      RoleId = role.Id,
      Role = role,
      User = user
    });

    return user;
  }

  private sealed class FakeUserRepository : IUserRepository
  {
    private readonly Dictionary<Guid, User> _usersById;
    private readonly Dictionary<string, User> _usersByEmail;

    public FakeUserRepository(params User[] users)
    {
      _usersById = users.ToDictionary(user => user.Id);
      _usersByEmail = users.ToDictionary(user => user.Email, StringComparer.OrdinalIgnoreCase);
    }

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
      => Task.FromResult(_usersByEmail.TryGetValue(email, out var user) ? user : null);

    public Task<User?> GetByGoogleSubjectIdAsync(string googleSubjectId, CancellationToken cancellationToken = default)
      => Task.FromResult<User?>(null);

    public Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default)
      => Task.FromResult(_usersById.TryGetValue(userId, out var user) ? user : null);

    public Task<IReadOnlyList<User>> GetPendingNurseProfilesAsync(CancellationToken cancellationToken = default)
      => Task.FromResult<IReadOnlyList<User>>(
        _usersById.Values
          .Where(user => user.ProfileType == UserProfileType.Nurse && user.NurseProfile?.IsActive == false)
          .OrderBy(user => user.CreatedAtUtc)
          .ToArray());

    public Task<User> CreateAsync(User user, CancellationToken cancellationToken = default)
    {
      _usersById[user.Id] = user;
      _usersByEmail[user.Email] = user;
      return Task.FromResult(user);
    }

    public Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
      _usersById[user.Id] = user;
      _usersByEmail[user.Email] = user;
      return Task.CompletedTask;
    }
  }
}
