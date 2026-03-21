namespace NursingCareBackend.Application.Identity.Commands;

public sealed record CompleteProfileRequest(
    string Name,
    string LastName,
    string IdentificationNumber,
    string Phone
);
