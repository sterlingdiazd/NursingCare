namespace NursingCareBackend.Application.AdminPortal.Payroll;

public sealed record NursePeriodHistoryItem(
    Guid PeriodId,
    DateOnly StartDate,
    DateOnly EndDate,
    string Status,
    int ServiceCount,
    decimal TotalCompensation
);

public sealed record NursePeriodDetail(
    Guid PeriodId,
    DateOnly StartDate,
    DateOnly EndDate,
    string Status,
    DateOnly CutoffDate,
    DateOnly PaymentDate,
    decimal TotalGrossCompensation,
    decimal TotalDeductions,
    decimal TotalAdjustments,
    decimal NetCompensation,
    IReadOnlyList<NurseServiceRow> Services
);

public sealed record NurseServiceRow(
    Guid ServiceExecutionId,
    Guid CareRequestId,
    DateOnly ServiceDate,
    decimal BaseCompensation,
    decimal TransportIncentive,
    decimal ComplexityBonus,
    decimal MedicalSuppliesCompensation,
    decimal AdjustmentsTotal,
    decimal DeductionsTotal,
    decimal NetCompensation,
    // Reconciliation: the client invoice that funded this line and whether it was collected.
    // Optional/additive so older clients keep parsing.
    string? InvoiceNumber = null,
    string? ClientPaymentStatus = null
);
