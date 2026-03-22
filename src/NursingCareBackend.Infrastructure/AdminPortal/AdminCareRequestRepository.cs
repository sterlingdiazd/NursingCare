using Microsoft.EntityFrameworkCore;
using NursingCareBackend.Application.AdminPortal.Queries;
using NursingCareBackend.Domain.CareRequests;
using NursingCareBackend.Domain.Identity;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Infrastructure.AdminPortal;

public sealed class AdminCareRequestRepository : IAdminCareRequestRepository
{
  private readonly NursingCareDbContext _dbContext;

  public AdminCareRequestRepository(NursingCareDbContext dbContext)
  {
    _dbContext = dbContext;
  }

  public async Task<IReadOnlyList<AdminCareRequestListItem>> GetListAsync(
    AdminCareRequestListFilter filter,
    CancellationToken cancellationToken)
  {
    var utcNow = DateTime.UtcNow;
    var careRequests = await _dbContext.CareRequests
      .AsNoTracking()
      .ToListAsync(cancellationToken);

    var userLookup = await LoadUserLookupAsync(careRequests, cancellationToken);
    var items = careRequests
      .Select(careRequest => ToListItem(careRequest, userLookup, utcNow))
      .Where(item => MatchesFilter(item, filter, utcNow))
      .ToList();

    return ApplySort(items, filter.Sort)
      .ToList()
      .AsReadOnly();
  }

  public async Task<AdminCareRequestDetail?> GetByIdAsync(
    Guid careRequestId,
    CancellationToken cancellationToken)
  {
    var utcNow = DateTime.UtcNow;
    var careRequest = await _dbContext.CareRequests
      .AsNoTracking()
      .FirstOrDefaultAsync(item => item.Id == careRequestId, cancellationToken);

    if (careRequest is null)
    {
      return null;
    }

    var users = await LoadUserLookupAsync(new[] { careRequest }, cancellationToken);
    var client = users[careRequest.UserID];
    var assignedNurse = careRequest.AssignedNurse.HasValue && users.TryGetValue(careRequest.AssignedNurse.Value, out var nurse)
      ? nurse
      : null;

    return new AdminCareRequestDetail(
      Id: careRequest.Id,
      ClientUserId: careRequest.UserID,
      ClientDisplayName: ResolveDisplayName(client),
      ClientEmail: client.Email,
      ClientIdentificationNumber: client.IdentificationNumber,
      AssignedNurseUserId: careRequest.AssignedNurse,
      AssignedNurseDisplayName: assignedNurse is null ? null : ResolveDisplayName(assignedNurse),
      AssignedNurseEmail: assignedNurse?.Email,
      CareRequestDescription: careRequest.Description,
      CareRequestType: careRequest.CareRequestType,
      Unit: careRequest.Unit,
      UnitType: careRequest.UnitType,
      Price: careRequest.Price,
      Total: careRequest.Total,
      DistanceFactor: careRequest.DistanceFactor,
      ComplexityLevel: careRequest.ComplexityLevel,
      ClientBasePrice: careRequest.ClientBasePrice,
      MedicalSuppliesCost: careRequest.MedicalSuppliesCost,
      CareRequestDate: careRequest.CareRequestDate,
      SuggestedNurse: careRequest.SuggestedNurse,
      Status: careRequest.Status.ToString(),
      CreatedAtUtc: careRequest.CreatedAtUtc,
      UpdatedAtUtc: careRequest.UpdatedAtUtc,
      ApprovedAtUtc: careRequest.ApprovedAtUtc,
      RejectedAtUtc: careRequest.RejectedAtUtc,
      CompletedAtUtc: careRequest.CompletedAtUtc,
      IsOverdueOrStale: IsOverdueOrStale(careRequest, utcNow),
      PricingBreakdown: BuildPricingBreakdown(careRequest),
      Timeline: BuildTimeline(careRequest));
  }

