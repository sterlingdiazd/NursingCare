using Microsoft.EntityFrameworkCore;
using NursingCareBackend.Application.AdminPortal.Users;
using NursingCareBackend.Application.Catalogs;
using NursingCareBackend.Application.Identity.Users;
using NursingCareBackend.Domain.Identity;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Infrastructure.AdminPortal;

public sealed class AdminUserManagementRepository : IAdminUserManagementRepository
{
  private readonly NursingCareDbContext _dbContext;
  private readonly INurseCatalogService _nurseCatalog;

  public AdminUserManagementRepository(NursingCareDbContext dbContext, INurseCatalogService nurseCatalog)
  {
    _dbContext = dbContext;
    _nurseCatalog = nurseCatalog;
  }

  public async Task<IReadOnlyList<AdminUserListItem>> GetListAsync(
    AdminUserListFilter filter,
    CancellationToken cancellationToken = default)
  {
    var users = await _dbContext.Users
      .AsNoTracking()
      .Include(user => user.UserRoles)
      .ThenInclude(userRole => userRole.Role)
      .Include(user => user.NurseProfile)
      .Include(user => user.ClientProfile)
      .OrderByDescending(user => user.CreatedAtUtc)
      .ToListAsync(cancellationToken);

    var items = users
      .Select(MapListItem)
      .Where(item => MatchesFilter(item, filter))
      .ToList();

    return items.AsReadOnly();
  }

  public async Task<AdminUserDetail?> GetByIdAsync(
    Guid userId,
    CancellationToken cancellationToken = default)
  {
    var user = await _dbContext.Users
      .AsNoTracking()
      .Include(item => item.UserRoles)
      .ThenInclude(userRole => userRole.Role)
      .Include(item => item.NurseProfile)
      .Include(item => item.ClientProfile)
      .FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);

    if (user is null)
    {
      return null;
    }

    var utcNow = DateTime.UtcNow;
    var activeRefreshTokenCount = await _dbContext.RefreshTokens
      .AsNoTracking()
      .CountAsync(
        item => item.UserId == userId && item.RevokedAtUtc == null && item.ExpiresAtUtc > utcNow,
        cancellationToken);

    var ownedCareRequestsCount = await _dbContext.CareRequests
      .AsNoTracking()
      .CountAsync(item => item.UserID == userId, cancellationToken);

    var assignedCareRequestsCount = await _dbContext.CareRequests
      .AsNoTracking()
      .CountAsync(item => item.AssignedNurse == userId, cancellationToken);

