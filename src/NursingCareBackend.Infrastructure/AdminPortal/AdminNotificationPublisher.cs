using Microsoft.EntityFrameworkCore;
using NursingCareBackend.Application.AdminPortal.Notifications;
using NursingCareBackend.Domain.Admin;
using NursingCareBackend.Domain.Identity;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Infrastructure.AdminPortal;

public sealed class AdminNotificationPublisher : IAdminNotificationPublisher
{
  private readonly NursingCareDbContext _dbContext;

  public AdminNotificationPublisher(NursingCareDbContext dbContext)
  {
    _dbContext = dbContext;
  }

  public async Task PublishToAdminsAsync(
    AdminNotificationPublishRequest request,
    CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(request.Category))
    {
      throw new ArgumentException("Notification category is required.", nameof(request.Category));
    }

    if (string.IsNullOrWhiteSpace(request.Severity))
    {
      throw new ArgumentException("Notification severity is required.", nameof(request.Severity));
    }

    if (string.IsNullOrWhiteSpace(request.Title))
    {
      throw new ArgumentException("Notification title is required.", nameof(request.Title));
    }

    if (string.IsNullOrWhiteSpace(request.Body))
    {
      throw new ArgumentException("Notification body is required.", nameof(request.Body));
    }

    var adminRecipientIds = await _dbContext.Users
      .AsNoTracking()
      .Where(user => user.IsActive && user.UserRoles.Any(userRole => userRole.Role.Name == SystemRoles.Admin))
      .Select(user => user.Id)
      .Distinct()
      .ToListAsync(cancellationToken);

    if (adminRecipientIds.Count == 0)
    {
      return;
    }

    var createdAtUtc = DateTime.UtcNow;
    var notifications = adminRecipientIds.Select(adminUserId => new AdminNotification
    {
      Id = Guid.NewGuid(),
      RecipientUserId = adminUserId,
      Category = request.Category.Trim(),
      Severity = request.Severity.Trim(),
      Title = request.Title.Trim(),
      Body = request.Body.Trim(),
      EntityType = request.EntityType?.Trim(),
      EntityId = request.EntityId?.Trim(),
      DeepLinkPath = request.DeepLinkPath?.Trim(),
      Source = request.Source?.Trim(),
      RequiresAction = request.RequiresAction,
      IsDismissed = false,
      CreatedAtUtc = createdAtUtc,
      ReadAtUtc = null,
      ArchivedAtUtc = null,
      CreatedBySystem = request.CreatedBySystem,
    });

    await _dbContext.AdminNotifications.AddRangeAsync(notifications, cancellationToken);
    await _dbContext.SaveChangesAsync(cancellationToken);
  }
}
