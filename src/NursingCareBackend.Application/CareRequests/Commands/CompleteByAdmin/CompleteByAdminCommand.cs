namespace NursingCareBackend.Application.CareRequests.Commands.CompleteByAdmin;

public sealed record CompleteByAdminCommand(Guid CareRequestId, Guid AdminUserId);
