namespace NursingCareBackend.Application.AdminPortal.Clients;

public interface IAdminClientManagementService
{
  Task<AdminClientDetail> CreateClientAsync(
    AdminCreateClientRequest request,
    Guid actorUserId,
    CancellationToken cancellationToken = default);

  Task<AdminClientDetail> UpdateClientAsync(
    Guid userId,
    AdminUpdateClientRequest request,
    Guid actorUserId,
    CancellationToken cancellationToken = default);

  Task<AdminClientDetail> UpdateClientActiveStateAsync(
    Guid userId,
    AdminSetClientActiveStateRequest request,
    Guid actorUserId,
    CancellationToken cancellationToken = default);
}