  public async Task<IReadOnlyList<AdminCareRequestClientOption>> GetActiveClientOptionsAsync(
    string? search,
    CancellationToken cancellationToken)
  {
    var normalizedSearch = search?.Trim().ToLowerInvariant();

    var query = _dbContext.Users
      .AsNoTracking()
      .Where(user =>
        user.ProfileType == UserProfileType.Client
        && user.IsActive
        && user.ClientProfile != null
        && user.UserRoles.Any(userRole => userRole.Role.Name == SystemRoles.Client));

    if (!string.IsNullOrWhiteSpace(normalizedSearch))
    {
      query = query.Where(user =>
        user.Email.ToLower().Contains(normalizedSearch)
        || (user.Name != null && user.Name.ToLower().Contains(normalizedSearch))
        || (user.LastName != null && user.LastName.ToLower().Contains(normalizedSearch))
        || (user.IdentificationNumber != null && user.IdentificationNumber.Contains(normalizedSearch)));
    }

    var users = await query
      .OrderBy(user => user.Name)
      .ThenBy(user => user.LastName)
      .Select(user => new UserLookup(
        user.Id,
        user.Email,
        user.Name,
        user.LastName,
        user.IdentificationNumber))
      .Take(50)
      .ToListAsync(cancellationToken);

    return users
      .Select(user => new AdminCareRequestClientOption(
        user.UserId,
        ResolveDisplayName(user),
        user.Email,
        user.IdentificationNumber))
      .ToList()
      .AsReadOnly();
  }

  private async Task<IReadOnlyDictionary<Guid, UserLookup>> LoadUserLookupAsync(
    IEnumerable<CareRequest> careRequests,
    CancellationToken cancellationToken)
  {
    var userIds = careRequests
      .Select(item => item.UserID)
      .Concat(careRequests.Where(item => item.AssignedNurse.HasValue).Select(item => item.AssignedNurse!.Value))
      .Distinct()
      .ToList();

    return await _dbContext.Users
      .AsNoTracking()
      .Where(user => userIds.Contains(user.Id))
      .Select(user => new UserLookup(
        user.Id,
        user.Email,
        user.Name,
        user.LastName,
        user.IdentificationNumber))
      .ToDictionaryAsync(user => user.UserId, cancellationToken);
  }

  private static AdminCareRequestListItem ToListItem(
    CareRequest careRequest,
    IReadOnlyDictionary<Guid, UserLookup> users,
    DateTime utcNow)
  {
    var client = users[careRequest.UserID];
    var assignedNurse = careRequest.AssignedNurse.HasValue && users.TryGetValue(careRequest.AssignedNurse.Value, out var nurse)
      ? nurse
      : null;

    return new AdminCareRequestListItem(
      Id: careRequest.Id,
      ClientUserId: careRequest.UserID,
      ClientDisplayName: ResolveDisplayName(client),
      ClientEmail: client.Email,
      AssignedNurseUserId: careRequest.AssignedNurse,
      AssignedNurseDisplayName: assignedNurse is null ? null : ResolveDisplayName(assignedNurse),
      AssignedNurseEmail: assignedNurse?.Email,
      CareRequestDescription: careRequest.Description,
      CareRequestType: careRequest.CareRequestType,
      Unit: careRequest.Unit,
      UnitType: careRequest.UnitType,
      Total: careRequest.Total,
      CareRequestDate: careRequest.CareRequestDate,
      Status: careRequest.Status.ToString(),
      CreatedAtUtc: careRequest.CreatedAtUtc,
      UpdatedAtUtc: careRequest.UpdatedAtUtc,
      RejectedAtUtc: careRequest.RejectedAtUtc,
      IsOverdueOrStale: IsOverdueOrStale(careRequest, utcNow));
  }

  private static bool MatchesFilter(
    AdminCareRequestListItem item,
    AdminCareRequestListFilter filter,
    DateTime utcNow)
  {
    if (!MatchesView(item, filter.View, utcNow))
    {
      return false;
    }

    if (filter.ScheduledFrom.HasValue)
    {
      if (!item.CareRequestDate.HasValue || item.CareRequestDate.Value < filter.ScheduledFrom.Value)
      {
        return false;
      }
    }

    if (filter.ScheduledTo.HasValue)
    {
      if (!item.CareRequestDate.HasValue || item.CareRequestDate.Value > filter.ScheduledTo.Value)
      {
        return false;
      }
    }

    if (string.IsNullOrWhiteSpace(filter.Search))
    {
      return true;
    }

    var normalizedSearch = filter.Search.Trim().ToLowerInvariant();

    return item.Id.ToString().ToLowerInvariant().Contains(normalizedSearch)
      || item.CareRequestDescription.ToLowerInvariant().Contains(normalizedSearch)
      || item.CareRequestType.ToLowerInvariant().Contains(normalizedSearch)
      || GetCareRequestTypeLabel(item.CareRequestType).Contains(normalizedSearch)
      || item.Status.ToLowerInvariant().Contains(normalizedSearch)
      || GetStatusLabel(item.Status).Contains(normalizedSearch)
      || item.ClientDisplayName.ToLowerInvariant().Contains(normalizedSearch)
      || item.ClientEmail.ToLowerInvariant().Contains(normalizedSearch)
      || (item.AssignedNurseDisplayName?.ToLowerInvariant().Contains(normalizedSearch) ?? false)
      || (item.AssignedNurseEmail?.ToLowerInvariant().Contains(normalizedSearch) ?? false)
      || (item.CareRequestDate?.ToString("yyyy-MM-dd").Contains(normalizedSearch) ?? false)
      || item.CreatedAtUtc.ToString("yyyy-MM-dd").Contains(normalizedSearch);
  }

