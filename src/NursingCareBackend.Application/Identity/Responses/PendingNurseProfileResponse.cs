namespace NursingCareBackend.Application.Identity.Responses;

public sealed record PendingNurseProfileResponse(
    Guid UserId,
    string Email,
    string? Name,
    string? LastName,
    string? IdentificationNumber,
    string? Phone,
    DateOnly? HireDate,
    string? Specialty,
    DateTime CreatedAtUtc
);
