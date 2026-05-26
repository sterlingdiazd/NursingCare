namespace NursingCareBackend.Application.Communications;

/// <summary>
/// Centralized "DEMO communications redirect" configuration. When <see cref="Enabled"/> is
/// true, EVERY outgoing email and every wa.me WhatsApp link is redirected to a single
/// configured demo contact (the owner) so demos never message real nurses or clients.
/// When disabled, communications go to their real recipients (production behavior).
/// </summary>
public sealed class DemoCommunicationsOptions
{
    public const string SectionName = "DemoCommunications";

    /// <summary>
    /// Master switch for the demo redirect. Defaults to <c>true</c> on this safe-by-default
    /// branch so a demo never accidentally messages a real recipient.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Email address that ALL outgoing email is redirected to while <see cref="Enabled"/>.
    /// When empty, the redirect is a no-op for email (real recipient is used).
    /// </summary>
    public string ContactEmail { get; set; } = string.Empty;

    /// <summary>
    /// Phone number that ALL wa.me WhatsApp links target while <see cref="Enabled"/>.
    /// When empty, the redirect is a no-op for WhatsApp (real recipient phone is used).
    /// </summary>
    public string ContactPhone { get; set; } = string.Empty;
}
