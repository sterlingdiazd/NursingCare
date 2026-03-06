namespace NursingCareBackend.Domain.Identity;
// TODO: Convert this class to a table in the database by adding a migration in next iteration
public sealed class Role
{
  public Guid Id { get; set; }
  public string Name { get; set; } = default!;

  public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}

