namespace NursingCareBackend.Domain.Identity;

public sealed class RefreshToken
{
  public Guid Id { get; set; }
  public Guid UserId { get; set; }
  public User User { get; set; } = default!;
  public string Token { get; set; } = default!;
  public DateTime CreatedAtUtc { get; set; }
  public DateTime ExpiresAtUtc { get; set; }
  public DateTime? RevokedAtUtc { get; set; }

  public bool IsActive => RevokedAtUtc is null && ExpiresAtUtc > DateTime.UtcNow;
}
