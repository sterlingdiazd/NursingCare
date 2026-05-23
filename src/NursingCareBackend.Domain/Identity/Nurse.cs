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
  // Días laborables/mes bajo la jornada de 44h RD (5.5 días/sem × 52 ÷ 12 = 23.83). Decimal para
  // que la diaria (= HomeCareMonthlyRate / HomeCareMonthlyExpectedDays) sea legalmente exacta.
  public decimal HomeCareMonthlyExpectedDays { get; set; } = 23.83m;
}
