using Microsoft.Extensions.Logging;
using NursingCareBackend.Application.Catalogs;
using NursingCareBackend.Application.Identity.Authentication;
using NursingCareBackend.Application.Identity.OAuth;
using NursingCareBackend.Application.Identity.Commands;
using NursingCareBackend.Application.Identity.Repositories;
using NursingCareBackend.Application.Identity.Responses;
using NursingCareBackend.Application.Identity.Users;
using NursingCareBackend.Application.Identity.Validation;
using NursingCareBackend.Application.AdminPortal.Notifications;
using NursingCareBackend.Domain.Identity;
using System.Security.Cryptography;

namespace NursingCareBackend.Application.Identity.Services;

public sealed class AuthenticationService : IAuthenticationService
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenGenerator _tokenGenerator;
    private readonly IGoogleOAuthClient _googleOAuthClient;
    private readonly IAdminBootstrapPolicy _adminBootstrapPolicy;
    private readonly INurseCatalogService _nurseCatalog;
    private readonly IAdminNotificationPublisher _notifications;
    private readonly ILogger<AuthenticationService> _logger;

    public AuthenticationService(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IPasswordHasher passwordHasher,
        ITokenGenerator tokenGenerator,
        IGoogleOAuthClient googleOAuthClient,
        IAdminBootstrapPolicy adminBootstrapPolicy,
        INurseCatalogService nurseCatalog,
        IAdminNotificationPublisher notifications,
        ILogger<AuthenticationService> logger)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _passwordHasher = passwordHasher;
        _tokenGenerator = tokenGenerator;
        _googleOAuthClient = googleOAuthClient;
        _adminBootstrapPolicy = adminBootstrapPolicy;
        _nurseCatalog = nurseCatalog;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim();

        // Validate input
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Name is required.", nameof(request.Name));
        }

        if (string.IsNullOrWhiteSpace(request.LastName))
        {
            throw new ArgumentException("Last name is required.", nameof(request.LastName));
        }

        if (string.IsNullOrWhiteSpace(request.IdentificationNumber))
        {
            throw new ArgumentException("Identification number is required.", nameof(request.IdentificationNumber));
        }

        if (string.IsNullOrWhiteSpace(request.Phone))
        {
            throw new ArgumentException("Phone is required.", nameof(request.Phone));
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            throw new ArgumentException("Email is required.", nameof(request.Email));
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            throw new ArgumentException("Password is required.", nameof(request.Password));
        }

        IdentityInputRules.EnsureTextOnlyRequired(request.Name, nameof(request.Name), "Name");
        IdentityInputRules.EnsureTextOnlyRequired(request.LastName, nameof(request.LastName), "Last name");
        IdentityInputRules.EnsureIdentificationNumber(request.IdentificationNumber, nameof(request.IdentificationNumber));
        IdentityInputRules.EnsurePhone(request.Phone, nameof(request.Phone));

        if (request.Password != request.ConfirmPassword)
        {
            throw new ArgumentException("Passwords do not match.", nameof(request.ConfirmPassword));
        }

        if (request.Password.Length < 6)
        {
            throw new ArgumentException("Password must be at least 6 characters long.", nameof(request.Password));
        }

        // Check if user already exists
        var existingUser = await _userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (existingUser is not null)
        {
            throw new InvalidOperationException("User with this email already exists.");
        }

        // Create new user
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            LastName = request.LastName.Trim(),
            IdentificationNumber = request.IdentificationNumber.Trim(),
            Phone = request.Phone.Trim(),
            Email = normalizedEmail,
            ProfileType = request.ProfileType,
            PasswordHash = _passwordHasher.Hash(request.Password),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        // Add appropriate role based on profile type
        string roleName = request.ProfileType == UserProfileType.NURSE ? SystemRoles.Nurse : SystemRoles.Client;
        var role = await _roleRepository.GetByNameAsync(roleName, cancellationToken);
        if (role is null)
        {
            throw new InvalidOperationException($"{roleName} role not found in the system.");
        }

        user.UserRoles.Add(new UserRole
        {
            UserId = user.Id,
            RoleId = role.Id,
            Role = role
        });

        if (request.ProfileType == UserProfileType.NURSE)
        {
            await ValidateNurseRegistrationFieldsAsync(request, cancellationToken);
            var specialty = await _nurseCatalog.NormalizeRequiredSpecialtyAsync(
                request.Specialty,
                nameof(request.Specialty),
                cancellationToken);
            user.NurseProfile = new Nurse
            {
                UserId = user.Id,
                IsActive = false,
                HireDate = request.HireDate,
                Specialty = specialty,
                LicenseId = TrimOptional(request.LicenseId),
                BankName = request.BankName?.Trim(),
                AccountNumber = TrimOptional(request.AccountNumber)
            };
        }
        else
        {
            user.ClientProfile = new Client
            {
                UserId = user.Id
            };
        }

        // Save user
        await _userRepository.CreateAsync(user, cancellationToken);

        if (request.ProfileType == UserProfileType.NURSE)
        {
            await _notifications.PublishToAdminsAsync(
                new AdminNotificationPublishRequest(
                    Category: "nurse_registration_created",
                    Severity: "Medium",
                    Title: "Nueva enfermera registrada",
                    Body: $"Se registro una nueva enfermera ({user.Email}) y requiere revision administrativa.",
                    EntityType: "NurseProfile",
                    EntityId: user.Id.ToString(),
                    DeepLinkPath: $"/admin/nurse-profiles/{user.Id}",
                    Source: "Registro publico",
                    RequiresAction: true),
                cancellationToken);

            await _notifications.PublishToAdminsAsync(
                new AdminNotificationPublishRequest(
                    Category: "nurse_profile_pending_completion",
                    Severity: "Medium",
                    Title: "Perfil de enfermeria pendiente de completar",
                    Body: $"El perfil de la enfermera {user.Email} esta pendiente de validacion y activacion operativa.",
                    EntityType: "NurseProfile",
                    EntityId: user.Id.ToString(),
                    DeepLinkPath: $"/admin/nurse-profiles?view=pending&userId={user.Id}",
                    Source: "Registro publico",
                    RequiresAction: true),
                cancellationToken);
        }

        return await CreateAuthResponseAsync(user, cancellationToken);
    }

    public async Task<AuthResponse> CompleteProfileAsync(
        Guid userId,
        CompleteProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));
        }

        ValidateProfileFields(request.Name, request.LastName, request.IdentificationNumber, request.Phone);

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            throw new InvalidOperationException($"User with ID {userId} not found.");
        }

        if (string.IsNullOrWhiteSpace(user.GoogleSubjectId))
        {
            throw new InvalidOperationException("Profile completion is only available for Google-linked users.");
        }

        user.Name = request.Name.Trim();
        user.LastName = request.LastName.Trim();
        user.IdentificationNumber = request.IdentificationNumber.Trim();
        user.Phone = request.Phone.Trim();
        user.IsActive = true;

        if (string.IsNullOrWhiteSpace(user.DisplayName))
        {
            user.DisplayName = $"{user.Name} {user.LastName}".Trim();
        }

        await _userRepository.UpdateAsync(user, cancellationToken);

        return await CreateAuthResponseAsync(user, cancellationToken);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            throw new ArgumentException("Email is required.", nameof(request.Email));
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            throw new ArgumentException("Password is required.", nameof(request.Password));
        }

        // Find user by email
        var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (user is null)
        {
            throw new InvalidOperationException("Invalid email or password.");
        }

        // Verify password
        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new InvalidOperationException("Invalid email or password.");
        }

        // Check if user is active
        if (!user.IsActive)
        {
            throw new InvalidOperationException("User account is not active.");
        }

        return await CreateAuthResponseAsync(user, cancellationToken);
    }

    public async Task<AuthResponse> LoginWithGoogleAsync(
        string authorizationCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(authorizationCode))
        {
            throw new ArgumentException("Google authorization code is required.", nameof(authorizationCode));
        }

        var googleUser = await _googleOAuthClient.GetUserInfoAsync(authorizationCode, cancellationToken);

        if (!googleUser.EmailVerified)
        {
            throw new InvalidOperationException("Google account email is not verified.");
        }

        var user = await _userRepository.GetByGoogleSubjectIdAsync(googleUser.Subject, cancellationToken);

        if (user is null)
        {
            var existingUser = await _userRepository.GetByEmailAsync(googleUser.Email, cancellationToken);

            if (existingUser is not null)
            {
                if (!string.IsNullOrWhiteSpace(existingUser.GoogleSubjectId)
                    && !string.Equals(existingUser.GoogleSubjectId, googleUser.Subject, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Google sign-in is already linked to a different account.");
                }

        if (!existingUser.IsActive && !UserAccountStateEvaluator.RequiresProfileCompletion(existingUser))
        {
            throw new InvalidOperationException("User account is not active.");
        }

                existingUser.GoogleSubjectId = googleUser.Subject;
                if (string.IsNullOrWhiteSpace(existingUser.DisplayName))
                {
                    existingUser.DisplayName = googleUser.Name;
                }

                await _userRepository.UpdateAsync(existingUser, cancellationToken);
                user = existingUser;
            }
            else
            {
                user = await CreateGoogleUserAsync(googleUser, cancellationToken);
            }
        }

        if (!user.IsActive && !UserAccountStateEvaluator.RequiresProfileCompletion(user))
        {
            throw new InvalidOperationException("User account is not active.");
        }

        return await CreateAuthResponseAsync(user, cancellationToken);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            throw new ArgumentException("Refresh token is required.", nameof(request.RefreshToken));
        }

        var existingRefreshToken = await _refreshTokenRepository.GetByTokenAsync(
            request.RefreshToken,
            cancellationToken);

        if (existingRefreshToken is null
            || existingRefreshToken.RevokedAtUtc is not null
            || existingRefreshToken.ExpiresAtUtc <= DateTime.UtcNow)
        {
            throw new InvalidOperationException("Refresh token is invalid or expired.");
        }

        var user = existingRefreshToken.User;

        if (!user.IsActive && !UserAccountStateEvaluator.RequiresProfileCompletion(user))
        {
            throw new InvalidOperationException("User account is not active.");
        }

        existingRefreshToken.RevokedAtUtc = DateTime.UtcNow;
        await _refreshTokenRepository.UpdateAsync(existingRefreshToken, cancellationToken);

        return await CreateAuthResponseAsync(user, cancellationToken);
    }

    public async Task AssignRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken = default)
    {
        // Validate input
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(roleName))
        {
            throw new ArgumentException("Role name is required.", nameof(roleName));
        }

        if (string.Equals(roleName.Trim(), SystemRoles.Admin, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The Admin role can only be assigned through the Admin Portal.");
        }

        // Get user
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            throw new InvalidOperationException($"User with ID {userId} not found.");
        }

        // Get role
        var role = await _roleRepository.GetByNameAsync(roleName, cancellationToken);
        if (role is null)
        {
            throw new InvalidOperationException($"Role '{roleName}' not found.");
        }

        // Check if user already has this role
        if (user.UserRoles.Any(ur => ur.RoleId == role.Id))
        {
            throw new InvalidOperationException($"User already has the '{roleName}' role.");
        }

        // Add role to user
        user.UserRoles.Add(new UserRole
        {
            UserId = user.Id,
            RoleId = role.Id,
            Role = role
        });

        // Save changes
        await _userRepository.UpdateAsync(user, cancellationToken);
    }

    public async Task<AuthResponse> CreateAdminAsync(AdminSetupRequest request, CancellationToken cancellationToken = default)
    {
        await _adminBootstrapPolicy.EnsureSetupAdminAllowedAsync(cancellationToken);

        // Validate input
        if (string.IsNullOrWhiteSpace(request.AdminEmail))
        {
            throw new ArgumentException("Email is required.", nameof(request.AdminEmail));
        }

        if (string.IsNullOrWhiteSpace(request.AdminPassword))
        {
            throw new ArgumentException("Password is required.", nameof(request.AdminPassword));
        }

        if (request.AdminPassword.Length < 6)
        {
            throw new ArgumentException("Password must be at least 6 characters long.", nameof(request.AdminPassword));
        }

        var normalizedEmail = request.AdminEmail.Trim();

        // Check if user already exists (regardless of role)
        var existingUser = await _userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (existingUser is not null)
        {
            throw new InvalidOperationException($"User with email '{normalizedEmail}' already exists. Use login instead.");
        }

        // Get Admin role
        var adminRole = await _roleRepository.GetByNameAsync(SystemRoles.Admin, cancellationToken);
        if (adminRole is null)
        {
            throw new InvalidOperationException("Admin role not found in the system.");
        }

        // Create new admin user
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            ProfileType = UserProfileType.CLIENT,
            PasswordHash = _passwordHasher.Hash(request.AdminPassword),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            ClientProfile = new Client()
        };

        user.ClientProfile.UserId = user.Id;

        user.UserRoles.Add(new UserRole
        {
            UserId = user.Id,
            RoleId = adminRole.Id,
            Role = adminRole
        });

        // Save user
        await _userRepository.CreateAsync(user, cancellationToken);

        return await CreateAuthResponseAsync(user, cancellationToken);
    }

    public async Task ActivateUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // Validate input
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));
        }

        // Get user
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            throw new InvalidOperationException($"User with ID {userId} not found.");
        }

        // Check if already active
        if (user.IsActive && (user.ProfileType != UserProfileType.NURSE || user.NurseProfile?.IsActive == true))
        {
            throw new InvalidOperationException("User account is already active.");
        }

        // Activate user
        user.IsActive = true;
        if (user.ProfileType == UserProfileType.NURSE && user.NurseProfile is not null)
        {
            user.NurseProfile.IsActive = true;
        }

        // Save changes
        await _userRepository.UpdateAsync(user, cancellationToken);
    }

    public async Task RequestPasswordResetAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await _userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);

        // Security: Don't leak if email exists or not, just return successfully if logic finishes
        if (user is null)
        {
            _logger.LogInformation("Password reset requested for non-existing email: {Email}", normalizedEmail);
            return;
        }

        // Generate a 6-digit code
        var code = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
        user.ResetPasswordCode = code;
        user.ResetPasswordCodeExpiresAtUtc = DateTime.UtcNow.AddMinutes(15);

        await _userRepository.UpdateAsync(user, cancellationToken);

        // MOCK: In a real system we would send an email here.
        _logger.LogInformation("PASSWORD RESET CODE FOR {Email}: {Code}", normalizedEmail, code);
    }

    public async Task<AuthResponse> ResetPasswordAsync(string email, string code, string newPassword, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await _userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);

        if (user is null)
        {
            throw new InvalidOperationException("Email o codigo invalido.");
        }

        if (user.ResetPasswordCode != code || user.ResetPasswordCodeExpiresAtUtc < DateTime.UtcNow)
        {
             _logger.LogWarning("Invalid or expired code attempt for {Email}: {Code}", normalizedEmail, code);
            throw new InvalidOperationException("Email o codigo invalido.");
        }

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
        {
            throw new ArgumentException("La contrasena debe tener al menos 6 caracteres.");
        }

        user.PasswordHash = _passwordHasher.Hash(newPassword);
        user.ResetPasswordCode = null;
        user.ResetPasswordCodeExpiresAtUtc = null;

        await _userRepository.UpdateAsync(user, cancellationToken);

        return await CreateAuthResponseAsync(user, cancellationToken);
    }

    private async Task<AuthResponse> CreateAuthResponseAsync(
        User user,
        CancellationToken cancellationToken)
    {
        await _refreshTokenRepository.RevokeActiveTokensForUserAsync(user.Id, cancellationToken);

        var tokenResult = _tokenGenerator.GenerateToken(user);
        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(14)
        };

        await _refreshTokenRepository.CreateAsync(refreshToken, cancellationToken);

        return new AuthResponse(
            Token: tokenResult.Token,
            RefreshToken: refreshToken.Token,
            ExpiresAtUtc: tokenResult.ExpiresAtUtc,
            UserId: user.Id,
            Email: user.Email,
            Roles: user.UserRoles.Select(ur => ur.Role.Name),
            RequiresProfileCompletion: UserAccountStateEvaluator.RequiresProfileCompletion(user),
            RequiresAdminReview: UserAccountStateEvaluator.RequiresAdminReview(user)
        );
    }

    private async Task<User> CreateGoogleUserAsync(
        GoogleOAuthUserInfo googleUser,
        CancellationToken cancellationToken)
    {
        var userRole = await _roleRepository.GetByNameAsync(SystemRoles.Client, cancellationToken);
        if (userRole is null)
        {
            throw new InvalidOperationException("Client role not found in the system.");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = googleUser.Email,
            ProfileType = UserProfileType.CLIENT,
            DisplayName = googleUser.Name,
            GoogleSubjectId = googleUser.Subject,
            PasswordHash = _passwordHasher.Hash(Convert.ToHexString(RandomNumberGenerator.GetBytes(16))),
            IsActive = false,
            CreatedAtUtc = DateTime.UtcNow,
            ClientProfile = new Client()
        };

        user.ClientProfile.UserId = user.Id;

        user.UserRoles.Add(new UserRole
        {
            UserId = user.Id,
            RoleId = userRole.Id,
            Role = userRole
        });

        return await _userRepository.CreateAsync(user, cancellationToken);
    }

    private static void ValidateProfileFields(
        string name,
        string lastName,
        string identificationNumber,
        string phone)
    {
        IdentityInputRules.EnsureTextOnlyRequired(name, nameof(name), "Name");
        IdentityInputRules.EnsureTextOnlyRequired(lastName, nameof(lastName), "Last name");
        IdentityInputRules.EnsureIdentificationNumber(identificationNumber, nameof(identificationNumber));
        IdentityInputRules.EnsurePhone(phone, nameof(phone));
    }

    private async Task ValidateNurseRegistrationFieldsAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        if (request.HireDate is null)
        {
            throw new ArgumentException("Hire date is required for nurse registration.", nameof(request.HireDate));
        }

        if (string.IsNullOrWhiteSpace(request.BankName))
        {
            throw new ArgumentException("Bank name is required for nurse registration.", nameof(request.BankName));
        }

        await _nurseCatalog.NormalizeRequiredSpecialtyAsync(request.Specialty, nameof(request.Specialty), cancellationToken);
        IdentityInputRules.EnsureTextOnlyRequired(request.BankName, nameof(request.BankName), "Bank name");
        IdentityInputRules.EnsureNumericOnlyOptional(request.LicenseId, nameof(request.LicenseId), "License ID");
        IdentityInputRules.EnsureNumericOnlyOptional(request.AccountNumber, nameof(request.AccountNumber), "Account number");
    }

    private static string? TrimOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