  private static bool MatchesView(
    AdminCareRequestListItem item,
    string? view,
    DateTime utcNow)
  {
    var normalizedView = (view ?? "all").Trim().ToLowerInvariant();
    var todayUtc = utcNow.Date;

    return normalizedView switch
    {
      "pending" => item.Status == CareRequestStatus.Pending.ToString(),
      "approved" or "approved-incomplete" => item.Status == CareRequestStatus.Approved.ToString(),
      "rejected" => item.Status == CareRequestStatus.Rejected.ToString(),
      "rejected-today" => item.Status == CareRequestStatus.Rejected.ToString()
        && item.RejectedAtUtc.HasValue
        && item.RejectedAtUtc.Value >= todayUtc
        && item.RejectedAtUtc.Value < todayUtc.AddDays(1),
      "completed" => item.Status == CareRequestStatus.Completed.ToString(),
      "unassigned" => item.Status == CareRequestStatus.Pending.ToString() && !item.AssignedNurseUserId.HasValue,
      "pending-approval" => item.Status == CareRequestStatus.Pending.ToString() && item.AssignedNurseUserId.HasValue,
      "overdue" => item.IsOverdueOrStale,
      _ => true,
    };
  }

  private static IEnumerable<AdminCareRequestListItem> ApplySort(
    IEnumerable<AdminCareRequestListItem> items,
    string? sort)
  {
    return (sort ?? "newest").Trim().ToLowerInvariant() switch
    {
      "oldest" => items.OrderBy(item => item.CreatedAtUtc),
      "scheduled" => items
        .OrderBy(item => item.CareRequestDate.HasValue ? 0 : 1)
        .ThenBy(item => item.CareRequestDate)
        .ThenByDescending(item => item.CreatedAtUtc),
      "status" => items
        .OrderBy(item => GetStatusRank(item.Status))
        .ThenByDescending(item => item.UpdatedAtUtc),
      "value" => items
        .OrderByDescending(item => item.Total)
        .ThenByDescending(item => item.CreatedAtUtc),
      _ => items.OrderByDescending(item => item.CreatedAtUtc),
    };
  }

  private static AdminCareRequestPricingBreakdown BuildPricingBreakdown(CareRequest careRequest)
  {
    var careRequestType = CareRequest.CareRequestTypes[careRequest.CareRequestType];
    var category = careRequestType.Category;
    var categoryFactor = CareRequest.CategoryComplexity.TryGetValue(category, out var resolvedCategoryFactor)
      ? resolvedCategoryFactor
      : 1.0m;
    var distanceFactorValue = !string.IsNullOrWhiteSpace(careRequest.DistanceFactor)
      && CareRequest.DistanceFactors.TryGetValue(careRequest.DistanceFactor, out var resolvedDistanceFactor)
        ? resolvedDistanceFactor
        : 1.0m;
    var complexityFactorValue = !string.IsNullOrWhiteSpace(careRequest.ComplexityLevel)
      && CareRequest.ComplexityFactors.TryGetValue(careRequest.ComplexityLevel, out var resolvedComplexityFactor)
        ? resolvedComplexityFactor
        : 1.0m;
    var medicalSuppliesCost = careRequest.MedicalSuppliesCost ?? 0m;
    var subtotalBeforeSupplies = decimal.Round(
      Math.Max(0m, careRequest.Total - medicalSuppliesCost),
      2,
      MidpointRounding.AwayFromZero);
    var undiscountedSubtotal = decimal.Round(
      careRequest.Price * categoryFactor * distanceFactorValue * complexityFactorValue * careRequest.Unit,
      2,
      MidpointRounding.AwayFromZero);
    var volumeDiscountPercent = undiscountedSubtotal <= 0m
      ? 0m
      : decimal.Round(
        Math.Clamp((1 - (subtotalBeforeSupplies / undiscountedSubtotal)) * 100m, 0m, 100m),
        2,
        MidpointRounding.AwayFromZero);

    return new AdminCareRequestPricingBreakdown(
      Category: category,
      BasePrice: careRequest.Price,
      CategoryFactor: categoryFactor,
      DistanceFactor: careRequest.DistanceFactor,
      DistanceFactorValue: distanceFactorValue,
      ComplexityLevel: careRequest.ComplexityLevel,
      ComplexityFactorValue: complexityFactorValue,
      VolumeDiscountPercent: volumeDiscountPercent,
      SubtotalBeforeSupplies: subtotalBeforeSupplies,
      MedicalSuppliesCost: medicalSuppliesCost,
      Total: careRequest.Total);
  }

