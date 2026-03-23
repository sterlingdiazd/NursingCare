using System.Text.Json;
using NursingCareBackend.Application.AdminPortal.Auditing;
using NursingCareBackend.Application.Identity.Authentication;
using NursingCareBackend.Application.Identity.Repositories;
using NursingCareBackend.Application.Identity.Validation;
using NursingCareBackend.Application.AdminPortal.Notifications;
using NursingCareBackend.Domain.Identity;

namespace NursingCareBackend.Application.AdminPortal.Clients;

public sealed class AdminClientManagementService : IAdminClientManagementService
{
  private readonly IUserRepository _userRepository;
  private readonly IRoleRepository _roleRepository;
  private readonly IPasswordHasher _passwordHasher;
  private readonly IRefreshTokenRepository _refreshTokenRepository;
  private readonly IAdminClientManagementRepository _adminClientManagementRepository;
  private readonly IAdminAuditService _adminAuditService;
  private readonly IAdminNotificationPublisher _notifications;

  public AdminClientManagementService(
    IUserRepository userRepository,
    IRoleRepository roleRepository,
    IPasswordHasher passwordHasher,
    IRefreshTokenRepository refreshTokenRepository,
    IAdminClientManagementRepository adminClientManagementRepository,
    IAdminAuditService adminAuditService,
    IAdminNotificationPublisher notifications)
  {
    _userRepository = userRepository;
    _roleRepository = roleRepository;
    _passwordHasher = passwordHasher;
    _refreshTokenRepository = refreshTokenRepository;
    _adminClientManagementRepository = adminClientManagementRepository;
    _adminAuditService = adminAuditService;
    _notifications = notifications;
  }

  public async Task<AdminClientDetail> CreateClientAsync(
    AdminCreateClientRequest request,
    Guid actorUserId,
    CancellationToken cancellationToken = default)
  {
    EnsureActorUserId(actorUserId);
    ValidateCreateRequest(request);

    var normalizedEmail = request.Email.Trim();
    var existingUser = await _userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);
    if (existingUser is not null)
    {
      throw new InvalidOperationException("User with this email already exists.");
    }

    var clientRole = await _roleRepository.GetByNameAsync(SystemRoles.Client, cancellationToken);
    if (clientRole is null)
    {
      throw new InvalidOperationException("Client role not found in the system.");
    }

    var user = new User
    {
      Id = Guid.NewGuid(),
      Email = normalizedEmail,
      ProfileType = UserProfileType.Client,
      Name = request.Name.Trim(),
      LastName = request.LastName.Trim(),
      IdentificationNumber = request.IdentificationNumber.Trim(),
      Phone = request.Phone.Trim(),
      DisplayName = $"{request.Name.Trim()} {request.LastName.Trim()}".Trim(),
      PasswordHash = _passwordHasher.Hash(request.Password),
      IsActive = true,
      CreatedAtUtc = DateTime.UtcNow,
      ClientProfile = new Client()
    };

    user.ClientProfile.UserId = user.Id;
    user.ClientProfile.User = user;
    user.UserRoles.Add(new UserRole
    {
      UserId = user.Id,
      RoleId = clientRole.Id,
      Role = clientRole,
    });

    await _userRepository.CreateAsync(user, cancellationToken);

    await _adminAuditService.WriteAsync(
      new AdminAuditRecord(
        ActorUserId: actorUserId,
        ActorRole: SystemRoles.Admin,
        Action: AdminAuditActions.ClientProfileCreatedByAdmin,
        EntityType: "ClientProfile",
        EntityId: user.Id.ToString(),
        Notes: $"Client profile created for {user.Email}.",
        MetadataJson: JsonSerializer.Serialize(new
        {
          after = CreateAuditSnapshot(user)
        })),
      cancellationToken);

