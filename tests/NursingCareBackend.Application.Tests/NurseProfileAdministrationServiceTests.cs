using NursingCareBackend.Application.AdminPortal.Auditing;
using NursingCareBackend.Application.AdminPortal.Notifications;
using NursingCareBackend.Application.Identity.Authentication;
using NursingCareBackend.Application.Identity.Commands;
using NursingCareBackend.Application.Identity.Models;
using NursingCareBackend.Application.Identity.Repositories;
using NursingCareBackend.Application.Identity.Services;
using NursingCareBackend.Domain.Identity;

namespace NursingCareBackend.Application.Tests;

public sealed class NurseProfileAdministrationServiceTests
{
  [Fact]
  public async Task GetPendingNurseProfilesAsync_Should_Return_Only_Nurses_Still_Under_Admin_Completion()
  {
    var pendingNurse = CreateNurseUser("pending@example.com", isProfileComplete: false, userIsActive: true, nurseProfileIsActive: false);
    var completedNurse = CreateNurseUser("completed@example.com", isProfileComplete: true, userIsActive: true, nurseProfileIsActive: true);
    var inactiveNurse = CreateNurseUser("inactive@example.com", isProfileComplete: true, userIsActive: false, nurseProfileIsActive: false);
    var client = CreateClientUser("client@example.com");

    var service = CreateService(new FakeUserRepository(pendingNurse, completedNurse, inactiveNurse, client));

    var response = await service.GetPendingNurseProfilesAsync();

    var pending = Assert.Single(response);
    Assert.Equal(pendingNurse.Id, pending.UserId);
    Assert.Equal("pending@example.com", pending.Email);
    Assert.Equal("Atencion domiciliaria", pending.Specialty);
  }

  [Fact]
  public async Task GetInactiveNurseProfilesAsync_Should_Return_Completed_Nurses_With_Operational_Access_Disabled()
  {
    var activeNurse = CreateNurseUser("active@example.com", isProfileComplete: true, userIsActive: true, nurseProfileIsActive: true);
    var inactiveNurse = CreateNurseUser("inactive@example.com", isProfileComplete: true, userIsActive: false, nurseProfileIsActive: false);
    var pendingNurse = CreateNurseUser("pending@example.com", isProfileComplete: false, userIsActive: true, nurseProfileIsActive: false);
    var repository = new FakeUserRepository(activeNurse, inactiveNurse, pendingNurse);
    repository.SetWorkload(inactiveNurse.Id, new NurseWorkloadSummary(3, 1, 1, 0, 1, new DateTime(2026, 3, 21, 12, 0, 0, DateTimeKind.Utc)));

    var service = CreateService(repository);

    var response = await service.GetInactiveNurseProfilesAsync();

    var inactive = Assert.Single(response);
    Assert.Equal(inactiveNurse.Id, inactive.UserId);
    Assert.False(inactive.IsAssignmentReady);
    Assert.Equal(3, inactive.Workload.TotalAssignedCareRequests);
  }

  [Fact]
  public async Task CreateNurseProfileAsync_Should_Create_And_Audit_Nurse_Profile()
  {
    var repository = new FakeUserRepository();
    var auditService = new FakeAdminAuditService();
    var notificationPublisher = new FakeAdminNotificationPublisher();
    var service = CreateService(repository, auditService: auditService, notificationPublisher: notificationPublisher);

    var response = await service.CreateNurseProfileAsync(
      new AdminCreateNurseProfileRequest(
        Name: "Laura",
        LastName: "Gomez",
        IdentificationNumber: "00111111111",
        Phone: "8095550199",
        Email: "laura.gomez@example.com",
        Password: "Pass123!",
        ConfirmPassword: "Pass123!",
        HireDate: new DateOnly(2026, 3, 21),
        Specialty: "Cuidados intensivos",
        LicenseId: "55",
        BankName: "Banco Central",
        AccountNumber: "123456",
        Category: "Semi Senior",
        IsOperationallyActive: false),
      actorUserId: Guid.NewGuid(),
      CancellationToken.None);

    Assert.Equal("laura.gomez@example.com", response.Email);
    Assert.False(response.UserIsActive);
    Assert.False(response.NurseProfileIsActive);
    Assert.True(response.IsProfileComplete);
    Assert.False(response.IsPendingReview);
    Assert.Equal("Semisenior", response.Category);

    var stored = await repository.GetByEmailAsync("laura.gomez@example.com");
    Assert.NotNull(stored);
    Assert.Equal(UserProfileType.NURSE, stored!.ProfileType);
    Assert.Contains(stored.UserRoles, userRole => userRole.Role.Name == SystemRoles.Nurse);
    Assert.Single(auditService.Records);
    Assert.Equal(AdminAuditActions.NurseProfileCreatedByAdmin, auditService.Records[0].Action);
    
    // Check notification
    var notification = Assert.Single(notificationPublisher.PublishedRequests);
    Assert.Equal("nurse_profile_completed", notification.Category);
    Assert.Equal("Low", notification.Severity);
  }

