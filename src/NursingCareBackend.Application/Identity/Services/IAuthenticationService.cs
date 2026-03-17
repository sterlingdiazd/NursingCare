using NursingCareBackend.Application.Identity.Commands;
using NursingCareBackend.Application.Identity.Responses;

namespace NursingCareBackend.Application.Identity.Services;

public interface IAuthenticationService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task AssignRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken = default);
    Task<AuthResponse> CreateAdminAsync(AdminSetupRequest request, CancellationToken cancellationToken = default);
}
