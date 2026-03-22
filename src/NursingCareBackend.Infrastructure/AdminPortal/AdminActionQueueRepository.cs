using Microsoft.EntityFrameworkCore;
using NursingCareBackend.Application.AdminPortal.Queries;
using NursingCareBackend.Domain.CareRequests;
using NursingCareBackend.Domain.Identity;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Infrastructure.AdminPortal;

public sealed class AdminActionQueueRepository : IAdminActionQueueRepository
{
  private readonly NursingCareDbContext _dbContext;

  public AdminActionQueueRepository(NursingCareDbContext dbContext)
  {
    _dbContext = dbContext;
  }

  public async Task<IReadOnlyList<AdminActionQueueItem>> GetItemsAsync(CancellationToken cancellationToken)
  {
    var utcNow = DateTime.UtcNow;
    var currentCareDate = DateOnly.FromDateTime(utcNow);
    var staleCutoffUtc = utcNow.AddHours(-48);
    var items = new List<AdminActionQueueItem>();

    var pendingNurseProfiles = await _dbContext.Users
      .AsNoTracking()
      .Where(user =>
        user.ProfileType == UserProfileType.Nurse
        && user.NurseProfile != null
        && !user.NurseProfile.IsActive)
      .OrderBy(user => user.CreatedAtUtc)
      .Select(user => new
      {
        user.Id,
        user.Email,
        user.Name,
        user.LastName,
        user.CreatedAtUtc,
      })
      .ToListAsync(cancellationToken);

    items.AddRange(pendingNurseProfiles.Select(user => CreateActionItem(
      id: $"nurse-profile-pending:{user.Id}",
      severity: "Medium",
      state: ResolveState(user.CreatedAtUtc, utcNow),
      entityType: "NurseProfile",
      entityIdentifier: user.Id.ToString(),
      summary: $"Perfil de enfermeria pendiente para {ResolveDisplayName(user.Name, user.LastName, user.Email)}.",
      requiredAction: "Completar el perfil administrativo para habilitar acceso operativo.",
      assignedOwner: null,
      deepLinkPath: $"/admin/nurse-profiles?view=pending&userId={user.Id}",
      detectedAtUtc: user.CreatedAtUtc)));

    var candidateCareRequests = await _dbContext.CareRequests
      .AsNoTracking()
      .Where(careRequest =>
        careRequest.Status == CareRequestStatus.Pending
        || careRequest.Status == CareRequestStatus.Approved)
      .Select(careRequest => new
      {
        careRequest.Id,
        careRequest.Description,
        careRequest.Status,
        careRequest.AssignedNurse,
        careRequest.CareRequestDate,
        careRequest.CreatedAtUtc,
        careRequest.UpdatedAtUtc,
      })
      .ToListAsync(cancellationToken);

    var assignedNurseIds = candidateCareRequests
      .Where(careRequest => careRequest.AssignedNurse.HasValue)
      .Select(careRequest => careRequest.AssignedNurse!.Value)
      .Distinct()
      .ToList();

    var assignedNurses = assignedNurseIds.Count == 0
      ? new Dictionary<Guid, AssignedNurseStatus>()
      : await _dbContext.Users
        .AsNoTracking()
        .Where(user => assignedNurseIds.Contains(user.Id))
        .Select(user => new AssignedNurseStatus(
          user.Id,
          user.ProfileType,
          user.IsActive,
          user.NurseProfile != null && user.NurseProfile.IsActive))
        .ToDictionaryAsync(user => user.UserId, cancellationToken);

    foreach (var careRequest in candidateCareRequests)
    {
      var blockedReason = ResolveBlockedReason(careRequest.Status, careRequest.AssignedNurse, assignedNurses);
      if (blockedReason is not null)
      {
        items.Add(CreateActionItem(
          id: $"care-request-blocked:{careRequest.Id}",
          severity: "High",
          state: ResolveState(careRequest.UpdatedAtUtc, utcNow),
          entityType: "CareRequest",
          entityIdentifier: careRequest.Id.ToString(),
          summary: $"La solicitud \"{careRequest.Description}\" quedo bloqueada por un estado invalido.",
          requiredAction: blockedReason,
          assignedOwner: null,
          deepLinkPath: $"/admin/care-requests?selected={careRequest.Id}",
          detectedAtUtc: careRequest.UpdatedAtUtc));

        continue;
      }

      if (careRequest.Status == CareRequestStatus.Pending && !careRequest.AssignedNurse.HasValue)
      {
        var isUrgent = IsOverdueOrStale(careRequest.Status, careRequest.CareRequestDate, careRequest.UpdatedAtUtc, currentCareDate, staleCutoffUtc);

        items.Add(CreateActionItem(
          id: $"care-request-unassigned:{careRequest.Id}",
          severity: isUrgent ? "High" : "Medium",
          state: ResolveState(careRequest.UpdatedAtUtc, utcNow),
          entityType: "CareRequest",
          entityIdentifier: careRequest.Id.ToString(),
          summary: $"La solicitud \"{careRequest.Description}\" sigue sin una enfermera asignada.",
          requiredAction: "Asignar una enfermera activa antes de enviarla a aprobacion.",
          assignedOwner: null,
          deepLinkPath: $"/admin/care-requests?view=unassigned&selected={careRequest.Id}",
          detectedAtUtc: careRequest.UpdatedAtUtc));

        continue;
      }

      if (careRequest.Status == CareRequestStatus.Pending && careRequest.AssignedNurse.HasValue)
      {
        var isUrgent = IsOverdueOrStale(careRequest.Status, careRequest.CareRequestDate, careRequest.UpdatedAtUtc, currentCareDate, staleCutoffUtc);

        items.Add(CreateActionItem(
          id: $"care-request-ready:{careRequest.Id}",
          severity: isUrgent ? "High" : "Medium",
          state: ResolveState(careRequest.UpdatedAtUtc, utcNow),
          entityType: "CareRequest",
          entityIdentifier: careRequest.Id.ToString(),
          summary: $"La solicitud \"{careRequest.Description}\" ya esta lista para revision administrativa.",
          requiredAction: "Revisar el caso y aprobar o rechazar la solicitud.",
          assignedOwner: null,
          deepLinkPath: $"/admin/care-requests?view=pending-approval&selected={careRequest.Id}",
          detectedAtUtc: careRequest.UpdatedAtUtc));
      }
    }

    var usersRequiringManualIntervention = await _dbContext.Users
      .AsNoTracking()
      .Where(user =>
        !user.UserRoles.Any()
        || (user.ProfileType == UserProfileType.Nurse && user.NurseProfile == null)
        || (user.ProfileType == UserProfileType.Client && user.ClientProfile == null))
      .OrderByDescending(user => user.CreatedAtUtc)
      .Select(user => new
      {
        user.Id,
        user.Email,
        user.Name,
        user.LastName,
        user.CreatedAtUtc,
        MissingRoles = !user.UserRoles.Any(),
        MissingNurseProfile = user.ProfileType == UserProfileType.Nurse && user.NurseProfile == null,
        MissingClientProfile = user.ProfileType == UserProfileType.Client && user.ClientProfile == null,
      })
      .ToListAsync(cancellationToken);

    items.AddRange(usersRequiringManualIntervention.Select(user => CreateActionItem(
      id: $"user-manual-intervention:{user.Id}",
      severity: "High",
      state: ResolveState(user.CreatedAtUtc, utcNow),
      entityType: "UserAccount",
      entityIdentifier: user.Id.ToString(),
      summary: $"La cuenta de {ResolveDisplayName(user.Name, user.LastName, user.Email)} requiere intervencion manual.",
      requiredAction: ResolveManualInterventionAction(user.MissingRoles, user.MissingNurseProfile, user.MissingClientProfile),
      assignedOwner: null,
      deepLinkPath: $"/admin/users/{user.Id}",
      detectedAtUtc: user.CreatedAtUtc)));

    var overdueOrStaleCount = await _dbContext.CareRequests
      .AsNoTracking()
      .Where(careRequest =>
        careRequest.Status != CareRequestStatus.Completed
        && (
          (careRequest.CareRequestDate.HasValue && careRequest.CareRequestDate.Value < currentCareDate)
          || (!careRequest.CareRequestDate.HasValue
              && careRequest.Status == CareRequestStatus.Pending
              && careRequest.UpdatedAtUtc <= staleCutoffUtc)
        ))
      .CountAsync(cancellationToken);

    if (overdueOrStaleCount > 0)
    {
      items.Add(CreateActionItem(
        id: "system-issue:overdue-backlog",
        severity: "High",
        state: "Unread",
        entityType: "SystemIssue",
        entityIdentifier: "overdue-backlog",
        summary: $"Se detectaron {overdueOrStaleCount} solicitudes atrasadas o estancadas en la operacion.",
        requiredAction: "Priorizar la revision de la cola vencida y normalizar los casos abiertos.",
        assignedOwner: null,
        deepLinkPath: "/admin/care-requests?view=overdue",
        detectedAtUtc: utcNow));
    }

    return items
      .OrderBy(item => GetSeverityRank(item.Severity))
      .ThenBy(item => item.State == "Unread" ? 0 : 1)
      .ThenByDescending(item => item.DetectedAtUtc)
      .ToList()
      .AsReadOnly();
  }

