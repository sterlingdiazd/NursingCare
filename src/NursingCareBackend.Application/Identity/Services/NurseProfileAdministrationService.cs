using System.Text.Json;
using NursingCareBackend.Application.AdminPortal.Auditing;
using NursingCareBackend.Application.AdminPortal.Notifications;
using NursingCareBackend.Application.Catalogs;
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
    private readonly INurseCatalogService _nurseCatalog;
    private readonly IAdminNotificationPublisher _notifications;
    private static readonly NurseWorkloadSummary EmptyWorkload = new(0, 0, 0, 0, 0, null);

    public NurseProfileAdministrationService(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IPasswordHasher passwordHasher,
        IAdminAuditService adminAuditService,
        INurseCatalogService nurseCatalog,
        IAdminNotificationPublisher notifications)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _passwordHasher = passwordHasher;
        _adminAuditService = adminAuditService;
        _nurseCatalog = nurseCatalog;
        _notifications = notifications;
    }

    public async Task<IReadOnlyList<PendingNurseProfileResponse>> GetPendingNurseProfilesAsync(
        CancellationToken cancellationToken = default)
    {
        var users = await _userRepository.GetNurseProfilesAsync(cancellationToken);

        var pending = users
            .Where(IsPendingReview)
            .OrderBy(user => user.CreatedAtUtc)
            .ToArray();

        var results = new List<PendingNurseProfileResponse>(pending.Length);
        foreach (var user in pending)
        {
            var specialty = await _nurseCatalog.NormalizeSpecialtyAsync(user.NurseProfile?.Specialty, cancellationToken);
            results.Add(new PendingNurseProfileResponse(
                user.Id,
                user.Email,
                user.Name,
                user.LastName,
                user.IdentificationNumber,
                user.Phone,
                user.NurseProfile?.HireDate,
                specialty,
                user.CreatedAtUtc));
        }

        return results;
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
        await ValidateCreateRequestAsync(request, cancellationToken);

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

        var normalizedSpecialty = await _nurseCatalog.NormalizeRequiredSpecialtyAsync(
            request.Specialty,
            nameof(request.Specialty),
            cancellationToken);
        var normalizedCategory = await _nurseCatalog.NormalizeRequiredCategoryAsync(
            request.Category,
            nameof(request.Category),
            cancellationToken);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            ProfileType = UserProfileType.NURSE,
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
                Specialty = normalizedSpecialty,
                LicenseId = TrimOptional(request.LicenseId),
                BankName = request.BankName.Trim(),
                AccountNumber = TrimOptional(request.AccountNumber),
                Category = normalizedCategory,
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

        if (IsPendingReview(user))
        {
            await _notifications.PublishToAdminsAsync(
                new AdminNotificationPublishRequest(
                    Category: "nurse_profile_pending_completion",
                    Severity: "Medium",
                    Title: "Perfil de enfermeria pendiente de completar",
                    Body: $"Se creo el perfil de la enfermera {user.Email} y requiere completar informacion administrativa.",
                    EntityType: "NurseProfile",
                    EntityId: user.Id.ToString(),
                    DeepLinkPath: $"/admin/nurse-profiles/{user.Id}",
                    Source: "Administracion",
                    RequiresAction: true),
                cancellationToken);
        }
        else
        {
            await _notifications.PublishToAdminsAsync(
                new AdminNotificationPublishRequest(
                    Category: "nurse_profile_completed",
                    Severity: "Low",
                    Title: "Perfil de enfermeria completado",
                    Body: $"Se creo el perfil completo de la enfermera {user.Email} y esta listo para asignacion.",
                    EntityType: "NurseProfile",
                    EntityId: user.Id.ToString(),
                    DeepLinkPath: $"/admin/nurse-profiles/{user.Id}",
                    Source: "Administracion",
                    RequiresAction: false),
                cancellationToken);
        }

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

        await ValidateProfileRequestAsync(
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
            request.Category,
            cancellationToken);

        await EnsureEmailUniquenessAsync(userId, request.Email, cancellationToken);

        var before = CreateAuditSnapshot(user);
        await ApplyProfileFieldsAsync(
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
            request.Category,
            cancellationToken);

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
        await ValidateProfileRequestAsync(
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
            request.Category,
            cancellationToken);

        await EnsureEmailUniquenessAsync(userId, request.Email, cancellationToken);

        var before = CreateAuditSnapshot(user);
        await ApplyProfileFieldsAsync(
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
            request.Category,
            cancellationToken);

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

        await _notifications.PublishToAdminsAsync(
            new AdminNotificationPublishRequest(
                Category: "nurse_profile_completed",
                Severity: "Low",
                Title: "Perfil de enfermeria completado",
                Body: $"El perfil de la enfermera {user.Email} ha sido completado y esta listo para asignacion.",
                EntityType: "NurseProfile",
                EntityId: user.Id.ToString(),
                DeepLinkPath: $"/admin/nurse-profiles/{user.Id}",
                Source: "Administracion",
                RequiresAction: false),
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

        if (user.ProfileType != UserProfileType.NURSE || user.NurseProfile is null)
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

        var results = new List<AdminNurseProfileSummaryResponse>(users.Count);
        foreach (var user in users)
        {
            var specialty = await _nurseCatalog.NormalizeSpecialtyAsync(user.NurseProfile?.Specialty, cancellationToken);
            var category = await _nurseCatalog.NormalizeCategoryAsync(user.NurseProfile?.Category, cancellationToken);
            results.Add(new AdminNurseProfileSummaryResponse(
                user.Id,
                user.Email,
                user.Name,
                user.LastName,
                specialty,
                category,
                user.IsActive,
                user.NurseProfile?.IsActive == true,
                IsProfileComplete(user),
                IsAssignmentReady(user),
                user.CreatedAtUtc,
                workloads.TryGetValue(user.Id, out var workload) ? workload : EmptyWorkload));
        }

        return results;
    }

    private async Task<NurseProfileAdminResponse> MapResponseAsync(User user, CancellationToken cancellationToken)
    {
        var nurse = user.NurseProfile!;
        var workloads = await _userRepository.GetNurseWorkloadsAsync([user.Id], cancellationToken);
        var hasHistoricalCareRequests = await _userRepository.HasAssignedCareRequestsAsync(user.Id, cancellationToken);
        var isProfileComplete = IsProfileComplete(user);
        var specialty = await _nurseCatalog.NormalizeSpecialtyAsync(nurse.Specialty, cancellationToken);
        var category = await _nurseCatalog.NormalizeCategoryAsync(nurse.Category, cancellationToken);

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
            specialty,
            nurse.LicenseId,
            nurse.BankName,
            nurse.AccountNumber,
            category,
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

    private async Task ValidateCreateRequestAsync(AdminCreateNurseProfileRequest request, CancellationToken cancellationToken)
    {
        await ValidateProfileRequestAsync(
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
            request.Category,
            cancellationToken);

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

    private async Task ValidateProfileRequestAsync(
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
        string category,
        CancellationToken cancellationToken)
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

        await _nurseCatalog.NormalizeRequiredSpecialtyAsync(specialty, nameof(specialty), cancellationToken);
        IdentityInputRules.EnsureTextOnlyRequired(bankName, nameof(bankName), "Bank name");
        IdentityInputRules.EnsureNumericOnlyOptional(licenseId, nameof(licenseId), "License ID");
        IdentityInputRules.EnsureNumericOnlyOptional(accountNumber, nameof(accountNumber), "Account number");
        await _nurseCatalog.NormalizeRequiredCategoryAsync(category, nameof(category), cancellationToken);
    }

    private async Task ApplyProfileFieldsAsync(
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
        string category,
        CancellationToken cancellationToken)
    {
        var nurse = user.NurseProfile!;

        user.Name = name.Trim();
        user.LastName = lastName.Trim();
        user.IdentificationNumber = identificationNumber.Trim();
        user.Phone = phone.Trim();
        user.Email = email.Trim();
        user.DisplayName = $"{user.Name} {user.LastName}".Trim();

        nurse.HireDate = hireDate;
        nurse.Specialty = await _nurseCatalog.NormalizeRequiredSpecialtyAsync(specialty, nameof(specialty), cancellationToken);
        nurse.LicenseId = TrimOptional(licenseId);
        nurse.BankName = bankName.Trim();
        nurse.AccountNumber = TrimOptional(accountNumber);
        nurse.Category = await _nurseCatalog.NormalizeRequiredCategoryAsync(category, nameof(category), cancellationToken);
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
            && !string.IsNullOrWhiteSpace(nurse.Specialty?.Trim())
            && !string.IsNullOrWhiteSpace(nurse.BankName)
            && !string.IsNullOrWhiteSpace(nurse.Category?.Trim());
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
            specialty = nurse?.Specialty,
            licenseId = nurse?.LicenseId,
            bankName = nurse?.BankName,
            accountNumber = nurse?.AccountNumber,
            category = nurse?.Category
        };
    }

    private static string? TrimOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
