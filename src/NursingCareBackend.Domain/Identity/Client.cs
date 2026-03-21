namespace NursingCareBackend.Domain.Identity;

public sealed class Client
{
  public Guid UserId { get; set; }
  public User User { get; set; } = default!;
}
