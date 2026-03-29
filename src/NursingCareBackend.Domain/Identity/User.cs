namespace NursingCareBackend.Domain.Identity;

public sealed class User
{
  public Guid Id { get; set; }
  public string Email { get; set; } = default!;
  public UserProfileType ProfileType { get; init; }
  public string? Name { get; set; }
  public string? LastName { get; set; }
  public string? IdentificationNumber { get; set; }
  public string? Phone { get; set; }
  public string? DisplayName { get; set; }
  public string? GoogleSubjectId { get; set; }
  public string PasswordHash { get; set; } = default!;
  public bool IsActive { get; set; }
  public DateTime CreatedAtUtc { get; set; }

  public string? ResetPasswordCode { get; set; }
  public DateTime? ResetPasswordCodeExpiresAtUtc { get; set; }

  public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
  public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
  public Nurse? NurseProfile { get; set; }
  public Client? ClientProfile { get; set; }
}
