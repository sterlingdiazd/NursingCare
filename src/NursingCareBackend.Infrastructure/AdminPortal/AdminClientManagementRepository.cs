using Microsoft.EntityFrameworkCore;
using NursingCareBackend.Application.AdminPortal.Clients;
using NursingCareBackend.Domain.CareRequests;
using NursingCareBackend.Domain.Identity;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Infrastructure.AdminPortal;

public sealed class AdminClientManagementRepository : IAdminClientManagementRepository
{
  private readonly NursingCareDbContext _dbContext;

  public AdminClientManagementRepository(NursingCareDbContext dbContext)
  {
    _dbContext = dbContext;
  }

  public async Task<IReadOnlyList<AdminClientListItem>> GetListAsync(
    AdminClientListFilter filter,
    CancellationToken cancellationToken = default)
  {
    var query = _dbContext.Users
      .AsNoTracking()
      .Where(user =>
        user.ProfileType == UserProfileType.Client
        && user.ClientProfile != null
        && user.UserRoles.Any(userRole => userRole.Role.Name == SystemRoles.Client));

    if (!string.IsNullOrWhiteSpace(filter.Status))
    {
      var normalizedStatus = filter.Status.Trim().ToLowerInvariant();
      query = normalizedStatus switch
      {
        "active" => query.Where(user => user.IsActive),
        "inactive" => query.Where(user => !user.IsActive),
        _ => query
      };
    }

    if (!string.IsNullOrWhiteSpace(filter.Search))
    {
      var normalizedSearch = filter.Search.Trim().ToLowerInvariant();
      query = query.Where(user =>
        user.Email.ToLower().Contains(normalizedSearch)
        || (user.Name != null && user.Name.ToLower().Contains(normalizedSearch))
        || (user.LastName != null && user.LastName.ToLower().Contains(normalizedSearch))
        || (user.IdentificationNumber != null && user.IdentificationNumber.Contains(normalizedSearch))
        || (user.Phone != null && user.Phone.Contains(normalizedSearch)));
    }

    var users = await query
      .OrderBy(user => user.Name)
      .ThenBy(user => user.LastName)
      .ThenBy(user => user.Email)
      .Select(user => new ClientUserRow(
        user.Id,
        user.Email,
        user.Name,
        user.LastName,
        user.IdentificationNumber,
        user.Phone,
        user.IsActive,
        user.CreatedAtUtc))
      .ToListAsync(cancellationToken);

    var careRequestStats = await GetClientCareRequestStatsAsync(
      users.Select(user => user.UserId).ToArray(),
      cancellationToken);

    return users
      .Select(user =>
      {
        careRequestStats.TryGetValue(user.UserId, out var stats);

        return new AdminClientListItem(
          user.UserId,
          user.Email,
          ResolveDisplayName(user),
          user.Name,
          user.LastName,
          user.IdentificationNumber,
          user.Phone,
          user.IsActive,
          stats?.OwnedCareRequestsCount ?? 0,
          stats?.LastCareRequestAtUtc,
          user.CreatedAtUtc);
      })
      .ToList()
      .AsReadOnly();
  }

