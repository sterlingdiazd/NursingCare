using NursingCareBackend.Domain.Identity;

namespace NursingCareBackend.Application.Identity.Repositories;

public interface IRoleRepository
{
    Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<Role?> GetByIdAsync(Guid roleId, CancellationToken cancellationToken = default);
    Task<Role> CreateAsync(Role role, CancellationToken cancellationToken = default);
    Task<IEnumerable<Role>> GetAllAsync(CancellationToken cancellationToken = default);
}
