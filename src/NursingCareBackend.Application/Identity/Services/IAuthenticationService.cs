using NursingCareBackend.Application.Identity.Commands;
using NursingCareBackend.Application.Identity.Responses;

namespace NursingCareBackend.Application.Identity.Services;

public interface IAuthenticationService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponse> CompleteProfileAsync(Guid userId, CompleteProfileRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponse> LoginWithGoogleAsync(string authorizationCode, CancellationToken cancellationToken = default);
    Task<AuthResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default);
    Task AssignRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken = default);
    Task<AuthResponse> CreateAdminAsync(AdminSetupRequest request, CancellationToken cancellationToken = default);
    Task ActivateUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
