using Microsoft.EntityFrameworkCore;
using NursingCareBackend.Application.Identity.Repositories;
using NursingCareBackend.Domain.Identity;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Infrastructure.Identity;

public sealed class RefreshTokenRepository : IRefreshTokenRepository
{
  private readonly NursingCareDbContext _dbContext;

  public RefreshTokenRepository(NursingCareDbContext dbContext)
  {
    _dbContext = dbContext;
  }

  public async Task<RefreshToken> CreateAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default)
  {
    _dbContext.RefreshTokens.Add(refreshToken);
    await _dbContext.SaveChangesAsync(cancellationToken);
    return refreshToken;
  }

  public Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default)
  {
    return _dbContext.RefreshTokens
      .Include(rt => rt.User)
      .ThenInclude(user => user.UserRoles)
      .ThenInclude(userRole => userRole.Role)
      .FirstOrDefaultAsync(rt => rt.Token == token, cancellationToken);
  }

  public async Task RevokeActiveTokensForUserAsync(Guid userId, CancellationToken cancellationToken = default)
  {
    var activeTokens = await _dbContext.RefreshTokens
      .Where(rt => rt.UserId == userId && rt.RevokedAtUtc == null && rt.ExpiresAtUtc > DateTime.UtcNow)
      .ToListAsync(cancellationToken);

    if (activeTokens.Count == 0)
    {
      return;
    }

    var revokedAtUtc = DateTime.UtcNow;

    foreach (var token in activeTokens)
    {
      token.RevokedAtUtc = revokedAtUtc;
    }

    await _dbContext.SaveChangesAsync(cancellationToken);
  }

  public async Task UpdateAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default)
  {
    _dbContext.RefreshTokens.Update(refreshToken);
    await _dbContext.SaveChangesAsync(cancellationToken);
  }
}
