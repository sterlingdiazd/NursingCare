namespace NursingCareBackend.Domain.Identity;

public sealed class Nurse
{
  public Guid UserId { get; set; }
  public User User { get; set; } = default!;

  public bool IsActive { get; set; }
  public DateOnly? HireDate { get; set; }
  public string? Specialty { get; set; }
  public string? LicenseId { get; set; }
  public string? BankName { get; set; }
  public string? AccountNumber { get; set; }
  public string? Category { get; set; }

  // Pago a la enfermera (independiente del precio al cliente).
  // Domicilio/calle: tarifa por dia. Casa hogar: monto mensual prorrateado entre los dias
  // esperados del mes (diaria = HomeCareMonthlyRate / HomeCareMonthlyExpectedDays).
  public decimal VisitDailyRate { get; set; }
  public decimal HomeCareMonthlyRate { get; set; }
  public int HomeCareMonthlyExpectedDays { get; set; } = 30;
}
