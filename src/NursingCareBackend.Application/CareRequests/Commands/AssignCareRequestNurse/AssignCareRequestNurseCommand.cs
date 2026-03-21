namespace NursingCareBackend.Application.CareRequests.Commands.AssignCareRequestNurse;

public sealed record AssignCareRequestNurseCommand(
    Guid CareRequestId,
    Guid AssignedNurse);
