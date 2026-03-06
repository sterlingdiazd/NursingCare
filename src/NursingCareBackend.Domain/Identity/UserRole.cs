namespace NursingCareBackend.Domain.Identity;
// TODO: Convert this class to a table in the database by adding a migration in next iteration
public sealed class UserRole
{
  public Guid UserId { get; set; }
  public User User { get; set; } = default!;

  public Guid RoleId { get; set; }
  public Role Role { get; set; } = default!;
}

