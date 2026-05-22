using Microsoft.EntityFrameworkCore;
using NursingCareBackend.Application.AdminPortal.Notifications;
using NursingCareBackend.Application.Email;
using NursingCareBackend.Domain.Identity;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Infrastructure.AdminPortal;

public sealed class AdminEmailNotifier : IAdminEmailNotifier
{
    private readonly NursingCareDbContext _dbContext;
    private readonly IEmailService _emailService;

    public AdminEmailNotifier(NursingCareDbContext dbContext, IEmailService emailService)
    {
        _dbContext = dbContext;
        _emailService = emailService;
    }

    public async Task SendToAdminsAsync(string subject, string htmlBody, CancellationToken cancellationToken)
    {
        var emails = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.IsActive
                        && u.UserRoles.Any(r => r.Role.Name == SystemRoles.Admin)
                        && u.Email != null && u.Email != "")
            .Select(u => u.Email)
            .ToListAsync(cancellationToken);

        // AcsEmailService is non-blocking and swallows per-recipient errors; one bad address
        // does not abort the rest.
        foreach (var email in emails)
        {
            await _emailService.SendAsync(email, subject, htmlBody, cancellationToken);
        }
    }
}