    return await GetRequiredDetailAsync(user.Id, cancellationToken);
  }

  public async Task<AdminClientDetail> UpdateClientAsync(
    Guid userId,
    AdminUpdateClientRequest request,
    Guid actorUserId,
    CancellationToken cancellationToken = default)
  {
    EnsureActorUserId(actorUserId);

    var user = await GetRequiredClientUserAsync(userId, cancellationToken);
    ValidateIdentityRequest(
      request.Name,
      request.LastName,
      request.IdentificationNumber,
      request.Phone,
      request.Email);

    var normalizedEmail = request.Email.Trim();
    var existingUser = await _userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);
    if (existingUser is not null && existingUser.Id != userId)
    {
      throw new InvalidOperationException("User with this email already exists.");
    }

    var before = CreateAuditSnapshot(user);
    user.Name = request.Name.Trim();
    user.LastName = request.LastName.Trim();
    user.IdentificationNumber = request.IdentificationNumber.Trim();
    user.Phone = request.Phone.Trim();
    user.Email = normalizedEmail;
    user.DisplayName = $"{user.Name} {user.LastName}".Trim();

    await _userRepository.UpdateAsync(user, cancellationToken);

    await _adminAuditService.WriteAsync(
      new AdminAuditRecord(
        ActorUserId: actorUserId,
        ActorRole: SystemRoles.Admin,
        Action: AdminAuditActions.ClientProfileUpdated,
        EntityType: "ClientProfile",
        EntityId: user.Id.ToString(),
        Notes: $"Client profile updated for {user.Email}.",
        MetadataJson: JsonSerializer.Serialize(new
        {
          before,
          after = CreateAuditSnapshot(user)
        })),
      cancellationToken);

    return await GetRequiredDetailAsync(user.Id, cancellationToken);
  }

  public async Task<AdminClientDetail> UpdateClientActiveStateAsync(
    Guid userId,
    AdminSetClientActiveStateRequest request,
    Guid actorUserId,
    CancellationToken cancellationToken = default)
  {
    EnsureActorUserId(actorUserId);

    var user = await GetRequiredClientUserAsync(userId, cancellationToken);
    if (user.IsActive == request.IsActive)
    {
      return await GetRequiredDetailAsync(user.Id, cancellationToken);
    }

    var before = CreateAuditSnapshot(user);
    user.IsActive = request.IsActive;
    await _userRepository.UpdateAsync(user, cancellationToken);

    if (!request.IsActive)
    {
      await _refreshTokenRepository.RevokeActiveTokensForUserAsync(user.Id, cancellationToken);
      await _notifications.PublishToAdminsAsync(
        new AdminNotificationPublishRequest(
          Category: "user_deactivated",
          Severity: "High",
          Title: "Cliente desactivado",
          Body: $"La cuenta de cliente {user.Email} fue desactivada.",
          EntityType: "User",
          EntityId: user.Id.ToString(),
          DeepLinkPath: $"/admin/clients/{user.Id}",
          Source: "Administracion",
          RequiresAction: false),
        cancellationToken);
    }

    await _adminAuditService.WriteAsync(
      new AdminAuditRecord(
        ActorUserId: actorUserId,
        ActorRole: SystemRoles.Admin,
        Action: AdminAuditActions.ClientActiveStateChanged,
        EntityType: "ClientProfile",
        EntityId: user.Id.ToString(),
        Notes: request.IsActive
          ? $"Client profile activated for {user.Email}."
          : $"Client profile deactivated for {user.Email}.",
        MetadataJson: JsonSerializer.Serialize(new
        {
          before,
          after = CreateAuditSnapshot(user)
        })),
      cancellationToken);

    return await GetRequiredDetailAsync(user.Id, cancellationToken);
  }

  private async Task<User> GetRequiredClientUserAsync(Guid userId, CancellationToken cancellationToken)
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

    if (user.ProfileType != UserProfileType.Client
      || user.ClientProfile is null
      || !HasClientRole(user))
    {
      throw new InvalidOperationException("The requested user does not have a client profile.");
    }

    return user;
  }

  private async Task<AdminClientDetail> GetRequiredDetailAsync(Guid userId, CancellationToken cancellationToken)
  {
    var detail = await _adminClientManagementRepository.GetByIdAsync(userId, cancellationToken);
    if (detail is null)
    {
      throw new KeyNotFoundException($"User with ID {userId} not found.");
    }

    return detail;
  }

  private static void EnsureActorUserId(Guid actorUserId)
  {
    if (actorUserId == Guid.Empty)
    {
      throw new InvalidOperationException("A valid admin user identifier is required to manage client profiles.");
    }
  }

  private static void ValidateCreateRequest(AdminCreateClientRequest request)
  {
    ValidateIdentityRequest(
      request.Name,
      request.LastName,
      request.IdentificationNumber,
      request.Phone,
      request.Email);

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

  private static void ValidateIdentityRequest(
    string name,
    string lastName,
    string identificationNumber,
    string phone,
    string email)
  {
    IdentityInputRules.EnsureTextOnlyRequired(name, nameof(name), "Name");
    IdentityInputRules.EnsureTextOnlyRequired(lastName, nameof(lastName), "Last name");
    IdentityInputRules.EnsureIdentificationNumber(identificationNumber, nameof(identificationNumber));
    IdentityInputRules.EnsurePhone(phone, nameof(phone));

    if (string.IsNullOrWhiteSpace(email))
    {
      throw new ArgumentException("Email is required.", nameof(email));
    }
  }

  private static bool HasClientRole(User user)
  {
    return user.UserRoles.Any(userRole => string.Equals(
      userRole.Role.Name,
      SystemRoles.Client,
      StringComparison.OrdinalIgnoreCase));
  }

  private static object CreateAuditSnapshot(User user)
  {
    return new
    {
      email = user.Email,
      name = user.Name,
      lastName = user.LastName,
      identificationNumber = user.IdentificationNumber,
      phone = user.Phone,
      isActive = user.IsActive,
      profileType = user.ProfileType.ToString(),
      roleNames = user.UserRoles
        .Select(userRole => userRole.Role.Name)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(roleName => roleName, StringComparer.OrdinalIgnoreCase)
        .ToArray()
    };
  }
}