  public async Task<AdminClientDetail?> GetByIdAsync(
    Guid userId,
    CancellationToken cancellationToken = default)
  {
    var user = await _dbContext.Users
      .AsNoTracking()
      .Where(item =>
        item.Id == userId
        && item.ProfileType == UserProfileType.Client
        && item.ClientProfile != null
        && item.UserRoles.Any(userRole => userRole.Role.Name == SystemRoles.Client))
      .Select(item => new ClientUserRow(
        item.Id,
        item.Email,
        item.Name,
        item.LastName,
        item.IdentificationNumber,
        item.Phone,
        item.IsActive,
        item.CreatedAtUtc))
      .FirstOrDefaultAsync(cancellationToken);

    if (user is null)
    {
      return null;
    }

    var careRequestStats = await GetClientCareRequestStatsAsync([user.UserId], cancellationToken);
    careRequestStats.TryGetValue(user.UserId, out var stats);

    var historyRows = await _dbContext.CareRequests
      .AsNoTracking()
      .Where(careRequest => careRequest.UserID == userId)
      .OrderByDescending(careRequest => careRequest.UpdatedAtUtc)
      .Select(careRequest => new ClientCareRequestRow(
        careRequest.Id,
        careRequest.Description,
        careRequest.CareRequestType,
        careRequest.Status.ToString(),
        careRequest.Total,
        careRequest.CareRequestDate,
        careRequest.CreatedAtUtc,
        careRequest.UpdatedAtUtc,
        careRequest.AssignedNurse))
      .ToListAsync(cancellationToken);

    var nurseLookup = await LoadUserLookupAsync(
      historyRows
        .Where(item => item.AssignedNurseUserId.HasValue)
        .Select(item => item.AssignedNurseUserId!.Value)
        .Distinct()
        .ToArray(),
      cancellationToken);

    return new AdminClientDetail(
      user.UserId,
      user.Email,
      ResolveDisplayName(user),
      user.Name,
      user.LastName,
      user.IdentificationNumber,
      user.Phone,
      user.IsActive,
      stats?.OwnedCareRequestsCount ?? 0,
      stats?.LastCareRequestAtUtc,
      (stats?.OwnedCareRequestsCount ?? 0) > 0,
      user.IsActive,
      user.CreatedAtUtc,
      historyRows
        .Select(item =>
        {
          UserLookup? assignedNurse = null;
          if (item.AssignedNurseUserId.HasValue)
          {
            nurseLookup.TryGetValue(item.AssignedNurseUserId.Value, out assignedNurse);
          }

          return new AdminClientCareRequestHistoryItem(
            item.CareRequestId,
            item.CareRequestDescription,
            item.CareRequestType,
            item.Status,
            item.Total,
            item.CareRequestDate,
            item.CreatedAtUtc,
            item.UpdatedAtUtc,
            assignedNurse is null ? null : ResolveDisplayName(assignedNurse),
            assignedNurse?.Email);
        })
        .ToList()
        .AsReadOnly());
  }

  private async Task<IReadOnlyDictionary<Guid, ClientCareRequestStats>> GetClientCareRequestStatsAsync(
    IReadOnlyCollection<Guid> clientUserIds,
    CancellationToken cancellationToken)
  {
    if (clientUserIds.Count == 0)
    {
      return new Dictionary<Guid, ClientCareRequestStats>();
    }

    return await _dbContext.CareRequests
      .AsNoTracking()
      .Where(careRequest => clientUserIds.Contains(careRequest.UserID))
      .GroupBy(careRequest => careRequest.UserID)
      .Select(group => new ClientCareRequestStats(
        group.Key,
        group.Count(),
        group.Max(careRequest => (DateTime?)careRequest.UpdatedAtUtc)))
      .ToDictionaryAsync(item => item.UserId, cancellationToken);
  }

  private async Task<IReadOnlyDictionary<Guid, UserLookup>> LoadUserLookupAsync(
    IReadOnlyCollection<Guid> userIds,
    CancellationToken cancellationToken)
  {
    if (userIds.Count == 0)
    {
      return new Dictionary<Guid, UserLookup>();
    }

    return await _dbContext.Users
      .AsNoTracking()
      .Where(user => userIds.Contains(user.Id))
      .Select(user => new UserLookup(
        user.Id,
        user.Email,
        user.Name,
        user.LastName))
      .ToDictionaryAsync(user => user.UserId, cancellationToken);
  }

  private static string ResolveDisplayName(ClientUserRow user)
  {
    var displayName = string.Join(
      " ",
      new[] { user.Name, user.LastName }.Where(value => !string.IsNullOrWhiteSpace(value)))
      .Trim();

    return displayName.Length > 0 ? displayName : user.Email;
  }

  private static string ResolveDisplayName(UserLookup user)
  {
    var displayName = string.Join(
      " ",
      new[] { user.Name, user.LastName }.Where(value => !string.IsNullOrWhiteSpace(value)))
      .Trim();

    return displayName.Length > 0 ? displayName : user.Email;
  }

  private sealed record ClientUserRow(
    Guid UserId,
    string Email,
    string? Name,
    string? LastName,
    string? IdentificationNumber,
    string? Phone,
    bool IsActive,
    DateTime CreatedAtUtc);

  private sealed record ClientCareRequestStats(
    Guid UserId,
    int OwnedCareRequestsCount,
    DateTime? LastCareRequestAtUtc);

  private sealed record ClientCareRequestRow(
    Guid CareRequestId,
    string CareRequestDescription,
    string CareRequestType,
    string Status,
    decimal Total,
    DateOnly? CareRequestDate,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    Guid? AssignedNurseUserId);

  private sealed record UserLookup(
    Guid UserId,
    string Email,
    string? Name,
    string? LastName);
}
