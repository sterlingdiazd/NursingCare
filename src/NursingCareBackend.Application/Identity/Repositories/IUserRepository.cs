using NursingCareBackend.Application.Identity.Models;
using NursingCareBackend.Domain.Identity;

namespace NursingCareBackend.Application.Identity.Repositories;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<User?> GetByGoogleSubjectIdAsync(string googleSubjectId, CancellationToken cancellationToken = default);
    Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> AnyAdminExistsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<User>> GetNurseProfilesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<User>> GetPendingNurseProfilesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<User>> GetActiveNurseProfilesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, NurseWorkloadSummary>> GetNurseWorkloadsAsync(
        IReadOnlyCollection<Guid> nurseUserIds,
        CancellationToken cancellationToken = default);
    Task<bool> HasAssignedCareRequestsAsync(Guid nurseUserId, CancellationToken cancellationToken = default);
    Task<User> CreateAsync(User user, CancellationToken cancellationToken = default);
    Task UpdateAsync(User user, CancellationToken cancellationToken = default);
}
