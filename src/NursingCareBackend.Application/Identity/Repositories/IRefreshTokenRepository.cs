using NursingCareBackend.Domain.Identity;

namespace NursingCareBackend.Application.Identity.Repositories;

public interface IRefreshTokenRepository
{
  Task<RefreshToken> CreateAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default);
  Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default);
  Task RevokeActiveTokensForUserAsync(Guid userId, CancellationToken cancellationToken = default);
  Task UpdateAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default);
}
