namespace NursingCareBackend.Application.Identity.Responses;

public sealed record ActiveNurseProfileSummaryResponse(
    Guid UserId,
    string Email,
    string? Name,
    string? LastName,
    string? Specialty,
    string? Category);
