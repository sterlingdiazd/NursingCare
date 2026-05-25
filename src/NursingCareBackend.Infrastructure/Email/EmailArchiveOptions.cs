namespace NursingCareBackend.Infrastructure.Email;

/// <summary>
/// Configuration for the on-disk email archive (a history of every outgoing email,
/// written as .eml files segmented by date: {RootPath}/yyyy/MM/dd/).
/// </summary>
public sealed class EmailArchiveOptions
{
    public const string SectionName = "EmailArchive";

    /// <summary>
    /// When true, every outgoing email is archived to disk before delivery. Default on.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Root folder for the archive. When empty, defaults to "{ContentRoot}/logs/email-archive"
    /// (next to the application logs). Files land under {RootPath}/yyyy/MM/dd/.
    /// </summary>
    public string RootPath { get; set; } = string.Empty;
}