  private static AdminActionQueueItem CreateActionItem(
    string id,
    string severity,
    string state,
    string entityType,
    string entityIdentifier,
    string summary,
    string requiredAction,
    string? assignedOwner,
    string deepLinkPath,
    DateTime detectedAtUtc)
  {
    return new AdminActionQueueItem(
      Id: id,
      Severity: severity,
      State: state,
      EntityType: entityType,
      EntityIdentifier: entityIdentifier,
      Summary: summary,
      RequiredAction: requiredAction,
      AssignedOwner: assignedOwner,
      DeepLinkPath: deepLinkPath,
      DetectedAtUtc: detectedAtUtc);
  }

  private static string ResolveState(DateTime detectedAtUtc, DateTime utcNow)
  {
    return detectedAtUtc >= utcNow.AddHours(-24)
      ? "Unread"
      : "Pending";
  }

  private static string ResolveDisplayName(string? name, string? lastName, string email)
  {
    var displayName = string.Join(" ", new[] { name, lastName }.Where(value => !string.IsNullOrWhiteSpace(value))).Trim();
    return displayName.Length > 0 ? displayName : email;
  }

  private static string? ResolveBlockedReason(
    CareRequestStatus status,
    Guid? assignedNurse,
    IReadOnlyDictionary<Guid, AssignedNurseStatus> assignedNurses)
  {
    if (status == CareRequestStatus.Approved && !assignedNurse.HasValue)
    {
      return "Revisar la asignacion porque la solicitud aparece aprobada sin una enfermera valida.";
    }

    if (!assignedNurse.HasValue)
    {
      return null;
    }

    if (!assignedNurses.TryGetValue(assignedNurse.Value, out var nurse))
    {
      return "Revisar la asignacion porque la enfermera vinculada ya no existe en el sistema.";
    }

    if (nurse.ProfileType != UserProfileType.Nurse || !nurse.UserIsActive || !nurse.NurseProfileIsActive)
    {
      return "Reasignar o corregir la enfermera vinculada porque la cuenta actual no esta lista para operar.";
    }

    return null;
  }

