namespace NursingCareBackend.Application.AdminPortal.Reports;

public record PayrollServiceRow(
    string NurseId,
    string NurseName,
    string CareRequestId,
    string CareRequestType,
    string PricingCategoryCode,
    string EmploymentType,
    string ServiceVariant,
    DateTime ExecutedAtUtc,
    decimal CareRequestTotal,
    decimal BaseCompensation,
    decimal TransportIncentive,
    decimal ComplexityBonus,
    decimal MedicalSuppliesCompensation,
    decimal AdjustmentsTotal,
    decimal DeductionsTotal,
    decimal NetCompensation
);

public record PayrollSummaryStaffRow(
    string NurseId,
    string NurseName,
    int ServiceCount,
    decimal GrossCompensation,
    decimal TransportIncentives,
    decimal AdjustmentsTotal,
    decimal DeductionsTotal,
    decimal NetCompensation
);

public record PayrollSummaryReport(
    string PeriodLabel,
    DateOnly StartDate,
    DateOnly EndDate,
    DateOnly CutoffDate,
    DateOnly PaymentDate,
    IReadOnlyList<PayrollSummaryStaffRow> Staff,
    IReadOnlyList<PayrollServiceRow> Services
);
