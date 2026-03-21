namespace NursingCareBackend.Domain.Identity;

public sealed class Nurse
{
  public Guid UserId { get; set; }
  public User User { get; set; } = default!;

  public DateOnly? HireDate { get; set; }
  public string? Specialty { get; set; }
  public string? LicenseId { get; set; }
  public string? BankName { get; set; }
  public string? AccountNumber { get; set; }
  public string? Category { get; set; }
}
