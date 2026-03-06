using NursingCareBackend.Domain.CareRequests;

namespace NursingCareBackend.Api.Controllers.CareRequests;

public sealed record CareRequestResponse(
  Guid Id,
  Guid ResidentId,
  string Description,
  string Status,
  DateTime CreatedAtUtc)
{
  public static CareRequestResponse FromDomain(CareRequest careRequest)
  {
    return new CareRequestResponse(
      careRequest.Id,
      careRequest.ResidentId,
      careRequest.Description,
      careRequest.Status.ToString(),
      careRequest.CreatedAtUtc);
  }
}