  private static IReadOnlyList<AdminCareRequestTimelineEvent> BuildTimeline(CareRequest careRequest)
  {
    var timeline = new List<AdminCareRequestTimelineEvent>
    {
      new(
        Id: $"created:{careRequest.Id}",
        Title: "Solicitud creada",
        Description: "La solicitud entro en la cola administrativa.",
        OccurredAtUtc: careRequest.CreatedAtUtc),
    };

    if (careRequest.ApprovedAtUtc.HasValue)
    {
      timeline.Add(new AdminCareRequestTimelineEvent(
        Id: $"approved:{careRequest.Id}",
        Title: "Solicitud aprobada",
        Description: "Administracion aprobo la solicitud para ejecucion operativa.",
        OccurredAtUtc: careRequest.ApprovedAtUtc.Value));
    }

    if (careRequest.RejectedAtUtc.HasValue)
    {
      timeline.Add(new AdminCareRequestTimelineEvent(
        Id: $"rejected:{careRequest.Id}",
        Title: "Solicitud rechazada",
        Description: "Administracion rechazo la solicitud.",
        OccurredAtUtc: careRequest.RejectedAtUtc.Value));
    }

    if (careRequest.CompletedAtUtc.HasValue)
    {
      timeline.Add(new AdminCareRequestTimelineEvent(
        Id: $"completed:{careRequest.Id}",
        Title: "Solicitud completada",
        Description: "La enfermera asignada marco la solicitud como completada.",
        OccurredAtUtc: careRequest.CompletedAtUtc.Value));
    }

    return timeline
      .OrderBy(item => item.OccurredAtUtc)
      .ToList()
      .AsReadOnly();
  }

  private static bool IsOverdueOrStale(CareRequest careRequest, DateTime utcNow)
  {
    if (careRequest.Status == CareRequestStatus.Completed)
    {
      return false;
    }

    var currentCareDate = DateOnly.FromDateTime(utcNow);
    var staleCutoffUtc = utcNow.AddHours(-48);

    if (careRequest.CareRequestDate.HasValue)
    {
      return careRequest.CareRequestDate.Value < currentCareDate;
    }

    return careRequest.Status == CareRequestStatus.Pending && careRequest.UpdatedAtUtc <= staleCutoffUtc;
  }

  private static int GetStatusRank(string status)
  {
    return status switch
    {
      "Pending" => 0,
      "Approved" => 1,
      "Rejected" => 2,
      "Completed" => 3,
      _ => 4,
    };
  }

  private static string GetStatusLabel(string status)
  {
    return status switch
    {
      "Approved" => "aprobada",
      "Rejected" => "rechazada",
      "Completed" => "completada",
      _ => "pendiente",
    };
  }

  private static string GetCareRequestTypeLabel(string careRequestType)
  {
    return careRequestType switch
    {
      "hogar_diario" => "hogar diario",
      "hogar_basico" => "hogar basico",
      "hogar_estandar" => "hogar estandar",
      "hogar_premium" => "hogar premium",
      "domicilio_dia_12h" => "domicilio de dia 12h",
      "domicilio_noche_12h" => "domicilio de noche 12h",
      "domicilio_24h" => "domicilio 24h",
      "suero" => "suero",
      "medicamentos" => "medicamentos",
      "sonda_vesical" => "sonda vesical",
      "sonda_nasogastrica" => "sonda nasogastrica",
      "sonda_peg" => "sonda peg",
      "curas" => "curas",
      _ => careRequestType.ToLowerInvariant(),
    };
  }

  private static string ResolveDisplayName(UserLookup user)
  {
    var displayName = string.Join(" ", new[] { user.Name, user.LastName }.Where(value => !string.IsNullOrWhiteSpace(value))).Trim();
    return displayName.Length > 0 ? displayName : user.Email;
  }

  private sealed record UserLookup(
    Guid UserId,
    string Email,
    string? Name,
    string? LastName,
    string? IdentificationNumber);
}