  private static string ResolveManualInterventionAction(
    bool missingRoles,
    bool missingNurseProfile,
    bool missingClientProfile)
  {
    if (missingRoles)
    {
      return "Revisar la cuenta y asignar el rol correcto antes de habilitar el acceso.";
    }

    if (missingNurseProfile)
    {
      return "Reconstruir o completar el perfil de enfermeria asociado a esta cuenta.";
    }

    if (missingClientProfile)
    {
      return "Completar la vinculacion del perfil de cliente antes de continuar.";
    }

    return "Revisar la consistencia de la cuenta y normalizar su estado.";
  }

  private static bool IsOverdueOrStale(
    CareRequestStatus status,
    DateOnly? careRequestDate,
    DateTime updatedAtUtc,
    DateOnly currentCareDate,
    DateTime staleCutoffUtc)
  {
    if (status == CareRequestStatus.Completed)
    {
      return false;
    }

    if (careRequestDate.HasValue)
    {
      return careRequestDate.Value < currentCareDate;
    }

    return status == CareRequestStatus.Pending && updatedAtUtc <= staleCutoffUtc;
  }

  private static int GetSeverityRank(string severity)
  {
    return severity switch
    {
      "High" => 0,
      "Medium" => 1,
      _ => 2,
    };
  }

  private sealed record AssignedNurseStatus(
    Guid UserId,
    UserProfileType ProfileType,
    bool UserIsActive,
    bool NurseProfileIsActive);
}
