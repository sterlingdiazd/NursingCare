namespace NursingCareBackend.Infrastructure.Notifications;

public sealed class OverdueNotificationOptions
{
    public const string SectionName = "OverdueNotifications";

    /// <summary>
    /// The UTC hour (0–23) at which the daily summary runs. Default: 8 (08:00 UTC).
    /// </summary>
    public int RunHourUtc { get; set; } = 8;

    /// <summary>
    /// Hours after a service is Completed before the "payment due" reminder is sent to the client and
    /// admin. Payment is due on completion; this is a short grace nudge. Default: 2.
    /// </summary>
    public int DuePaymentReminderAfterHours { get; set; } = 2;

    /// <summary>
    /// Hours after completion before an unpaid request is treated as OVERDUE (reminder to client +
    /// admin, and counted in the daily summary). Default: 24 (next day).
    /// </summary>
    public int OverduePaymentAfterHours { get; set; } = 24;
}