  [Fact]
  public async Task CompleteNurseProfileCreationAsync_Should_Update_User_And_Nurse_And_Activate_Both()
  {
    var nurse = CreateNurseUser("pending@example.com", isProfileComplete: false, userIsActive: true, nurseProfileIsActive: false);
    nurse.IsActive = true;
    var repository = new FakeUserRepository(nurse);
    var auditService = new FakeAdminAuditService();
    var notificationPublisher = new FakeAdminNotificationPublisher();
    var service = CreateService(repository, auditService: auditService, notificationPublisher: notificationPublisher);

    var response = await service.CompleteNurseProfileCreationAsync(
      nurse.Id,
      new AdminCompleteNurseProfileRequest(
        Name: "Laura",
        LastName: "Gomez",
        IdentificationNumber: "00111111111",
        Phone: "8095550199",
        Email: "laura.gomez@example.com",
        HireDate: new DateOnly(2026, 3, 21),
        Specialty: "Cuidados intensivos",
        LicenseId: "55",
        BankName: "Banco Central",
        AccountNumber: "123456",
        Category: "Senior"),
      actorUserId: Guid.NewGuid(),
      CancellationToken.None);

    Assert.True(nurse.IsActive);
    Assert.True(nurse.NurseProfile!.IsActive);
    Assert.Equal("laura.gomez@example.com", nurse.Email);
    Assert.Equal("Cuidados intensivos", nurse.NurseProfile.Specialty);
    Assert.Equal("Senior", nurse.NurseProfile.Category);
    Assert.Equal("laura.gomez@example.com", response.Email);
    Assert.True(response.NurseProfileIsActive);
    Assert.False(response.IsPendingReview);
    Assert.Single(auditService.Records);
    Assert.Equal(AdminAuditActions.NurseProfileCompleted, auditService.Records[0].Action);

    // Check notification
    var notification = Assert.Single(notificationPublisher.PublishedRequests);
    Assert.Equal("nurse_profile_completed", notification.Category);
    Assert.Equal("Low", notification.Severity);
  }

  [Fact]
  public async Task CompleteNurseProfileCreationAsync_Should_Reject_Duplicate_Email()
  {
    var nurse = CreateNurseUser("pending@example.com", isProfileComplete: false, userIsActive: true, nurseProfileIsActive: false);
    var otherUser = CreateClientUser("used@example.com");

    var service = CreateService(new FakeUserRepository(nurse, otherUser));

    var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
      service.CompleteNurseProfileCreationAsync(
        nurse.Id,
        new AdminCompleteNurseProfileRequest(
          Name: "Laura",
          LastName: "Gomez",
          IdentificationNumber: "00111111111",
          Phone: "8095550199",
          Email: "used@example.com",
          HireDate: new DateOnly(2026, 3, 21),
          Specialty: "Cuidados intensivos",
          LicenseId: null,
          BankName: "Banco Central",
          AccountNumber: null,
          Category: "Senior"),
        actorUserId: Guid.NewGuid()));

