using NursingCareBackend.Application.Identity.Commands;
using NursingCareBackend.Application.Identity.Repositories;
using NursingCareBackend.Application.Identity.Responses;
using NursingCareBackend.Application.Identity.Validation;
using NursingCareBackend.Domain.Identity;

namespace NursingCareBackend.Application.Identity.Services;

public sealed class NurseProfileAdministrationService : INurseProfileAdministrationService
{
    private readonly IUserRepository _userRepository;

    public NurseProfileAdministrationService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<IReadOnlyList<PendingNurseProfileResponse>> GetPendingNurseProfilesAsync(
        CancellationToken cancellationToken = default)
    {
        var users = await _userRepository.GetPendingNurseProfilesAsync(cancellationToken);

        return users
            .Select(user => new PendingNurseProfileResponse(
                user.Id,
                user.Email,
                user.Name,
                user.LastName,
                user.IdentificationNumber,
                user.Phone,
                user.CreatedAtUtc))
            .ToArray();
    }

    public async Task<IReadOnlyList<ActiveNurseProfileSummaryResponse>> GetActiveNurseProfilesAsync(
        CancellationToken cancellationToken = default)
    {
        var users = await _userRepository.GetActiveNurseProfilesAsync(cancellationToken);

        return users
            .Select(user => new ActiveNurseProfileSummaryResponse(
                user.Id,
                user.Email,
                user.Name,
                user.LastName,
                user.NurseProfile?.Specialty,
                user.NurseProfile?.Category))
            .ToArray();
    }

    public async Task<NurseProfileAdminResponse> GetNurseProfileAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await GetRequiredNurseUserAsync(userId, cancellationToken);
        return MapResponse(user);
    }

    public async Task<NurseProfileAdminResponse> CompleteNurseProfileCreationAsync(
        Guid userId,
        AdminCompleteNurseProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await GetRequiredNurseUserAsync(userId, cancellationToken);
        ValidateCompletionRequest(request);

        var existingUserWithEmail = await _userRepository.GetByEmailAsync(request.Email.Trim(), cancellationToken);
        if (existingUserWithEmail is not null && existingUserWithEmail.Id != userId)
        {
            throw new InvalidOperationException("User with this email already exists.");
        }

        var nurse = user.NurseProfile!;

        user.Name = request.Name.Trim();
        user.LastName = request.LastName.Trim();
        user.IdentificationNumber = request.IdentificationNumber.Trim();
        user.Phone = request.Phone.Trim();
        user.Email = request.Email.Trim();
        user.IsActive = true;

        nurse.HireDate = request.HireDate;
        nurse.Specialty = request.Specialty.Trim();
        nurse.LicenseId = TrimOptional(request.LicenseId);
        nurse.BankName = request.BankName.Trim();
        nurse.AccountNumber = TrimOptional(request.AccountNumber);
        nurse.Category = request.Category.Trim();
        nurse.IsActive = true;

        await _userRepository.UpdateAsync(user, cancellationToken);

        return MapResponse(user);
    }

    private async Task<User> GetRequiredNurseUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));
        }

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            throw new KeyNotFoundException($"User with ID {userId} not found.");
        }

        if (user.ProfileType != UserProfileType.Nurse || user.NurseProfile is null)
        {
            throw new InvalidOperationException("The requested user does not have a nurse profile.");
        }

        return user;
    }

    private static void ValidateCompletionRequest(AdminCompleteNurseProfileRequest request)
    {
        IdentityInputRules.EnsureTextOnlyRequired(request.Name, nameof(request.Name), "Name");
        IdentityInputRules.EnsureTextOnlyRequired(request.LastName, nameof(request.LastName), "Last name");
        IdentityInputRules.EnsureIdentificationNumber(request.IdentificationNumber, nameof(request.IdentificationNumber));
        IdentityInputRules.EnsurePhone(request.Phone, nameof(request.Phone));

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            throw new ArgumentException("Email is required.", nameof(request.Email));
        }

        if (string.IsNullOrWhiteSpace(request.Specialty))
        {
            throw new ArgumentException("Specialty is required.", nameof(request.Specialty));
        }

        if (string.IsNullOrWhiteSpace(request.BankName))
        {
            throw new ArgumentException("Bank name is required.", nameof(request.BankName));
        }

        IdentityInputRules.EnsureTextOnlyRequired(request.BankName, nameof(request.BankName), "Bank name");
        IdentityInputRules.EnsureNumericOnlyOptional(request.LicenseId, nameof(request.LicenseId), "License ID");
        IdentityInputRules.EnsureNumericOnlyOptional(request.AccountNumber, nameof(request.AccountNumber), "Account number");

        if (string.IsNullOrWhiteSpace(request.Category))
        {
            throw new ArgumentException("Category is required.", nameof(request.Category));
        }
    }

    private static NurseProfileAdminResponse MapResponse(User user)
    {
        var nurse = user.NurseProfile!;

        return new NurseProfileAdminResponse(
            user.Id,
            user.Email,
            user.Name,
            user.LastName,
            user.IdentificationNumber,
            user.Phone,
            user.ProfileType,
            user.IsActive,
            nurse.IsActive,
            user.CreatedAtUtc,
            nurse.HireDate,
            nurse.Specialty,
            nurse.LicenseId,
            nurse.BankName,
            nurse.AccountNumber,
            nurse.Category);
    }

    private static string? TrimOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
