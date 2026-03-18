using NursingCareBackend.Application.Identity.Authentication;
using NursingCareBackend.Application.Identity.Commands;
using NursingCareBackend.Application.Identity.Repositories;
using NursingCareBackend.Application.Identity.Services;
using NursingCareBackend.Domain.Identity;

namespace NursingCareBackend.Application.Tests;

public sealed class AuthenticationServiceTests
{
  [Fact]
  public async Task RegisterAsync_Should_Create_Inactive_Nurse_Without_Tokens()
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
      Email: "nurse@example.com",
      Password: "Pass123!",
      ConfirmPassword: "Pass123!",
      ProfileType: UserProfileType.Nurse));

    Assert.Equal(string.Empty, response.Token);
    Assert.Equal(string.Empty, response.RefreshToken);
    Assert.Null(response.ExpiresAtUtc);
    Assert.Contains("Nurse", response.Roles);

    var createdUser = Assert.Single(userRepository.CreatedUsers);
    Assert.False(createdUser.IsActive);
    Assert.Contains(createdUser.UserRoles, userRole => userRole.Role.Name == "Nurse");
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
      Name = "User"
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

  private static AuthenticationService CreateService(
    IUserRepository? userRepository = null,
    IRoleRepository? roleRepository = null,
    IRefreshTokenRepository? refreshTokenRepository = null,
    IPasswordHasher? passwordHasher = null,
    ITokenGenerator? tokenGenerator = null)
  {
    return new AuthenticationService(
      userRepository ?? new FakeUserRepository(),
      roleRepository ?? new FakeRoleRepository(),
      refreshTokenRepository ?? new FakeRefreshTokenRepository(),
      passwordHasher ?? new FakePasswordHasher(),
      tokenGenerator ?? new FakeTokenGenerator());
  }

  private static User CreateUser(string email, Role role)
  {
    var user = new User
    {
      Id = Guid.NewGuid(),
      Email = email,
      PasswordHash = "hashed-password",
      IsActive = true,
      CreatedAtUtc = DateTime.UtcNow
    };

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

    public List<User> CreatedUsers { get; } = [];

    public Task<User> CreateAsync(User user, CancellationToken cancellationToken = default)
    {
      _usersById[user.Id] = user;
      _usersByEmail[user.Email] = user;
      CreatedUsers.Add(user);
      return Task.FromResult(user);
    }

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
      => Task.FromResult(_usersByEmail.TryGetValue(email, out var user) ? user : null);

    public Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default)
      => Task.FromResult(_usersById.TryGetValue(userId, out var user) ? user : null);

    public Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
      _usersById[user.Id] = user;
      _usersByEmail[user.Email] = user;
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

    public Task RevokeActiveTokensForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
      var now = DateTime.UtcNow;

      foreach (var refreshToken in _tokens.Values.Where(token => token.UserId == userId && token.RevokedAtUtc is null))
      {
        refreshToken.RevokedAtUtc = now;
      }

      return Task.CompletedTask;
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
}
