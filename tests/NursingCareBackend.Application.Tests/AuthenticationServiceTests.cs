using NursingCareBackend.Application.AdminPortal.Notifications;
using NursingCareBackend.Application.Identity.Authentication;
using NursingCareBackend.Application.Identity.Commands;
using NursingCareBackend.Application.Identity.Models;
using NursingCareBackend.Application.Identity.OAuth;
using NursingCareBackend.Application.Identity.Repositories;
using NursingCareBackend.Application.Identity.Services;
using NursingCareBackend.Domain.Identity;

namespace NursingCareBackend.Application.Tests;

public sealed class AuthenticationServiceTests
{
  [Fact]
  public async Task RegisterAsync_Should_Create_Nurse_With_Immediate_Access_And_Admin_Review_State()
  {
    var nurseRole = new Role
    {
      Id = Guid.NewGuid(),
      Name = "Nurse"
    };

    var userRepository = new FakeUserRepository();
    var service = CreateService(
      userRepository: userRepository,
      roleRepository: new FakeRoleRepository(nurseRole));

    var response = await service.RegisterAsync(new RegisterRequest(
      Name: "Ana",
      LastName: "Lopez",
      IdentificationNumber: "00112345678",
      Phone: "8095550101",
      Email: "nurse@example.com",
      Password: "Pass123!",
      ConfirmPassword: "Pass123!",
      HireDate: new DateOnly(2026, 3, 21),
      Specialty: "Atencion domiciliaria",
      BankName: "Banco Central",
      ProfileType: UserProfileType.Nurse));

    Assert.False(string.IsNullOrWhiteSpace(response.Token));
    Assert.False(string.IsNullOrWhiteSpace(response.RefreshToken));
    Assert.NotNull(response.ExpiresAtUtc);
    Assert.Contains("Nurse", response.Roles);
    Assert.True(response.RequiresAdminReview);

    var createdUser = Assert.Single(userRepository.CreatedUsers);
    Assert.True(createdUser.IsActive);
    Assert.Equal(UserProfileType.Nurse, createdUser.ProfileType);
    Assert.Equal("Ana", createdUser.Name);
    Assert.Equal("Lopez", createdUser.LastName);
    Assert.Equal("00112345678", createdUser.IdentificationNumber);
    Assert.Equal("8095550101", createdUser.Phone);
    Assert.NotNull(createdUser.NurseProfile);
    Assert.Equal(createdUser.Id, createdUser.NurseProfile!.UserId);
    Assert.False(createdUser.NurseProfile.IsActive);
    Assert.Equal(new DateOnly(2026, 3, 21), createdUser.NurseProfile.HireDate);
    Assert.Equal("Atencion domiciliaria", createdUser.NurseProfile.Specialty);
    Assert.Equal("Banco Central", createdUser.NurseProfile.BankName);
    Assert.Null(createdUser.ClientProfile);
    Assert.Contains(createdUser.UserRoles, userRole => userRole.Role.Name == "Nurse");
  }

  [Fact]
  public async Task RegisterAsync_Should_Reject_Nurse_When_Required_Nurse_Fields_Are_Missing()
  {
    var nurseRole = new Role
    {
      Id = Guid.NewGuid(),
      Name = "Nurse"
    };

    var service = CreateService(roleRepository: new FakeRoleRepository(nurseRole));

    var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
      service.RegisterAsync(new RegisterRequest(
        Name: "Ana",
        LastName: "Lopez",
        IdentificationNumber: "00112345678",
        Phone: "8095550101",
        Email: "nurse@example.com",
        Password: "Pass123!",
        ConfirmPassword: "Pass123!",
        ProfileType: UserProfileType.Nurse)));

