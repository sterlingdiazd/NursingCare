using NursingCareBackend.Application.Identity.Repositories;
using NursingCareBackend.Application.Identity.Users;
using NursingCareBackend.Application.Identity.Validation;
using NursingCareBackend.Domain.Identity;

namespace NursingCareBackend.Application.AdminPortal.Users;

public sealed class AdminUserManagementService : IAdminUserManagementService
{
  private static readonly HashSet<string> SupportedRoles = new(
    [SystemRoles.Admin, SystemRoles.Client, SystemRoles.Nurse],
    StringComparer.OrdinalIgnoreCase);

  private readonly IUserRepository _userRepository;
  private readonly IRoleRepository _roleRepository;
  private readonly IRefreshTokenRepository _refreshTokenRepository;
  private readonly IAdminUserManagementRepository _adminUserManagementRepository;

  public AdminUserManagementService(
    IUserRepository userRepository,
    IRoleRepository roleRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IAdminUserManagementRepository adminUserManagementRepository)
  {
    _userRepository = userRepository;
    _roleRepository = roleRepository;
    _refreshTokenRepository = refreshTokenRepository;
    _adminUserManagementRepository = adminUserManagementRepository;
  }

  public async Task<AdminUserDetail> UpdateIdentityAsync(
    Guid userId,
    AdminUserIdentityUpdate request,
    CancellationToken cancellationToken = default)
  {
    var user = await GetRequiredUserAsync(userId, cancellationToken);
    ValidateIdentityUpdate(request);

    var normalizedEmail = request.Email.Trim();
    var existingUser = await _userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);
    if (existingUser is not null && existingUser.Id != userId)
    {
      throw new InvalidOperationException("User with this email already exists.");
    }

    user.Name = request.Name.Trim();
    user.LastName = request.LastName.Trim();
    user.IdentificationNumber = request.IdentificationNumber.Trim();
    user.Phone = request.Phone.Trim();
    user.Email = normalizedEmail;

    if (string.IsNullOrWhiteSpace(user.DisplayName))
    {
      user.DisplayName = $"{user.Name} {user.LastName}".Trim();
    }

    await _userRepository.UpdateAsync(user, cancellationToken);

