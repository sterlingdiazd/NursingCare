using NursingCareBackend.Domain.Identity;

namespace NursingCareBackend.Application.Identity.ClientProfiles;

public sealed record ClientSelfProfileResponse(
  Guid UserId,
  string Email,
  string? Name,
  string? LastName,
  string? DisplayName,
  string? IdentificationNumber,
  string? Phone,
  bool IsActive,
  DateTime CreatedAtUtc);

public sealed record UpdateClientSelfProfileRequest(
  string Name,
  string LastName,
  string IdentificationNumber,
  string Phone);

internal static class ClientSelfProfileMapper
{
  public static ClientSelfProfileResponse FromUser(User user)
    => new(
      user.Id,
      user.Email,
      user.Name,
      user.LastName,
      user.DisplayName,
      user.IdentificationNumber,
      user.Phone,
      user.IsActive,
      user.CreatedAtUtc);
}
