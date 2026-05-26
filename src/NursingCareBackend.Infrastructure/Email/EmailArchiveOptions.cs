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

    /// <summary>
    /// How many days of archived .eml day-folders to keep. On each write, day-folders older
    /// than this are best-effort pruned. Set to 0 or negative to disable pruning. Default 90.
    /// </summary>
    public int RetentionDays { get; set; } = 90;

    /// <summary>
    /// Largest single attachment (in bytes) embedded verbatim into the .eml. Attachments above
    /// this size are replaced by a short text placeholder part instead of their base64 bytes,
    /// keeping archive files bounded. Set to 0 or negative to disable the cap. Default 5 MB.
    /// </summary>
    public long MaxAttachmentBytes { get; set; } = 5_000_000;
}
