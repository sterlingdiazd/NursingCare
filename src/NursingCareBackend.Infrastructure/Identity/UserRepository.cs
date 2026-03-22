using Microsoft.EntityFrameworkCore;
using NursingCareBackend.Application.Identity.Models;
using NursingCareBackend.Application.Identity.Repositories;
using NursingCareBackend.Domain.CareRequests;
using NursingCareBackend.Domain.Identity;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Infrastructure.Identity;

public sealed class UserRepository : IUserRepository
{
    private readonly NursingCareDbContext _dbContext;

    public UserRepository(NursingCareDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users
            .Include(u => u.NurseProfile)
            .Include(u => u.ClientProfile)
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
    }

    public async Task<User?> GetByGoogleSubjectIdAsync(
        string googleSubjectId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users
            .Include(u => u.NurseProfile)
            .Include(u => u.ClientProfile)
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.GoogleSubjectId == googleSubjectId, cancellationToken);
    }

    public async Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users
            .Include(u => u.NurseProfile)
            .Include(u => u.ClientProfile)
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
    }

    public Task<bool> AnyAdminExistsAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.Users
            .AsNoTracking()
            .AnyAsync(
                user => user.UserRoles.Any(userRole => userRole.Role.Name == SystemRoles.Admin),
                cancellationToken);
    }

    public async Task<IReadOnlyList<User>> GetNurseProfilesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users
            .AsNoTracking()
            .Include(u => u.NurseProfile)
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .Where(u =>
                u.ProfileType == UserProfileType.Nurse &&
                u.NurseProfile != null)
            .OrderBy(u => u.Name)
            .ThenBy(u => u.LastName)
            .ThenBy(u => u.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<User>> GetPendingNurseProfilesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users
            .Include(u => u.NurseProfile)
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .Where(u =>
                u.ProfileType == UserProfileType.Nurse &&
                u.NurseProfile != null &&
                !u.NurseProfile.IsActive)
            .OrderBy(u => u.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<User>> GetActiveNurseProfilesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users
            .Include(u => u.NurseProfile)
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .Where(u =>
                u.ProfileType == UserProfileType.Nurse &&
                u.IsActive &&
                u.NurseProfile != null &&
                u.NurseProfile.IsActive)
            .OrderBy(u => u.Name)
            .ThenBy(u => u.LastName)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, NurseWorkloadSummary>> GetNurseWorkloadsAsync(
        IReadOnlyCollection<Guid> nurseUserIds,
        CancellationToken cancellationToken = default)
    {
        if (nurseUserIds.Count == 0)
        {
            return new Dictionary<Guid, NurseWorkloadSummary>();
        }

        var workloadRows = await _dbContext.CareRequests
            .AsNoTracking()
            .Where(careRequest =>
                careRequest.AssignedNurse.HasValue &&
                nurseUserIds.Contains(careRequest.AssignedNurse.Value))
            .GroupBy(careRequest => careRequest.AssignedNurse!.Value)
            .Select(group => new
            {
                NurseUserId = group.Key,
                TotalAssignedCareRequests = group.Count(),
                PendingAssignedCareRequests = group.Count(careRequest => careRequest.Status == CareRequestStatus.Pending),
                ApprovedAssignedCareRequests = group.Count(careRequest => careRequest.Status == CareRequestStatus.Approved),
                RejectedAssignedCareRequests = group.Count(careRequest => careRequest.Status == CareRequestStatus.Rejected),
                CompletedAssignedCareRequests = group.Count(careRequest => careRequest.Status == CareRequestStatus.Completed),
                LastCareRequestAtUtc = group.Max(careRequest => (DateTime?)careRequest.UpdatedAtUtc)
            })
            .ToListAsync(cancellationToken);

        return workloadRows.ToDictionary(
            row => row.NurseUserId,
            row => new NurseWorkloadSummary(
                row.TotalAssignedCareRequests,
                row.PendingAssignedCareRequests,
                row.ApprovedAssignedCareRequests,
                row.RejectedAssignedCareRequests,
                row.CompletedAssignedCareRequests,
                row.LastCareRequestAtUtc));
    }

    public Task<bool> HasAssignedCareRequestsAsync(Guid nurseUserId, CancellationToken cancellationToken = default)
    {
        return _dbContext.CareRequests
            .AsNoTracking()
            .AnyAsync(careRequest => careRequest.AssignedNurse == nurseUserId, cancellationToken);
    }

    public async Task<User> CreateAsync(User user, CancellationToken cancellationToken = default)
    {
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return user;
    }

    public async Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        _dbContext.Users.Update(user);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
