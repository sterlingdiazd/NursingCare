namespace NursingCareBackend.Infrastructure.Notifications;

public sealed class OverdueNotificationOptions
{
    public const string SectionName = "OverdueNotifications";

    /// <summary>
    /// The UTC hour (0–23) at which the daily summary runs. Default: 8 (08:00 UTC).
    /// </summary>
    public int RunHourUtc { get; set; } = 8;
}
