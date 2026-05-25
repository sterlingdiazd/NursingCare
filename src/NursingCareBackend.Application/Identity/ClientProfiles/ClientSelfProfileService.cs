using NursingCareBackend.Application.Identity.Repositories;
using NursingCareBackend.Application.Identity.Users;
using NursingCareBackend.Application.Identity.Validation;
using NursingCareBackend.Domain.Identity;

namespace NursingCareBackend.Application.Identity.ClientProfiles;

public sealed class ClientSelfProfileService : IClientSelfProfileService
{
  private readonly IUserRepository _users;

  public ClientSelfProfileService(IUserRepository users)
  {
    _users = users;
  }

  public async Task<ClientSelfProfileResponse?> GetAsync(
    Guid userId,
    CancellationToken cancellationToken = default)
  {
    var user = await _users.GetByIdAsync(userId, cancellationToken);
    if (user is null)
    {
      return null;
    }

    EnsureClientCanManageProfile(user);
    return ClientSelfProfileMapper.FromUser(user);
  }

  public async Task<ClientSelfProfileResponse> UpdateAsync(
    Guid userId,
    UpdateClientSelfProfileRequest request,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(request);

    var user = await _users.GetByIdAsync(userId, cancellationToken);
    if (user is null)
    {
      throw new KeyNotFoundException($"Client user '{userId}' was not found.");
    }

    EnsureClientCanManageProfile(user);
    Validate(request);

    user.Name = request.Name.Trim();
    user.LastName = request.LastName.Trim();
    user.IdentificationNumber = request.IdentificationNumber.Trim();
    user.Phone = request.Phone.Trim();
    user.DisplayName = $"{user.Name} {user.LastName}".Trim();

    await _users.UpdateAsync(user, cancellationToken);

    return ClientSelfProfileMapper.FromUser(user);
  }

  private static void EnsureClientCanManageProfile(User user)
  {
    var hasClientRole = user.UserRoles.Any(
      userRole => userRole.Role.Name == SystemRoles.Client);

    if (user.ProfileType != UserProfileType.CLIENT || user.ClientProfile is null || !hasClientRole)
    {
      throw new UnauthorizedAccessException("The authenticated user is not a client account.");
    }

    if (!user.IsActive || UserAccountStateEvaluator.RequiresProfileCompletion(user))
    {
      throw new InvalidOperationException("The client account is not ready for profile editing.");
    }
  }

  private static void Validate(UpdateClientSelfProfileRequest request)
  {
    IdentityInputRules.EnsureTextOnlyRequired(request.Name, nameof(request.Name), "Name");
    IdentityInputRules.EnsureTextOnlyRequired(request.LastName, nameof(request.LastName), "Last name");
    IdentityInputRules.EnsureIdentificationNumber(request.IdentificationNumber, nameof(request.IdentificationNumber));
    IdentityInputRules.EnsurePhone(request.Phone, nameof(request.Phone));
  }
}