    return await MapDetailAsync(user, activeRefreshTokenCount, ownedCareRequestsCount, assignedCareRequestsCount, cancellationToken);
  }

  private static AdminUserListItem MapListItem(User user)
  {
    var roleNames = ResolveRoleNames(user);
    var requiresProfileCompletion = UserAccountStateEvaluator.RequiresProfileCompletion(user);
    var requiresAdminReview = UserAccountStateEvaluator.RequiresAdminReview(user);
    var requiresManualIntervention = UserAccountStateEvaluator.RequiresManualIntervention(user);

    return new AdminUserListItem(
      Id: user.Id,
      Email: user.Email,
      DisplayName: ResolveDisplayName(user),
      Name: user.Name,
      LastName: user.LastName,
      IdentificationNumber: user.IdentificationNumber,
      Phone: user.Phone,
      ProfileType: user.ProfileType.ToString(),
      RoleNames: roleNames,
      IsActive: user.IsActive,
      AccountStatus: ResolveAccountStatus(user, requiresProfileCompletion, requiresAdminReview, requiresManualIntervention),
      RequiresProfileCompletion: requiresProfileCompletion,
      RequiresAdminReview: requiresAdminReview,
      RequiresManualIntervention: requiresManualIntervention,
      CreatedAtUtc: user.CreatedAtUtc);
  }

  private async Task<AdminUserDetail> MapDetailAsync(
    User user,
    int activeRefreshTokenCount,
    int ownedCareRequestsCount,
    int assignedCareRequestsCount,
    CancellationToken cancellationToken)
  {
    var roleNames = ResolveRoleNames(user);
    var requiresProfileCompletion = UserAccountStateEvaluator.RequiresProfileCompletion(user);
    var requiresAdminReview = UserAccountStateEvaluator.RequiresAdminReview(user);
    var requiresManualIntervention = UserAccountStateEvaluator.RequiresManualIntervention(user);

    string? nurseSpecialty = null;
    string? nurseCategory = null;
    if (user.NurseProfile is not null)
    {
      nurseSpecialty = await _nurseCatalog.NormalizeSpecialtyAsync(user.NurseProfile.Specialty, cancellationToken);
      nurseCategory = await _nurseCatalog.NormalizeCategoryAsync(user.NurseProfile.Category, cancellationToken);
    }

    return new AdminUserDetail(
      Id: user.Id,
      Email: user.Email,
      DisplayName: ResolveDisplayName(user),
      Name: user.Name,
      LastName: user.LastName,
      IdentificationNumber: user.IdentificationNumber,
      Phone: user.Phone,
      ProfileType: user.ProfileType.ToString(),
      RoleNames: roleNames,
      AllowedRoleNames: ResolveAllowedRoleNames(user.ProfileType),
      IsActive: user.IsActive,
      AccountStatus: ResolveAccountStatus(user, requiresProfileCompletion, requiresAdminReview, requiresManualIntervention),
      RequiresProfileCompletion: requiresProfileCompletion,
      RequiresAdminReview: requiresAdminReview,
      RequiresManualIntervention: requiresManualIntervention,
      HasOperationalHistory: ownedCareRequestsCount > 0 || assignedCareRequestsCount > 0,
      ActiveRefreshTokenCount: activeRefreshTokenCount,
      CreatedAtUtc: user.CreatedAtUtc,
      NurseProfile: user.NurseProfile is null
        ? null
        : new AdminUserNurseProfile(
          user.NurseProfile.IsActive,
          user.NurseProfile.HireDate,
          nurseSpecialty,
          user.NurseProfile.LicenseId,
          user.NurseProfile.BankName,
          user.NurseProfile.AccountNumber,
          nurseCategory,
          assignedCareRequestsCount),
      ClientProfile: user.ClientProfile is null
        ? null
        : new AdminUserClientProfile(ownedCareRequestsCount));
  }

  private static bool MatchesFilter(AdminUserListItem item, AdminUserListFilter filter)
  {
    if (!string.IsNullOrWhiteSpace(filter.RoleName)
      && !item.RoleNames.Any(roleName => string.Equals(roleName, filter.RoleName.Trim(), StringComparison.OrdinalIgnoreCase)))
    {
      return false;
    }

    if (!string.IsNullOrWhiteSpace(filter.ProfileType)
      && !string.Equals(item.ProfileType, filter.ProfileType.Trim(), StringComparison.OrdinalIgnoreCase))
    {
      return false;
    }

    if (!string.IsNullOrWhiteSpace(filter.Status)
      && !string.Equals(item.AccountStatus, filter.Status.Trim(), StringComparison.OrdinalIgnoreCase))
    {
      return false;
    }

    if (string.IsNullOrWhiteSpace(filter.Search))
    {
      return true;
    }

    var normalizedSearch = filter.Search.Trim().ToLowerInvariant();
    var searchableValues = new[]
    {
      item.Id.ToString(),
      item.Email,
      item.DisplayName,
      item.Name,
      item.LastName,
      item.IdentificationNumber,
      item.Phone,
      item.ProfileType,
      GetProfileTypeLabel(item.ProfileType),
      item.AccountStatus,
      GetAccountStatusLabel(item.AccountStatus),
      string.Join(" ", item.RoleNames),
      string.Join(" ", item.RoleNames.Select(GetRoleLabel)),
    };

    return searchableValues
      .Where(value => !string.IsNullOrWhiteSpace(value))
      .Any(value => value!.ToLowerInvariant().Contains(normalizedSearch));
  }

  private static IReadOnlyList<string> ResolveRoleNames(User user)
  {
    return user.UserRoles
      .Select(userRole => userRole.Role.Name)
      .Distinct(StringComparer.OrdinalIgnoreCase)
      .OrderBy(GetRoleRank)
      .ThenBy(roleName => roleName, StringComparer.OrdinalIgnoreCase)
      .ToList()
      .AsReadOnly();
  }

  private static IReadOnlyList<string> ResolveAllowedRoleNames(UserProfileType profileType)
  {
    return profileType == UserProfileType.Nurse
      ? new[] { SystemRoles.Admin, SystemRoles.Nurse }
      : new[] { SystemRoles.Admin, SystemRoles.Client };
  }

  private static string ResolveAccountStatus(
    User user,
    bool requiresProfileCompletion,
    bool requiresAdminReview,
    bool requiresManualIntervention)
  {
    if (requiresManualIntervention)
    {
      return AdminUserAccountStatuses.ManualIntervention;
    }

    if (requiresProfileCompletion)
    {
      return AdminUserAccountStatuses.ProfileIncomplete;
    }

    if (requiresAdminReview)
    {
      return AdminUserAccountStatuses.AdminReview;
    }

    return user.IsActive ? AdminUserAccountStatuses.Active : AdminUserAccountStatuses.Inactive;
  }

  private static string ResolveDisplayName(User user)
  {
    var displayName = string.Join(
      " ",
      new[] { user.Name, user.LastName }.Where(value => !string.IsNullOrWhiteSpace(value)))
      .Trim();

    return displayName.Length > 0 ? displayName : user.Email;
  }

  private static string GetRoleLabel(string roleName)
  {
    return roleName switch
    {
      SystemRoles.Admin => "administracion",
      SystemRoles.Client => "cliente",
      SystemRoles.Nurse => "enfermeria",
      _ => "rol desconocido",
    };
  }

  private static string GetProfileTypeLabel(string profileType)
  {
    return profileType switch
    {
      nameof(UserProfileType.Nurse) => "enfermeria",
      _ => "cliente",
    };
  }

  private static string GetAccountStatusLabel(string status)
  {
    return status switch
    {
      AdminUserAccountStatuses.Inactive => "inactiva",
      AdminUserAccountStatuses.ProfileIncomplete => "perfil incompleto",
      AdminUserAccountStatuses.AdminReview => "en revision administrativa",
      AdminUserAccountStatuses.ManualIntervention => "requiere intervencion manual",
      _ => "activa",
    };
  }

  private static int GetRoleRank(string roleName)
  {
    return roleName switch
    {
      SystemRoles.Admin => 0,
      SystemRoles.Client => 1,
      SystemRoles.Nurse => 2,
      _ => 3,
    };
  }
}
