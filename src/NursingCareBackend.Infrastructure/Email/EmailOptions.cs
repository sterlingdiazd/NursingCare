namespace NursingCareBackend.Infrastructure.Email;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>
    /// Azure Communication Services connection string.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Verified sender email address in ACS (e.g. DoNotReply@{your-domain}.azurecomm.net).
    /// </summary>
    public string SenderAddress { get; set; } = string.Empty;

    /// <summary>
    /// Display name that will appear in the "From" field.
    /// </summary>
    public string SenderDisplayName { get; set; } = "NursingCare";
}