    return await GetRequiredDetailAsync(userId, cancellationToken);
  }

  public async Task<AdminUserDetail> UpdateRolesAsync(
    Guid userId,
    IReadOnlyCollection<string> roleNames,
    Guid? actorUserId,
    CancellationToken cancellationToken = default)
  {
    var user = await GetRequiredUserAsync(userId, cancellationToken);
    var normalizedRoleNames = NormalizeRoleNames(roleNames);

    if (normalizedRoleNames.Count == 0)
    {
      throw new InvalidOperationException("At least one role is required.");
    }

    if (actorUserId == userId
      && user.UserRoles.Any(userRole => string.Equals(userRole.Role.Name, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase))
      && !normalizedRoleNames.Contains(SystemRoles.Admin))
    {
      throw new InvalidOperationException("The Admin role cannot be removed from your own account.");
    }

    ValidateRolePolicy(user, normalizedRoleNames);

    var desiredRoles = new List<Role>();
    foreach (var roleName in normalizedRoleNames)
    {
      var role = await _roleRepository.GetByNameAsync(roleName, cancellationToken);
      if (role is null)
      {
        throw new InvalidOperationException($"Role '{roleName}' not found.");
      }

      desiredRoles.Add(role);
    }

    var desiredRoleIds = desiredRoles.Select(role => role.Id).ToHashSet();
    var removableRoles = user.UserRoles
      .Where(userRole => !desiredRoleIds.Contains(userRole.RoleId))
      .ToList();

    foreach (var removableRole in removableRoles)
    {
      user.UserRoles.Remove(removableRole);
    }

    foreach (var desiredRole in desiredRoles)
    {
      if (user.UserRoles.Any(userRole => userRole.RoleId == desiredRole.Id))
      {
        continue;
      }

      user.UserRoles.Add(new UserRole
      {
        UserId = user.Id,
        RoleId = desiredRole.Id,
        Role = desiredRole,
      });
    }

    await _userRepository.UpdateAsync(user, cancellationToken);

    return await GetRequiredDetailAsync(userId, cancellationToken);
  }

  public async Task<AdminUserDetail> UpdateActiveStateAsync(
    Guid userId,
    bool isActive,
    Guid? actorUserId,
    CancellationToken cancellationToken = default)
  {
    var user = await GetRequiredUserAsync(userId, cancellationToken);

    if (!isActive && actorUserId == userId)
    {
      throw new InvalidOperationException("You cannot deactivate your own account.");
    }

    if (user.IsActive == isActive)
    {
      return await GetRequiredDetailAsync(userId, cancellationToken);
    }

    user.IsActive = isActive;
    await _userRepository.UpdateAsync(user, cancellationToken);

    if (!isActive)
    {
      await _refreshTokenRepository.RevokeActiveTokensForUserAsync(userId, cancellationToken);
    }

    return await GetRequiredDetailAsync(userId, cancellationToken);
  }

  public async Task<AdminUserSessionInvalidationResult> InvalidateSessionsAsync(
    Guid userId,
    CancellationToken cancellationToken = default)
  {
    await GetRequiredUserAsync(userId, cancellationToken);
    var revokedCount = await _refreshTokenRepository.RevokeActiveTokensForUserAsync(userId, cancellationToken);
    return new AdminUserSessionInvalidationResult(userId, revokedCount);
  }

  private async Task<User> GetRequiredUserAsync(Guid userId, CancellationToken cancellationToken)
  {
    if (userId == Guid.Empty)
    {
      throw new ArgumentException("User ID cannot be empty.", nameof(userId));
    }

    var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
    if (user is null)
    {
      throw new KeyNotFoundException($"User with ID {userId} not found.");
    }

    return user;
  }

  private async Task<AdminUserDetail> GetRequiredDetailAsync(Guid userId, CancellationToken cancellationToken)
  {
    var detail = await _adminUserManagementRepository.GetByIdAsync(userId, cancellationToken);
    if (detail is null)
    {
      throw new KeyNotFoundException($"User with ID {userId} not found.");
    }

    return detail;
  }

  private static void ValidateIdentityUpdate(AdminUserIdentityUpdate request)
  {
    IdentityInputRules.EnsureTextOnlyRequired(request.Name, nameof(request.Name), "Name");
    IdentityInputRules.EnsureTextOnlyRequired(request.LastName, nameof(request.LastName), "Last name");
    IdentityInputRules.EnsureIdentificationNumber(request.IdentificationNumber, nameof(request.IdentificationNumber));
    IdentityInputRules.EnsurePhone(request.Phone, nameof(request.Phone));

    if (string.IsNullOrWhiteSpace(request.Email))
    {
      throw new ArgumentException("Email is required.", nameof(request.Email));
    }
  }

  private static HashSet<string> NormalizeRoleNames(IReadOnlyCollection<string> roleNames)
  {
    return roleNames
      .Where(roleName => !string.IsNullOrWhiteSpace(roleName))
      .Select(roleName => roleName.Trim())
      .ToHashSet(StringComparer.OrdinalIgnoreCase);
  }

  private static void ValidateRolePolicy(User user, IReadOnlyCollection<string> roleNames)
  {
    foreach (var roleName in roleNames)
    {
      if (!SupportedRoles.Contains(roleName))
      {
        throw new InvalidOperationException("The selected role is not supported.");
      }

      if (string.Equals(roleName, SystemRoles.Nurse, StringComparison.OrdinalIgnoreCase)
        && user.ProfileType != UserProfileType.Nurse)
      {
        throw new InvalidOperationException("The Nurse role can only be assigned to nurse profiles.");
      }

      if (string.Equals(roleName, SystemRoles.Client, StringComparison.OrdinalIgnoreCase)
        && user.ProfileType != UserProfileType.Client)
      {
        throw new InvalidOperationException("The Client role can only be assigned to client profiles.");
      }
    }
  }
}
