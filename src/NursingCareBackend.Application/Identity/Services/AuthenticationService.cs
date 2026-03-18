using NursingCareBackend.Application.Identity.Authentication;
using NursingCareBackend.Application.Identity.OAuth;
using NursingCareBackend.Application.Identity.Commands;
using NursingCareBackend.Application.Identity.Repositories;
using NursingCareBackend.Application.Identity.Responses;
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

    public AuthenticationService(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IPasswordHasher passwordHasher,
        ITokenGenerator tokenGenerator,
        IGoogleOAuthClient googleOAuthClient)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _passwordHasher = passwordHasher;
        _tokenGenerator = tokenGenerator;
        _googleOAuthClient = googleOAuthClient;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
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

        if (request.Password != request.ConfirmPassword)
        {
            throw new ArgumentException("Passwords do not match.", nameof(request.ConfirmPassword));
        }

        if (request.Password.Length < 6)
        {
            throw new ArgumentException("Password must be at least 6 characters long.", nameof(request.Password));
        }

        // Check if user already exists
        var existingUser = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (existingUser is not null)
        {
            throw new InvalidOperationException("User with this email already exists.");
        }

        // Determine if user should be active based on profile type
        // Clients are immediately active, Nurses require admin approval
        bool isActive = request.ProfileType == UserProfileType.Client;

        // Create new user
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            PasswordHash = _passwordHasher.Hash(request.Password),
            IsActive = isActive,
            CreatedAtUtc = DateTime.UtcNow
        };

        // Add appropriate role based on profile type
        string roleName = request.ProfileType == UserProfileType.Nurse ? "Nurse" : "User";
        var role = await _roleRepository.GetByNameAsync(roleName, cancellationToken);
        if (role is not null)
        {
            user.UserRoles.Add(new UserRole
            {
                UserId = user.Id,
                RoleId = role.Id,
                Role = role
            });
        }

        // Save user
        await _userRepository.CreateAsync(user, cancellationToken);

        // For Nurses, return response without token (account inactive)
        if (request.ProfileType == UserProfileType.Nurse)
        {
            return new AuthResponse(
                Token: string.Empty,
                RefreshToken: string.Empty,
                ExpiresAtUtc: null,
                Email: user.Email,
                Roles: new[] { roleName }
            );
        }

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

                if (!existingUser.IsActive)
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

        if (!user.IsActive)
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

        if (!user.IsActive)
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

        // Check if user already exists (regardless of role)
        var existingUser = await _userRepository.GetByEmailAsync(request.AdminEmail, cancellationToken);
        if (existingUser is not null)
        {
            throw new InvalidOperationException($"User with email '{request.AdminEmail}' already exists. Use login instead.");
        }

        // Get Admin role
        var adminRole = await _roleRepository.GetByNameAsync("Admin", cancellationToken);
        if (adminRole is null)
        {
            throw new InvalidOperationException("Admin role not found in the system.");
        }

        // Create new admin user
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.AdminEmail,
            PasswordHash = _passwordHasher.Hash(request.AdminPassword),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

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
        if (user.IsActive)
        {
            throw new InvalidOperationException("User account is already active.");
        }

        // Activate user
        user.IsActive = true;

        // Save changes
        await _userRepository.UpdateAsync(user, cancellationToken);
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
            Email: user.Email,
            Roles: user.UserRoles.Select(ur => ur.Role.Name)
        );
    }

    private async Task<User> CreateGoogleUserAsync(
        GoogleOAuthUserInfo googleUser,
        CancellationToken cancellationToken)
    {
        var userRole = await _roleRepository.GetByNameAsync("User", cancellationToken);
        if (userRole is null)
        {
            throw new InvalidOperationException("User role not found in the system.");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = googleUser.Email,
            DisplayName = googleUser.Name,
            GoogleSubjectId = googleUser.Subject,
            PasswordHash = _passwordHasher.Hash(Convert.ToHexString(RandomNumberGenerator.GetBytes(16))),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        user.UserRoles.Add(new UserRole
        {
            UserId = user.Id,
            RoleId = userRole.Id,
            Role = userRole
        });

        return await _userRepository.CreateAsync(user, cancellationToken);
    }
}
