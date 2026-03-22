using NursingCareBackend.Application.Identity.Models;
using NursingCareBackend.Domain.Identity;

namespace NursingCareBackend.Application.Identity.Responses;

public sealed record NurseProfileAdminResponse(
    Guid UserId,
    string Email,
    string? Name,
    string? LastName,
    string? IdentificationNumber,
    string? Phone,
    UserProfileType ProfileType,
    bool UserIsActive,
    bool NurseProfileIsActive,
    bool IsProfileComplete,
    bool IsPendingReview,
    bool IsAssignmentReady,
    bool HasHistoricalCareRequests,
    DateTime CreatedAtUtc,
    DateOnly? HireDate,
    string? Specialty,
    string? LicenseId,
    string? BankName,
    string? AccountNumber,
    string? Category,
    NurseWorkloadSummary Workload
);
