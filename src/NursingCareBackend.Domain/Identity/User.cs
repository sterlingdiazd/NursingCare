namespace NursingCareBackend.Domain.Identity;

public sealed class User
{
  public Guid Id { get; set; }
  public string Email { get; set; } = default!;
  public string PasswordHash { get; set; } = default!;
  public bool IsActive { get; set; }
  public DateTime CreatedAtUtc { get; set; }

  public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}

