using Microsoft.EntityFrameworkCore;
using NursingCareBackend.Application.Notifications;
using NursingCareBackend.Domain.Identity;
using NursingCareBackend.Domain.Notifications;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Infrastructure.Notifications;

public sealed class UserNotificationPublisher : IUserNotificationPublisher
{
  private readonly NursingCareDbContext _dbContext;

  public UserNotificationPublisher(NursingCareDbContext dbContext)
  {
    _dbContext = dbContext;
  }

  public async Task PublishToUserAsync(
    UserNotificationPublishRequest request,
    CancellationToken cancellationToken = default)
  {
    if (request.RecipientUserId == Guid.Empty)
    {
      throw new ArgumentException("Recipient user identifier is required.", nameof(request.RecipientUserId));
    }

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

    var recipientExists = await _dbContext.Users
      .AsNoTracking()
      .AnyAsync(user =>
        user.Id == request.RecipientUserId
        && user.IsActive
        && user.UserRoles.Any(userRole =>
            userRole.Role.Name == SystemRoles.Client || userRole.Role.Name == SystemRoles.Nurse),
        cancellationToken);

    if (!recipientExists)
    {
      return;
    }

    var createdAtUtc = DateTime.UtcNow;
    var notification = new UserNotification
    {
      Id = Guid.NewGuid(),
      RecipientUserId = request.RecipientUserId,
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
    };

    var outboxRow = new NotificationOutbox
    {
      Id = Guid.NewGuid(),
      NotificationId = notification.Id,
      Kind = NotificationOutboxKind.User,
      RecipientUserId = notification.RecipientUserId,
      Source = notification.Source,
      Status = NotificationOutboxStatus.Pending,
      Attempts = 0,
      CreatedAtUtc = createdAtUtc,
    };

    await _dbContext.UserNotifications.AddAsync(notification, cancellationToken);
    await _dbContext.NotificationOutbox.AddAsync(outboxRow, cancellationToken);
    await _dbContext.SaveChangesAsync(cancellationToken);
  }
}
