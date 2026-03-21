namespace NursingCareBackend.Application.Identity.Commands;

public sealed record AdminCompleteNurseProfileRequest(
    string Name,
    string LastName,
    string IdentificationNumber,
    string Phone,
    string Email,
    DateOnly HireDate,
    string Specialty,
    string? LicenseId,
    string BankName,
    string? AccountNumber,
    string Category
);
