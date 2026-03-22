using System.Text.Json;
using NursingCareBackend.Application.AdminPortal.Auditing;
using NursingCareBackend.Application.Identity.Authentication;
using NursingCareBackend.Application.Identity.Commands;
using NursingCareBackend.Application.Identity.Models;
using NursingCareBackend.Application.Identity.Repositories;
using NursingCareBackend.Application.Identity.Responses;
using NursingCareBackend.Application.Identity.Validation;
using NursingCareBackend.Domain.Identity;

namespace NursingCareBackend.Application.Identity.Services;

public sealed class NurseProfileAdministrationService : INurseProfileAdministrationService
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAdminAuditService _adminAuditService;
    private static readonly NurseWorkloadSummary EmptyWorkload = new(0, 0, 0, 0, 0, null);

    public NurseProfileAdministrationService(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IPasswordHasher passwordHasher,
        IAdminAuditService adminAuditService)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _passwordHasher = passwordHasher;
        _adminAuditService = adminAuditService;
    }

    public async Task<IReadOnlyList<PendingNurseProfileResponse>> GetPendingNurseProfilesAsync(
        CancellationToken cancellationToken = default)
    {
        var users = await _userRepository.GetNurseProfilesAsync(cancellationToken);

        return users
            .Where(IsPendingReview)
            .OrderBy(user => user.CreatedAtUtc)
            .Select(user => new PendingNurseProfileResponse(
                user.Id,
                user.Email,
                user.Name,
                user.LastName,
                user.IdentificationNumber,
                user.Phone,
                user.NurseProfile?.HireDate,
                NurseProfileCatalog.NormalizeSpecialty(user.NurseProfile?.Specialty),
                user.CreatedAtUtc))
            .ToArray();
    }

    public async Task<IReadOnlyList<AdminNurseProfileSummaryResponse>> GetActiveNurseProfilesAsync(
        CancellationToken cancellationToken = default)
    {
        var users = await _userRepository.GetNurseProfilesAsync(cancellationToken);
        return await BuildSummariesAsync(
            users
                .Where(IsOperationallyActive)
                .OrderBy(user => user.Name)
                .ThenBy(user => user.LastName)
                .ToArray(),
            cancellationToken);
    }

    public async Task<IReadOnlyList<AdminNurseProfileSummaryResponse>> GetInactiveNurseProfilesAsync(
        CancellationToken cancellationToken = default)
    {
        var users = await _userRepository.GetNurseProfilesAsync(cancellationToken);
        return await BuildSummariesAsync(
            users
                .Where(user => IsProfileComplete(user) && !IsOperationallyActive(user))
                .OrderBy(user => user.Name)
                .ThenBy(user => user.LastName)
                .ToArray(),
            cancellationToken);
    }

    public async Task<NurseProfileAdminResponse> GetNurseProfileAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await GetRequiredNurseUserAsync(userId, cancellationToken);
        return await MapResponseAsync(user, cancellationToken);
    }

    public async Task<NurseProfileAdminResponse> CreateNurseProfileAsync(
        AdminCreateNurseProfileRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        EnsureActorUserId(actorUserId);
        ValidateCreateRequest(request);

        var normalizedEmail = request.Email.Trim();
        var existingUserWithEmail = await _userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (existingUserWithEmail is not null)
        {
            throw new InvalidOperationException("User with this email already exists.");
        }

        var nurseRole = await _roleRepository.GetByNameAsync(SystemRoles.Nurse, cancellationToken);
        if (nurseRole is null)
        {
            throw new InvalidOperationException("Nurse role not found in the system.");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            ProfileType = UserProfileType.Nurse,
            Name = request.Name.Trim(),
            LastName = request.LastName.Trim(),
            IdentificationNumber = request.IdentificationNumber.Trim(),
            Phone = request.Phone.Trim(),
            DisplayName = $"{request.Name.Trim()} {request.LastName.Trim()}".Trim(),
            PasswordHash = _passwordHasher.Hash(request.Password),
            IsActive = request.IsOperationallyActive,
            CreatedAtUtc = DateTime.UtcNow,
            NurseProfile = new Nurse
            {
                UserId = Guid.Empty,
                IsActive = request.IsOperationallyActive,
                HireDate = request.HireDate,
                Specialty = NurseProfileCatalog.NormalizeRequiredSpecialty(request.Specialty, nameof(request.Specialty)),
                LicenseId = TrimOptional(request.LicenseId),
                BankName = request.BankName.Trim(),
                AccountNumber = TrimOptional(request.AccountNumber),
                Category = NurseProfileCatalog.NormalizeRequiredCategory(request.Category, nameof(request.Category)),
            }
        };

        user.NurseProfile.UserId = user.Id;
        user.UserRoles.Add(new UserRole
        {
            UserId = user.Id,
            RoleId = nurseRole.Id,
            Role = nurseRole,
        });

        await _userRepository.CreateAsync(user, cancellationToken);

        await _adminAuditService.WriteAsync(
            new AdminAuditRecord(
                ActorUserId: actorUserId,
                ActorRole: SystemRoles.Admin,
                Action: AdminAuditActions.NurseProfileCreatedByAdmin,
                EntityType: "NurseProfile",
                EntityId: user.Id.ToString(),
                Notes: $"Nurse profile created for {user.Email}.",
                MetadataJson: JsonSerializer.Serialize(new
                {
                    after = CreateAuditSnapshot(user)
                })),
            cancellationToken);

        return await MapResponseAsync(user, cancellationToken);
    }

    public async Task<NurseProfileAdminResponse> UpdateNurseProfileAsync(
        Guid userId,
        AdminUpdateNurseProfileRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        EnsureActorUserId(actorUserId);

        var user = await GetRequiredNurseUserAsync(userId, cancellationToken);
        if (IsPendingReview(user))
        {
            throw new InvalidOperationException("Pending nurse profiles must be completed through the review flow.");
        }

        ValidateProfileRequest(
            request.Name,
            request.LastName,
            request.IdentificationNumber,
            request.Phone,
            request.Email,
            request.HireDate,
            request.Specialty,
            request.LicenseId,
            request.BankName,
            request.AccountNumber,
            request.Category);

        await EnsureEmailUniquenessAsync(userId, request.Email, cancellationToken);

        var before = CreateAuditSnapshot(user);
        ApplyProfileFields(
            user,
            request.Name,
            request.LastName,
            request.IdentificationNumber,
            request.Phone,
            request.Email,
            request.HireDate,
            request.Specialty,
            request.LicenseId,
            request.BankName,
            request.AccountNumber,
            request.Category);

        await _userRepository.UpdateAsync(user, cancellationToken);

        await _adminAuditService.WriteAsync(
            new AdminAuditRecord(
                ActorUserId: actorUserId,
                ActorRole: SystemRoles.Admin,
                Action: AdminAuditActions.NurseProfileUpdated,
                EntityType: "NurseProfile",
                EntityId: user.Id.ToString(),
                Notes: $"Nurse profile updated for {user.Email}.",
                MetadataJson: JsonSerializer.Serialize(new
                {
                    before,
                    after = CreateAuditSnapshot(user)
                })),
            cancellationToken);

        return await MapResponseAsync(user, cancellationToken);
    }

    public async Task<NurseProfileAdminResponse> CompleteNurseProfileCreationAsync(
        Guid userId,
        AdminCompleteNurseProfileRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        EnsureActorUserId(actorUserId);
        var user = await GetRequiredNurseUserAsync(userId, cancellationToken);
        ValidateProfileRequest(
            request.Name,
            request.LastName,
            request.IdentificationNumber,
            request.Phone,
            request.Email,
            request.HireDate,
            request.Specialty,
            request.LicenseId,
            request.BankName,
            request.AccountNumber,
            request.Category);

        await EnsureEmailUniquenessAsync(userId, request.Email, cancellationToken);

        var before = CreateAuditSnapshot(user);
        ApplyProfileFields(
            user,
            request.Name,
            request.LastName,
            request.IdentificationNumber,
            request.Phone,
            request.Email,
            request.HireDate,
            request.Specialty,
            request.LicenseId,
            request.BankName,
            request.AccountNumber,
            request.Category);

        user.IsActive = true;
        user.NurseProfile!.IsActive = true;

        await _userRepository.UpdateAsync(user, cancellationToken);

        await _adminAuditService.WriteAsync(
            new AdminAuditRecord(
                ActorUserId: actorUserId,
                ActorRole: SystemRoles.Admin,
                Action: AdminAuditActions.NurseProfileCompleted,
                EntityType: "NurseProfile",
                EntityId: user.Id.ToString(),
                Notes: $"Pending nurse profile completed for {user.Email}.",
                MetadataJson: JsonSerializer.Serialize(new
                {
                    before,
                    after = CreateAuditSnapshot(user)
                })),
            cancellationToken);

        return await MapResponseAsync(user, cancellationToken);
    }

    public async Task<NurseProfileAdminResponse> SetOperationalAccessAsync(
        Guid userId,
        AdminSetNurseOperationalAccessRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        EnsureActorUserId(actorUserId);

        var user = await GetRequiredNurseUserAsync(userId, cancellationToken);
        if (!IsProfileComplete(user))
        {
            throw new InvalidOperationException("Nurse operational access can only be changed after the profile is complete.");
        }

        var before = CreateAuditSnapshot(user);
        user.IsActive = request.IsOperationallyActive;
        user.NurseProfile!.IsActive = request.IsOperationallyActive;

        await _userRepository.UpdateAsync(user, cancellationToken);

        await _adminAuditService.WriteAsync(
            new AdminAuditRecord(
                ActorUserId: actorUserId,
                ActorRole: SystemRoles.Admin,
                Action: AdminAuditActions.NurseOperationalAccessChanged,
                EntityType: "NurseProfile",
                EntityId: user.Id.ToString(),
                Notes: request.IsOperationallyActive
                    ? $"Nurse operational access activated for {user.Email}."
                    : $"Nurse operational access deactivated for {user.Email}.",
                MetadataJson: JsonSerializer.Serialize(new
                {
                    before,
                    after = CreateAuditSnapshot(user)
                })),
            cancellationToken);

        return await MapResponseAsync(user, cancellationToken);
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

    private static void EnsureActorUserId(Guid actorUserId)
    {
        if (actorUserId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("A valid admin user identifier is required to manage nurse profiles.");
        }
    }

    private async Task<IReadOnlyList<AdminNurseProfileSummaryResponse>> BuildSummariesAsync(
        IReadOnlyCollection<User> users,
        CancellationToken cancellationToken)
    {
        if (users.Count == 0)
        {
            return Array.Empty<AdminNurseProfileSummaryResponse>();
        }

        var workloads = await _userRepository.GetNurseWorkloadsAsync(
            users.Select(user => user.Id).ToArray(),
            cancellationToken);

        return users
            .Select(user => new AdminNurseProfileSummaryResponse(
                user.Id,
                user.Email,
                user.Name,
                user.LastName,
                NurseProfileCatalog.NormalizeSpecialty(user.NurseProfile?.Specialty),
                NurseProfileCatalog.NormalizeCategory(user.NurseProfile?.Category),
                user.IsActive,
                user.NurseProfile?.IsActive == true,
                IsProfileComplete(user),
                IsAssignmentReady(user),
                user.CreatedAtUtc,
                workloads.TryGetValue(user.Id, out var workload) ? workload : EmptyWorkload))
            .ToArray();
    }

    private async Task<NurseProfileAdminResponse> MapResponseAsync(User user, CancellationToken cancellationToken)
    {
        var nurse = user.NurseProfile!;
        var workloads = await _userRepository.GetNurseWorkloadsAsync([user.Id], cancellationToken);
        var hasHistoricalCareRequests = await _userRepository.HasAssignedCareRequestsAsync(user.Id, cancellationToken);
        var isProfileComplete = IsProfileComplete(user);

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
            isProfileComplete,
            IsPendingReview(user),
            IsAssignmentReady(user),
            hasHistoricalCareRequests,
            user.CreatedAtUtc,
            nurse.HireDate,
            NurseProfileCatalog.NormalizeSpecialty(nurse.Specialty),
            nurse.LicenseId,
            nurse.BankName,
            nurse.AccountNumber,
            NurseProfileCatalog.NormalizeCategory(nurse.Category),
            workloads.TryGetValue(user.Id, out var workload) ? workload : EmptyWorkload);
    }

    private async Task EnsureEmailUniquenessAsync(Guid userId, string email, CancellationToken cancellationToken)
    {
        var existingUserWithEmail = await _userRepository.GetByEmailAsync(email.Trim(), cancellationToken);
        if (existingUserWithEmail is not null && existingUserWithEmail.Id != userId)
        {
            throw new InvalidOperationException("User with this email already exists.");
        }
    }

    private static void ValidateCreateRequest(AdminCreateNurseProfileRequest request)
    {
        ValidateProfileRequest(
            request.Name,
            request.LastName,
            request.IdentificationNumber,
            request.Phone,
            request.Email,
            request.HireDate,
            request.Specialty,
            request.LicenseId,
            request.BankName,
            request.AccountNumber,
            request.Category);

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            throw new ArgumentException("Password is required.", nameof(request.Password));
        }

        if (request.Password.Length < 6)
        {
            throw new ArgumentException("Password must be at least 6 characters long.", nameof(request.Password));
        }

        if (!string.Equals(request.Password, request.ConfirmPassword, StringComparison.Ordinal))
        {
            throw new ArgumentException("Passwords do not match.", nameof(request.ConfirmPassword));
        }
    }

    private static void ValidateProfileRequest(
        string name,
        string lastName,
        string identificationNumber,
        string phone,
        string email,
        DateOnly hireDate,
        string specialty,
        string? licenseId,
        string bankName,
        string? accountNumber,
        string category)
    {
        IdentityInputRules.EnsureTextOnlyRequired(name, nameof(name), "Name");
        IdentityInputRules.EnsureTextOnlyRequired(lastName, nameof(lastName), "Last name");
        IdentityInputRules.EnsureIdentificationNumber(identificationNumber, nameof(identificationNumber));
        IdentityInputRules.EnsurePhone(phone, nameof(phone));

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email is required.", nameof(email));
        }

        if (hireDate == default)
        {
            throw new ArgumentException("Hire date is required.", nameof(hireDate));
        }

        if (string.IsNullOrWhiteSpace(bankName))
        {
            throw new ArgumentException("Bank name is required.", nameof(bankName));
        }

        NurseProfileCatalog.NormalizeRequiredSpecialty(specialty, nameof(specialty));
        IdentityInputRules.EnsureTextOnlyRequired(bankName, nameof(bankName), "Bank name");
        IdentityInputRules.EnsureNumericOnlyOptional(licenseId, nameof(licenseId), "License ID");
        IdentityInputRules.EnsureNumericOnlyOptional(accountNumber, nameof(accountNumber), "Account number");
        NurseProfileCatalog.NormalizeRequiredCategory(category, nameof(category));
    }

    private static void ApplyProfileFields(
        User user,
        string name,
        string lastName,
        string identificationNumber,
        string phone,
        string email,
        DateOnly hireDate,
        string specialty,
        string? licenseId,
        string bankName,
        string? accountNumber,
        string category)
    {
        var nurse = user.NurseProfile!;

        user.Name = name.Trim();
        user.LastName = lastName.Trim();
        user.IdentificationNumber = identificationNumber.Trim();
        user.Phone = phone.Trim();
        user.Email = email.Trim();
        user.DisplayName = $"{user.Name} {user.LastName}".Trim();

        nurse.HireDate = hireDate;
        nurse.Specialty = NurseProfileCatalog.NormalizeRequiredSpecialty(specialty, nameof(specialty));
        nurse.LicenseId = TrimOptional(licenseId);
        nurse.BankName = bankName.Trim();
        nurse.AccountNumber = TrimOptional(accountNumber);
        nurse.Category = NurseProfileCatalog.NormalizeRequiredCategory(category, nameof(category));
    }

    private static bool IsProfileComplete(User user)
    {
        var nurse = user.NurseProfile;
        return nurse is not null
            && !string.IsNullOrWhiteSpace(user.Name)
            && !string.IsNullOrWhiteSpace(user.LastName)
            && !string.IsNullOrWhiteSpace(user.IdentificationNumber)
            && !string.IsNullOrWhiteSpace(user.Phone)
            && !string.IsNullOrWhiteSpace(user.Email)
            && nurse.HireDate is not null
            && !string.IsNullOrWhiteSpace(NurseProfileCatalog.NormalizeSpecialty(nurse.Specialty))
            && !string.IsNullOrWhiteSpace(nurse.BankName)
            && !string.IsNullOrWhiteSpace(NurseProfileCatalog.NormalizeCategory(nurse.Category));
    }

    private static bool IsPendingReview(User user)
        => !IsProfileComplete(user);

    private static bool IsOperationallyActive(User user)
        => IsProfileComplete(user) && user.IsActive && user.NurseProfile?.IsActive == true;

    private static bool IsAssignmentReady(User user)
        => IsOperationallyActive(user);

    private static object CreateAuditSnapshot(User user)
    {
        var nurse = user.NurseProfile;

        return new
        {
            userId = user.Id,
            email = user.Email,
            name = user.Name,
            lastName = user.LastName,
            identificationNumber = user.IdentificationNumber,
            phone = user.Phone,
            userIsActive = user.IsActive,
            nurseProfileIsActive = nurse?.IsActive,
            isProfileComplete = IsProfileComplete(user),
            isPendingReview = IsPendingReview(user),
            isAssignmentReady = IsAssignmentReady(user),
            hireDate = nurse?.HireDate,
            specialty = NurseProfileCatalog.NormalizeSpecialty(nurse?.Specialty),
            licenseId = nurse?.LicenseId,
            bankName = nurse?.BankName,
            accountNumber = nurse?.AccountNumber,
            category = NurseProfileCatalog.NormalizeCategory(nurse?.Category)
        };
    }

    private static string? TrimOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
