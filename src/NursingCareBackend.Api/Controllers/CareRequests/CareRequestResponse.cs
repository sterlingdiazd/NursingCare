using NursingCareBackend.Domain.CareRequests;

namespace NursingCareBackend.Api.Controllers.CareRequests;

public sealed record CareRequestResponse(
  Guid Id,
  Guid UserID,
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
  Guid? AssignedNurse,
  string Status,
  DateTime CreatedAtUtc,
  DateTime UpdatedAtUtc,
  DateTime? ApprovedAtUtc,
  DateTime? RejectedAtUtc,
  DateTime? CompletedAtUtc,
  DateTime? CancelledAtUtc,
  string? RejectionReason,
  string? PricingCategoryCode,
  decimal? CategoryFactorSnapshot,
  decimal? DistanceFactorMultiplierSnapshot,
  decimal? ComplexityMultiplierSnapshot,
  int? VolumeDiscountPercentSnapshot,
  decimal? LineBeforeVolumeDiscount,
  decimal? UnitPriceAfterVolumeDiscount,
  decimal? SubtotalBeforeSupplies,
  // Billing fields — visible to the owning client (their own price + invoice/payment status only).
  // Nurse pay, cost, and margin internals are intentionally excluded from this DTO.
  string? InvoiceNumber,
  DateTime? InvoicedAtUtc,
  DateTime? PaidAtUtc,
  DateTime? VoidedAtUtc,
  string PaymentStatus)
{
  /// <summary>
  /// Derives a human-readable payment status string from the billing timestamp fields.
  /// "Anulado" takes precedence; then "Pagado"; then "Facturado"; else "Pendiente de factura".
  /// </summary>
  public static string DerivePaymentStatus(CareRequest careRequest)
  {
    if (careRequest.VoidedAtUtc.HasValue) return "Anulado";
    if (careRequest.PaidAtUtc.HasValue) return "Pagado";
    if (careRequest.Status == CareRequestStatus.PaymentReported) return "Pago reportado";
    if (careRequest.InvoicedAtUtc.HasValue) return "Facturado";
    return "Pendiente de factura";
  }

  public static CareRequestResponse FromDomain(CareRequest careRequest)
  {
    return new CareRequestResponse(
      careRequest.Id,
      careRequest.UserID,
      careRequest.Description,
      careRequest.CareRequestType,
      careRequest.Unit,
      careRequest.UnitType,
      careRequest.Price,
      careRequest.Total,
      careRequest.DistanceFactor,
      careRequest.ComplexityLevel,
      careRequest.ClientBasePrice,
      careRequest.MedicalSuppliesCost,
      careRequest.CareRequestDate,
      careRequest.SuggestedNurse,
      careRequest.AssignedNurse,
      careRequest.Status.ToString(),
      careRequest.CreatedAtUtc,
      careRequest.UpdatedAtUtc,
      careRequest.ApprovedAtUtc,
      careRequest.RejectedAtUtc,
      careRequest.CompletedAtUtc,
      careRequest.CancelledAtUtc,
      careRequest.RejectionReason,
      careRequest.PricingCategoryCode,
      careRequest.CategoryFactorSnapshot,
      careRequest.DistanceFactorMultiplierSnapshot,
      careRequest.ComplexityMultiplierSnapshot,
      careRequest.VolumeDiscountPercentSnapshot,
      careRequest.LineBeforeVolumeDiscount,
      careRequest.UnitPriceAfterVolumeDiscount,
      careRequest.SubtotalBeforeSupplies,
      careRequest.InvoiceNumber,
      careRequest.InvoicedAtUtc,
      careRequest.PaidAtUtc,
      careRequest.VoidedAtUtc,
      DerivePaymentStatus(careRequest));
  }
}