    Assert.Equal("User with this email already exists.", exception.Message);
  }

  [Fact]
  public async Task UpdateNurseProfileAsync_Should_Reject_Pending_Profiles()
  {
    var nurse = CreateNurseUser("pending@example.com", isProfileComplete: false, userIsActive: true, nurseProfileIsActive: false);
    var service = CreateService(new FakeUserRepository(nurse));

    var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
      service.UpdateNurseProfileAsync(
        nurse.Id,
        new AdminUpdateNurseProfileRequest(
          Name: "Laura",
          LastName: "Gomez",
          IdentificationNumber: "00111111111",
          Phone: "8095550199",
          Email: "laura.gomez@example.com",
          HireDate: new DateOnly(2026, 3, 21),
          Specialty: "Cuidados intensivos",
          LicenseId: "55",
          BankName: "Banco Central",
          AccountNumber: "123456",
          Category: "Senior"),
        actorUserId: Guid.NewGuid()));

    Assert.Equal("Pending nurse profiles must be completed through the review flow.", exception.Message);
  }

  [Fact]
  public async Task SetOperationalAccessAsync_Should_Require_Completed_Profile()
  {
    var nurse = CreateNurseUser("pending@example.com", isProfileComplete: false, userIsActive: true, nurseProfileIsActive: false);
    var service = CreateService(new FakeUserRepository(nurse));

    var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
      service.SetOperationalAccessAsync(
        nurse.Id,
        new AdminSetNurseOperationalAccessRequest(true),
        actorUserId: Guid.NewGuid()));

    Assert.Equal("Nurse operational access can only be changed after the profile is complete.", exception.Message);
  }

  [Fact]
  public async Task SetOperationalAccessAsync_Should_Deactivate_Completed_Profile()
  {
    var nurse = CreateNurseUser("active@example.com", isProfileComplete: true, userIsActive: true, nurseProfileIsActive: true);
    var auditService = new FakeAdminAuditService();
    var service = CreateService(new FakeUserRepository(nurse), auditService: auditService);

    var response = await service.SetOperationalAccessAsync(
      nurse.Id,
      new AdminSetNurseOperationalAccessRequest(false),
      actorUserId: Guid.NewGuid(),
      CancellationToken.None);

    Assert.False(nurse.IsActive);
    Assert.False(nurse.NurseProfile!.IsActive);
    Assert.False(response.IsAssignmentReady);
    Assert.False(response.IsPendingReview);
    Assert.Single(auditService.Records);
    Assert.Equal(AdminAuditActions.NurseOperationalAccessChanged, auditService.Records[0].Action);
  }

  private static NurseProfileAdministrationService CreateService(
    FakeUserRepository userRepository,
    FakeRoleRepository? roleRepository = null,
    FakePasswordHasher? passwordHasher = null,
    FakeAdminAuditService? auditService = null,
    FakeAdminNotificationPublisher? notificationPublisher = null)
  {
    return new NurseProfileAdministrationService(
      userRepository,
      roleRepository ?? new FakeRoleRepository(),
      passwordHasher ?? new FakePasswordHasher(),
      auditService ?? new FakeAdminAuditService(),
      new FakeNurseCatalogService(),
      notificationPublisher ?? new FakeAdminNotificationPublisher());
  }

  private static User CreateNurseUser(
    string email,
    bool isProfileComplete,
    bool userIsActive,
    bool nurseProfileIsActive)
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
      ProfileType = UserProfileType.NURSE,
      Name = "Laura",
      LastName = "Gomez",
      IdentificationNumber = "00100000001",
      Phone = "8095550100",
      PasswordHash = "hash",
      IsActive = userIsActive,
      CreatedAtUtc = DateTime.UtcNow,
      NurseProfile = new Nurse
      {
        IsActive = nurseProfileIsActive,
        HireDate = new DateOnly(2026, 3, 21),
        Specialty = "Atencion domiciliaria",
        LicenseId = "55",
        BankName = "Banco Central",
        AccountNumber = "123456",
        Category = isProfileComplete ? "Senior" : null
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
      ProfileType = UserProfileType.CLIENT,
      Name = "CLIENT",
      LastName = "User",
      IdentificationNumber = "00100000002",
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
    private readonly Dictionary<Guid, NurseWorkloadSummary> _workloads = [];
    private readonly HashSet<Guid> _historicalCareRequestLinks = [];

    public FakeUserRepository(params User[] users)
    {
      _usersById = users.ToDictionary(user => user.Id);
      _usersByEmail = users.ToDictionary(user => user.Email, StringComparer.OrdinalIgnoreCase);
    }

    public void SetWorkload(Guid userId, NurseWorkloadSummary workload)
    {
      _workloads[userId] = workload;
      if (workload.TotalAssignedCareRequests > 0)
      {
        _historicalCareRequestLinks.Add(userId);
      }
    }

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
      => Task.FromResult(_usersByEmail.TryGetValue(email, out var user) ? user : null);

    public Task<User?> GetByGoogleSubjectIdAsync(string googleSubjectId, CancellationToken cancellationToken = default)
      => Task.FromResult<User?>(null);

    public Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default)
      => Task.FromResult(_usersById.TryGetValue(userId, out var user) ? user : null);

    public Task<bool> AnyAdminExistsAsync(CancellationToken cancellationToken = default)
      => Task.FromResult(
        _usersById.Values.Any(user =>
          user.UserRoles.Any(userRole => string.Equals(userRole.Role.Name, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase))));

    public Task<IReadOnlyList<User>> GetNurseProfilesAsync(CancellationToken cancellationToken = default)
      => Task.FromResult<IReadOnlyList<User>>(
        _usersById.Values
          .Where(user => user.ProfileType == UserProfileType.NURSE && user.NurseProfile is not null)
          .OrderBy(user => user.Name)
          .ThenBy(user => user.LastName)
          .ToArray());

    public Task<IReadOnlyList<User>> GetPendingNurseProfilesAsync(CancellationToken cancellationToken = default)
      => Task.FromResult<IReadOnlyList<User>>(Array.Empty<User>());

    public Task<IReadOnlyList<User>> GetActiveNurseProfilesAsync(CancellationToken cancellationToken = default)
      => Task.FromResult<IReadOnlyList<User>>(Array.Empty<User>());

    public Task<IReadOnlyDictionary<Guid, NurseWorkloadSummary>> GetNurseWorkloadsAsync(
      IReadOnlyCollection<Guid> nurseUserIds,
      CancellationToken cancellationToken = default)
    {
      IReadOnlyDictionary<Guid, NurseWorkloadSummary> result = _workloads
        .Where(entry => nurseUserIds.Contains(entry.Key))
        .ToDictionary(entry => entry.Key, entry => entry.Value);

      return Task.FromResult(result);
    }

    public Task<bool> HasAssignedCareRequestsAsync(Guid nurseUserId, CancellationToken cancellationToken = default)
      => Task.FromResult(_historicalCareRequestLinks.Contains(nurseUserId));

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

  private sealed class FakeRoleRepository : IRoleRepository
  {
    private readonly Dictionary<string, Role> _roles = new(StringComparer.OrdinalIgnoreCase)
    {
      [SystemRoles.Nurse] = new Role { Id = Guid.NewGuid(), Name = SystemRoles.Nurse },
      [SystemRoles.Admin] = new Role { Id = Guid.NewGuid(), Name = SystemRoles.Admin },
      [SystemRoles.Client] = new Role { Id = Guid.NewGuid(), Name = SystemRoles.Client },
    };

    public Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
      => Task.FromResult(_roles.TryGetValue(name, out var role) ? role : null);

    public Task<Role?> GetByIdAsync(Guid roleId, CancellationToken cancellationToken = default)
      => Task.FromResult(_roles.Values.FirstOrDefault(role => role.Id == roleId));

    public Task<Role> CreateAsync(Role role, CancellationToken cancellationToken = default)
    {
      _roles[role.Name] = role;
      return Task.FromResult(role);
    }

    public Task<IEnumerable<Role>> GetAllAsync(CancellationToken cancellationToken = default)
      => Task.FromResult<IEnumerable<Role>>(_roles.Values.ToArray());
  }

  private sealed class FakePasswordHasher : IPasswordHasher
  {
    public string Hash(string password)
      => $"hashed::{password}";

    public bool Verify(string password, string hash)
      => hash == Hash(password);
  }

  private sealed class FakeAdminAuditService : IAdminAuditService
  {
    public List<AdminAuditRecord> Records { get; } = [];

    public Task WriteAsync(AdminAuditRecord record, CancellationToken cancellationToken = default)
    {
      Records.Add(record);
      return Task.CompletedTask;
    }
  }
}
