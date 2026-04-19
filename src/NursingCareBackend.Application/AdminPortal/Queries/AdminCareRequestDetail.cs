using NursingCareBackend.Application.AdminPortal.Shifts;

namespace NursingCareBackend.Application.AdminPortal.Queries;

public sealed record AdminCareRequestBillingInfo(
  string? InvoiceNumber,
  DateTime? InvoicedAtUtc,
  DateTime? PaidAtUtc,
  DateTime? VoidedAtUtc,
  string? VoidReason,
  string? BankReference,
  DateTime? ValidationDate,
  string? ReceiptNumber,
  Guid? ReceiptId,
  DateTime? ReceiptGeneratedAtUtc
);

public sealed record AdminCareRequestDetail(
  Guid Id,
  Guid ClientUserId,
  string ClientDisplayName,
  string ClientEmail,
  string? ClientIdentificationNumber,
  Guid? AssignedNurseUserId,
  string? AssignedNurseDisplayName,
  string? AssignedNurseEmail,
  string CareRequestDescription,
  string CareRequestType,
  int Unit,
  string UnitType,
  decimal Price,
  decimal Total,
  string? DistanceFactor,
  string? ComplexityLevel,
  decimal? ClientBasePrice,
  decimal? MedicalSuppliesCost,
  DateOnly? CareRequestDate,
  string? SuggestedNurse,
  string Status,
  DateTime CreatedAtUtc,
  DateTime UpdatedAtUtc,
  DateTime? ApprovedAtUtc,
  DateTime? RejectedAtUtc,
  DateTime? CompletedAtUtc,
  bool IsOverdueOrStale,
  AdminCareRequestPricingBreakdown PricingBreakdown,
  AdminPayrollCompensationSnapshot? PayrollCompensation,
  IReadOnlyList<AdminShiftRecordSummary> Shifts,
  IReadOnlyList<AdminCareRequestTimelineEvent> Timeline,
  AdminCareRequestBillingInfo? BillingInfo);
