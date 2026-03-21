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
  DateTime? CompletedAtUtc)
{
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
      careRequest.CompletedAtUtc);
  }
}
