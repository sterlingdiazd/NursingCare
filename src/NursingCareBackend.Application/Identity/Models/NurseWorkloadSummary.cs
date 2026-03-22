namespace NursingCareBackend.Application.Identity.Models;

public sealed record NurseWorkloadSummary(
    int TotalAssignedCareRequests,
    int PendingAssignedCareRequests,
    int ApprovedAssignedCareRequests,
    int RejectedAssignedCareRequests,
    int CompletedAssignedCareRequests,
    DateTime? LastCareRequestAtUtc);
