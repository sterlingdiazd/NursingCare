using NursingCareBackend.Domain.CareRequests;

namespace NursingCareBackend.Api.Controllers.CareRequests;

public sealed record CareRequestResponse(
  Guid Id,
  Guid ResidentId,
  string Description,
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
      careRequest.ResidentId,
      careRequest.Description,
      careRequest.Status.ToString(),
      careRequest.CreatedAtUtc,
      careRequest.UpdatedAtUtc,
      careRequest.ApprovedAtUtc,
      careRequest.RejectedAtUtc,
      careRequest.CompletedAtUtc);
  }
}
