using NursingCareBackend.Application.Identity.Authentication;
using NursingCareBackend.Application.Identity.Commands;
using NursingCareBackend.Application.Identity.Repositories;
using NursingCareBackend.Application.Identity.Responses;
using NursingCareBackend.Domain.Identity;

namespace NursingCareBackend.Application.Identity.Services;

public sealed class AuthenticationService : IAuthenticationService
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenGenerator _tokenGenerator;

    public AuthenticationService(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IPasswordHasher passwordHasher,
        ITokenGenerator tokenGenerator)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _passwordHasher = passwordHasher;
        _tokenGenerator = tokenGenerator;
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

        // Create new user
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            PasswordHash = _passwordHasher.Hash(request.Password),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        // Add default role (e.g., "User" or "Nurse")
        var defaultRole = await _roleRepository.GetByNameAsync("User", cancellationToken);
        if (defaultRole is not null)
        {
            user.UserRoles.Add(new UserRole
            {
                UserId = user.Id,
                RoleId = defaultRole.Id,
                Role = defaultRole
            });
        }

        // Save user
        await _userRepository.CreateAsync(user, cancellationToken);

        // Generate token
        var token = _tokenGenerator.GenerateToken(user);

        return new AuthResponse(
            Token: token,
            Email: user.Email,
            Roles: user.UserRoles.Select(ur => ur.Role.Name)
        );
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

        // Generate token
        var token = _tokenGenerator.GenerateToken(user);

        return new AuthResponse(
            Token: token,
            Email: user.Email,
            Roles: user.UserRoles.Select(ur => ur.Role.Name)
        );
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

        // Generate token
        var token = _tokenGenerator.GenerateToken(user);

        return new AuthResponse(
            Token: token,
            Email: user.Email,
            Roles: user.UserRoles.Select(ur => ur.Role.Name)
        );
    }
}
