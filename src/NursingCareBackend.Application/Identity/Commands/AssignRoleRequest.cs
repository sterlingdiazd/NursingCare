namespace NursingCareBackend.Application.Identity.Commands;

public sealed record AssignRoleRequest(
    string UserId,
    string RoleName
);