    Assert.Equal("Hire date is required for nurse registration. (Parameter 'HireDate')", exception.Message);
  }

  [Fact]
  public async Task RegisterAsync_Should_Reject_When_Default_Client_Role_Is_Missing()
  {
    var service = CreateService(roleRepository: new FakeRoleRepository());

    var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
      service.RegisterAsync(new RegisterRequest(
        Name: "Client",
        LastName: "User",
        IdentificationNumber: "00198765432",
        Phone: "8095550102",
        Email: "client@example.com",
        Password: "Pass123!",
        ConfirmPassword: "Pass123!",
        ProfileType: UserProfileType.Client)));

    Assert.Equal("Client role not found in the system.", exception.Message);
  }

  [Fact]
  public async Task RefreshAsync_Should_Revoke_Previous_Token_And_Issue_A_New_One()
  {
    var adminRole = new Role
    {
      Id = Guid.NewGuid(),
      Name = "Admin"
    };

    var user = CreateUser("admin@example.com", adminRole);
    var existingRefreshToken = new RefreshToken
    {
      Id = Guid.NewGuid(),
      UserId = user.Id,
      User = user,
      Token = "refresh-token-1",
      CreatedAtUtc = DateTime.UtcNow.AddDays(-1),
      ExpiresAtUtc = DateTime.UtcNow.AddDays(7)
    };

    var refreshTokenRepository = new FakeRefreshTokenRepository(existingRefreshToken);
    var service = CreateService(
      refreshTokenRepository: refreshTokenRepository,
      tokenGenerator: new FakeTokenGenerator());

    var response = await service.RefreshAsync(new RefreshTokenRequest("refresh-token-1"));

    Assert.Equal("jwt-token-1", response.Token);
    Assert.NotEqual(existingRefreshToken.Token, response.RefreshToken);
    Assert.Equal(existingRefreshToken.User.Email, response.Email);
    Assert.Contains("Admin", response.Roles);
    Assert.NotNull(existingRefreshToken.RevokedAtUtc);
    Assert.Single(refreshTokenRepository.CreatedTokens);
    Assert.Equal(response.RefreshToken, refreshTokenRepository.CreatedTokens[0].Token);
  }

  [Fact]
  public async Task RefreshAsync_Should_Reject_Reused_Refresh_Tokens()
  {
    var role = new Role
    {
      Id = Guid.NewGuid(),
      Name = "Client"
    };

    var user = CreateUser("client@example.com", role);
    var existingRefreshToken = new RefreshToken
    {
      Id = Guid.NewGuid(),
      UserId = user.Id,
      User = user,
      Token = "refresh-token-1",
      CreatedAtUtc = DateTime.UtcNow.AddDays(-1),
      ExpiresAtUtc = DateTime.UtcNow.AddDays(7)
    };

    var service = CreateService(
      refreshTokenRepository: new FakeRefreshTokenRepository(existingRefreshToken),
      tokenGenerator: new FakeTokenGenerator());

    await service.RefreshAsync(new RefreshTokenRequest("refresh-token-1"));

    var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
      service.RefreshAsync(new RefreshTokenRequest("refresh-token-1")));

    Assert.Equal("Refresh token is invalid or expired.", exception.Message);
  }

  [Fact]
  public async Task LoginWithGoogleAsync_Should_Create_A_New_Active_Client_User_Requiring_Profile_Completion()
  {
    var userRole = new Role
    {
      Id = Guid.NewGuid(),
      Name = "Client"
    };

    var userRepository = new FakeUserRepository();
    var service = CreateService(
      userRepository: userRepository,
      roleRepository: new FakeRoleRepository(userRole),
      googleOAuthClient: new FakeGoogleOAuthClient(
        new GoogleOAuthUserInfo("google-subject-1", "google-user@example.com", "Google User", true)));

    var response = await service.LoginWithGoogleAsync("google-auth-code");

    Assert.Equal("jwt-token-1", response.Token);
    Assert.Equal("google-user@example.com", response.Email);
    Assert.Contains("Client", response.Roles);
    Assert.True(response.RequiresProfileCompletion);

    var createdUser = Assert.Single(userRepository.CreatedUsers);
    Assert.False(createdUser.IsActive);
    Assert.Equal(UserProfileType.Client, createdUser.ProfileType);
    Assert.Equal("google-subject-1", createdUser.GoogleSubjectId);
    Assert.Equal("Google User", createdUser.DisplayName);
    Assert.Null(createdUser.Name);
    Assert.Null(createdUser.LastName);
    Assert.Null(createdUser.IdentificationNumber);
    Assert.Null(createdUser.Phone);
    Assert.NotNull(createdUser.ClientProfile);
    Assert.Equal(createdUser.Id, createdUser.ClientProfile!.UserId);
    Assert.Contains(createdUser.UserRoles, userRoleLink => userRoleLink.Role.Name == "Client");
  }

  [Fact]
  public async Task LoginWithGoogleAsync_Should_Link_An_Existing_Active_User_By_Email()
  {
    var userRole = new Role
    {
      Id = Guid.NewGuid(),
      Name = "Client"
    };

    var existingUser = CreateUser("existing@example.com", userRole);
    var userRepository = new FakeUserRepository(existingUser);
    var service = CreateService(
      userRepository: userRepository,
      roleRepository: new FakeRoleRepository(userRole),
      googleOAuthClient: new FakeGoogleOAuthClient(
        new GoogleOAuthUserInfo("google-subject-2", "existing@example.com", "Existing User", true)));

    var response = await service.LoginWithGoogleAsync("google-auth-code");

    Assert.Equal("existing@example.com", response.Email);
    Assert.False(response.RequiresProfileCompletion);
    Assert.Empty(userRepository.CreatedUsers);
    Assert.Equal("google-subject-2", existingUser.GoogleSubjectId);
    Assert.Equal("Existing User", existingUser.DisplayName);
  }

  [Fact]
  public async Task CompleteProfileAsync_Should_Update_Google_User_And_Clear_Profile_Completion_Flag()
  {
    var userRole = new Role
    {
      Id = Guid.NewGuid(),
      Name = "Client"
    };

    var existingUser = CreateUser("google-existing@example.com", userRole);
    existingUser.GoogleSubjectId = "google-subject-9";
    existingUser.Name = null;
    existingUser.LastName = null;
    existingUser.IdentificationNumber = null;
    existingUser.Phone = null;

    var service = CreateService(userRepository: new FakeUserRepository(existingUser));

    var response = await service.CompleteProfileAsync(
      existingUser.Id,
      new CompleteProfileRequest(
        Name: "Laura",
        LastName: "Gomez",
        IdentificationNumber: "00111111111",
        Phone: "8095550199"));

    Assert.False(response.RequiresProfileCompletion);
    Assert.True(existingUser.IsActive);
    Assert.Equal("Laura", existingUser.Name);
    Assert.Equal("Gomez", existingUser.LastName);
    Assert.Equal("00111111111", existingUser.IdentificationNumber);
    Assert.Equal("8095550199", existingUser.Phone);
  }

  [Fact]
  public async Task LoginWithGoogleAsync_Should_Reject_Inactive_Existing_User_By_Email()
  {
    var nurseRole = new Role
    {
      Id = Guid.NewGuid(),
      Name = "Nurse"
    };

    var existingUser = CreateUser("pending@example.com", nurseRole);
    existingUser.IsActive = false;

    var service = CreateService(
      userRepository: new FakeUserRepository(existingUser),
      roleRepository: new FakeRoleRepository(nurseRole),
      googleOAuthClient: new FakeGoogleOAuthClient(
        new GoogleOAuthUserInfo("google-subject-3", "pending@example.com", "Pending User", true)));

    var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
      service.LoginWithGoogleAsync("google-auth-code"));

    Assert.Equal("User account is not active.", exception.Message);
  }

  private static AuthenticationService CreateService(
    IUserRepository? userRepository = null,
    IRoleRepository? roleRepository = null,
    IRefreshTokenRepository? refreshTokenRepository = null,
    IPasswordHasher? passwordHasher = null,
    ITokenGenerator? tokenGenerator = null,
    IGoogleOAuthClient? googleOAuthClient = null,
    IAdminBootstrapPolicy? adminBootstrapPolicy = null,
    IAdminNotificationPublisher? notifications = null)
  {
    return new AuthenticationService(
      userRepository ?? new FakeUserRepository(),
      roleRepository ?? new FakeRoleRepository(),
      refreshTokenRepository ?? new FakeRefreshTokenRepository(),
      passwordHasher ?? new FakePasswordHasher(),
      tokenGenerator ?? new FakeTokenGenerator(),
      googleOAuthClient ?? new FakeGoogleOAuthClient(
        new GoogleOAuthUserInfo("default-google-subject", "default@example.com", "Default User", true)),
      adminBootstrapPolicy ?? new FakeAdminBootstrapPolicy(),
      new FakeNurseCatalogService(),
      notifications ?? new FakeAdminNotificationPublisher());
  }

  private static User CreateUser(string email, Role role)
  {
    var user = new User
    {
      Id = Guid.NewGuid(),
      ProfileType = role.Name == "Nurse" ? UserProfileType.Nurse : UserProfileType.Client,
      Name = "Existing",
      LastName = "User",
      IdentificationNumber = "00176543210",
      Phone = "8095550109",
      Email = email,
      PasswordHash = "hashed-password",
      IsActive = true,
      CreatedAtUtc = DateTime.UtcNow,
      ClientProfile = role.Name == "Nurse" ? null : new Client()
    };

    if (role.Name == "Nurse")
    {
      user.NurseProfile = new Nurse
      {
        UserId = user.Id,
        IsActive = true
      };
    }
    else
    {
      user.ClientProfile!.UserId = user.Id;
    }

    user.UserRoles.Add(new UserRole
    {
      UserId = user.Id,
      User = user,
      RoleId = role.Id,
      Role = role
    });

    return user;
  }

  private sealed class FakeUserRepository : IUserRepository
  {
    private readonly Dictionary<Guid, User> _usersById = new();
    private readonly Dictionary<string, User> _usersByEmail = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, User> _usersByGoogleSubjectId = new(StringComparer.Ordinal);

    public List<User> CreatedUsers { get; } = [];

    public FakeUserRepository(params User[] users)
    {
      foreach (var user in users)
      {
        _usersById[user.Id] = user;
        _usersByEmail[user.Email] = user;
        if (!string.IsNullOrWhiteSpace(user.GoogleSubjectId))
        {
          _usersByGoogleSubjectId[user.GoogleSubjectId] = user;
        }
      }
    }

    public Task<User> CreateAsync(User user, CancellationToken cancellationToken = default)
    {
      _usersById[user.Id] = user;
      _usersByEmail[user.Email] = user;
      if (!string.IsNullOrWhiteSpace(user.GoogleSubjectId))
      {
        _usersByGoogleSubjectId[user.GoogleSubjectId] = user;
      }

      CreatedUsers.Add(user);
      return Task.FromResult(user);
    }

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
      => Task.FromResult(_usersByEmail.TryGetValue(email, out var user) ? user : null);

    public Task<User?> GetByGoogleSubjectIdAsync(string googleSubjectId, CancellationToken cancellationToken = default)
      => Task.FromResult(_usersByGoogleSubjectId.TryGetValue(googleSubjectId, out var user) ? user : null);

    public Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default)
      => Task.FromResult(_usersById.TryGetValue(userId, out var user) ? user : null);

    public Task<bool> AnyAdminExistsAsync(CancellationToken cancellationToken = default)
      => Task.FromResult(
        _usersById.Values.Any(user =>
          user.UserRoles.Any(userRole => string.Equals(userRole.Role.Name, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase))));

    public Task<IReadOnlyList<User>> GetNurseProfilesAsync(CancellationToken cancellationToken = default)
      => Task.FromResult<IReadOnlyList<User>>(
        _usersById.Values
          .Where(user => user.ProfileType == UserProfileType.Nurse && user.NurseProfile is not null)
          .OrderBy(user => user.CreatedAtUtc)
          .ToArray());

    public Task<IReadOnlyList<User>> GetPendingNurseProfilesAsync(CancellationToken cancellationToken = default)
      => Task.FromResult<IReadOnlyList<User>>(
        _usersById.Values
          .Where(user => user.ProfileType == UserProfileType.Nurse && user.NurseProfile?.IsActive == false)
          .OrderBy(user => user.CreatedAtUtc)
          .ToArray());

    public Task<IReadOnlyList<User>> GetActiveNurseProfilesAsync(CancellationToken cancellationToken = default)
      => Task.FromResult<IReadOnlyList<User>>(
        _usersById.Values
          .Where(user => user.ProfileType == UserProfileType.Nurse && user.IsActive && user.NurseProfile?.IsActive == true)
          .OrderBy(user => user.CreatedAtUtc)
          .ToArray());

    public Task<IReadOnlyDictionary<Guid, NurseWorkloadSummary>> GetNurseWorkloadsAsync(
      IReadOnlyCollection<Guid> nurseUserIds,
      CancellationToken cancellationToken = default)
      => Task.FromResult<IReadOnlyDictionary<Guid, NurseWorkloadSummary>>(new Dictionary<Guid, NurseWorkloadSummary>());

    public Task<bool> HasAssignedCareRequestsAsync(Guid nurseUserId, CancellationToken cancellationToken = default)
      => Task.FromResult(false);

    public Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
      _usersById[user.Id] = user;
      _usersByEmail[user.Email] = user;
      if (!string.IsNullOrWhiteSpace(user.GoogleSubjectId))
      {
        _usersByGoogleSubjectId[user.GoogleSubjectId] = user;
      }

      return Task.CompletedTask;
    }
  }

  private sealed class FakeRoleRepository : IRoleRepository
  {
    private readonly Dictionary<string, Role> _rolesByName;

    public FakeRoleRepository(params Role[] roles)
    {
      _rolesByName = roles.ToDictionary(role => role.Name, StringComparer.OrdinalIgnoreCase);
    }

    public Task<Role> CreateAsync(Role role, CancellationToken cancellationToken = default)
    {
      _rolesByName[role.Name] = role;
      return Task.FromResult(role);
    }

    public Task<IEnumerable<Role>> GetAllAsync(CancellationToken cancellationToken = default)
      => Task.FromResult<IEnumerable<Role>>(_rolesByName.Values);

    public Task<Role?> GetByIdAsync(Guid roleId, CancellationToken cancellationToken = default)
      => Task.FromResult(_rolesByName.Values.FirstOrDefault(role => role.Id == roleId));

    public Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
      => Task.FromResult(_rolesByName.TryGetValue(name, out var role) ? role : null);
  }

  private sealed class FakeRefreshTokenRepository : IRefreshTokenRepository
  {
    private readonly Dictionary<string, RefreshToken> _tokens = new(StringComparer.Ordinal);

    public FakeRefreshTokenRepository(params RefreshToken[] refreshTokens)
    {
      foreach (var refreshToken in refreshTokens)
      {
        _tokens[refreshToken.Token] = refreshToken;
      }
    }

    public List<RefreshToken> CreatedTokens { get; } = [];

    public Task<RefreshToken> CreateAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default)
    {
      CreatedTokens.Add(refreshToken);
      _tokens[refreshToken.Token] = refreshToken;
      return Task.FromResult(refreshToken);
    }

    public Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default)
      => Task.FromResult(_tokens.TryGetValue(token, out var refreshToken) ? refreshToken : null);

    public Task<int> RevokeActiveTokensForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
      var now = DateTime.UtcNow;
      var revokedCount = 0;

      foreach (var refreshToken in _tokens.Values.Where(token => token.UserId == userId && token.RevokedAtUtc is null))
      {
        refreshToken.RevokedAtUtc = now;
        revokedCount++;
      }

      return Task.FromResult(revokedCount);
    }

    public Task UpdateAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default)
    {
      _tokens[refreshToken.Token] = refreshToken;
      return Task.CompletedTask;
    }
  }

  private sealed class FakePasswordHasher : IPasswordHasher
  {
    public string Hash(string password) => $"hashed::{password}";

    public bool Verify(string password, string hash) => hash == Hash(password);
  }

  private sealed class FakeTokenGenerator : ITokenGenerator
  {
    private int _calls;

    public TokenResult GenerateToken(User user)
    {
      _calls++;
      return new TokenResult(
        Token: $"jwt-token-{_calls}",
        ExpiresAtUtc: DateTime.UtcNow.AddHours(1));
    }
  }

  private sealed class FakeGoogleOAuthClient : IGoogleOAuthClient
  {
    private readonly GoogleOAuthUserInfo _userInfo;

    public FakeGoogleOAuthClient(GoogleOAuthUserInfo userInfo)
    {
      _userInfo = userInfo;
    }

    public string BuildAuthorizationUrl(string? state = null) => "https://accounts.google.com/o/oauth2/v2/auth";

    public Task<GoogleOAuthUserInfo> GetUserInfoAsync(
      string authorizationCode,
      CancellationToken cancellationToken = default)
      => Task.FromResult(_userInfo);
  }

  private sealed class FakeAdminBootstrapPolicy : IAdminBootstrapPolicy
  {
    public Task EnsureSetupAdminAllowedAsync(CancellationToken cancellationToken = default)
      => Task.CompletedTask;
  }
}
