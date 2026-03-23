using System.Text.Json;
using NursingCareBackend.Application.AdminPortal.Auditing;
using NursingCareBackend.Application.Identity.Authentication;
using NursingCareBackend.Application.Identity.Repositories;
using NursingCareBackend.Application.Identity.Validation;
using NursingCareBackend.Application.AdminPortal.Notifications;
using NursingCareBackend.Domain.Identity;

namespace NursingCareBackend.Application.AdminPortal.Users;

public sealed class AdminAccountProvisioningService : IAdminAccountProvisioningService
{
  private readonly IUserRepository _userRepository;
  private readonly IRoleRepository _roleRepository;
  private readonly IPasswordHasher _passwordHasher;
  private readonly IAdminUserManagementRepository _adminUserManagementRepository;
  private readonly IAdminAuditService _adminAuditService;
  private readonly IAdminNotificationPublisher _notifications;

  public AdminAccountProvisioningService(
    IUserRepository userRepository,
    IRoleRepository roleRepository,
    IPasswordHasher passwordHasher,
    IAdminUserManagementRepository adminUserManagementRepository,
    IAdminAuditService adminAuditService,
    IAdminNotificationPublisher notifications)
  {
    _userRepository = userRepository;
    _roleRepository = roleRepository;
    _passwordHasher = passwordHasher;
    _adminUserManagementRepository = adminUserManagementRepository;
    _adminAuditService = adminAuditService;
    _notifications = notifications;
  }

  public async Task<AdminUserDetail> CreateAsync(
    CreateAdminAccountRequest request,
    Guid actorUserId,
    CancellationToken cancellationToken = default)
  {
    if (actorUserId == Guid.Empty)
    {
      throw new UnauthorizedAccessException("A valid admin user identifier is required to create an admin account.");
    }

    ValidateRequest(request);

    var normalizedEmail = request.Email.Trim();
    var existingUser = await _userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);
    if (existingUser is not null)
    {
      throw new InvalidOperationException("User with this email already exists.");
    }

    var adminRole = await _roleRepository.GetByNameAsync(SystemRoles.Admin, cancellationToken);
    if (adminRole is null)
    {
      throw new InvalidOperationException("Admin role not found in the system.");
    }

    var user = new User
    {
      Id = Guid.NewGuid(),
      Name = request.Name.Trim(),
      LastName = request.LastName.Trim(),
      IdentificationNumber = request.IdentificationNumber.Trim(),
      Phone = request.Phone.Trim(),
      Email = normalizedEmail,
      DisplayName = $"{request.Name.Trim()} {request.LastName.Trim()}".Trim(),
      ProfileType = UserProfileType.Client,
      PasswordHash = _passwordHasher.Hash(request.Password),
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

    await _userRepository.CreateAsync(user, cancellationToken);

    await _adminAuditService.WriteAsync(
      new AdminAuditRecord(
        ActorUserId: actorUserId,
        ActorRole: SystemRoles.Admin,
        Action: AdminAuditActions.AdminAccountCreated,
        EntityType: "User",
        EntityId: user.Id.ToString(),
        Notes: $"Admin account created for {user.Email}.",
        MetadataJson: JsonSerializer.Serialize(new
        {
          email = user.Email,
          profileType = user.ProfileType.ToString(),
          roleNames = new[] { SystemRoles.Admin }
        })),
      cancellationToken);

    await _notifications.PublishToAdminsAsync(
      new AdminNotificationPublishRequest(
        Category: "admin_account_created",
        Severity: "Medium",
        Title: "Nueva cuenta administrativa creada",
        Body: $"Se creo la cuenta administrativa {user.Email}.",
        EntityType: "User",
        EntityId: user.Id.ToString(),
        DeepLinkPath: $"/admin/users/{user.Id}",
        Source: "Administracion",
        RequiresAction: false),
      cancellationToken);

    var detail = await _adminUserManagementRepository.GetByIdAsync(user.Id, cancellationToken);
    if (detail is null)
    {
      throw new KeyNotFoundException($"User with ID {user.Id} not found.");
    }

    return detail;
  }

  private static void ValidateRequest(CreateAdminAccountRequest request)
  {
    IdentityInputRules.EnsureTextOnlyRequired(request.Name, nameof(request.Name), "Name");
    IdentityInputRules.EnsureTextOnlyRequired(request.LastName, nameof(request.LastName), "Last name");
    IdentityInputRules.EnsureIdentificationNumber(request.IdentificationNumber, nameof(request.IdentificationNumber));
    IdentityInputRules.EnsurePhone(request.Phone, nameof(request.Phone));

    if (string.IsNullOrWhiteSpace(request.Email))
    {
      throw new ArgumentException("Email is required.", nameof(request.Email));
    }

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
}
