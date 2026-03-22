using NursingCareBackend.Application.Identity.Models;

namespace NursingCareBackend.Application.Identity.Responses;

public sealed record AdminNurseProfileSummaryResponse(
    Guid UserId,
    string Email,
    string? Name,
    string? LastName,
    string? Specialty,
    string? Category,
    bool UserIsActive,
    bool NurseProfileIsActive,
    bool IsProfileComplete,
    bool IsAssignmentReady,
    DateTime CreatedAtUtc,
    NurseWorkloadSummary Workload);
