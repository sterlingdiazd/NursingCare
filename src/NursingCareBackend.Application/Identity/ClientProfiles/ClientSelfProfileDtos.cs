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
  string? PreferredAddress,
  string? EmergencyContactName,
  string? EmergencyContactPhone,
  bool IsActive,
  DateTime CreatedAtUtc);

public sealed record UpdateClientSelfProfileRequest(
  string Name,
  string LastName,
  string IdentificationNumber,
  string Phone,
  string? PreferredAddress = null,
  string? EmergencyContactName = null,
  string? EmergencyContactPhone = null);

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
      user.PreferredAddress,
      user.EmergencyContactName,
      user.EmergencyContactPhone,
      user.IsActive,
      user.CreatedAtUtc);
}
