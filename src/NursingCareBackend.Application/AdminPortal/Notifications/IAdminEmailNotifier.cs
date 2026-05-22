namespace NursingCareBackend.Application.AdminPortal.Notifications;

/// <summary>Sends an email to every active admin (e.g., when a client reports a payment).</summary>
public interface IAdminEmailNotifier
{
    Task SendToAdminsAsync(string subject, string htmlBody, CancellationToken cancellationToken);
}
