namespace ProjectLedger.API.Common.Settings;

/// <summary>
/// SMTP configuration for sending emails.
/// By default points to Gmail. Configures SmtpUser + SmtpPassword (App Password).
/// </summary>
public class EmailSettings
{
    public const string SectionName = "EmailSettings";

    public string SmtpHost { get; set; } = "smtp.gmail.com";
    public int SmtpPort { get; set; } = 587;

    /// <summary>Your Gmail account (e.g. youraccount@gmail.com).</summary>
    public string SmtpUser { get; set; } = string.Empty;

    /// <summary>Gmail App Password (16 characters, no spaces).</summary>
    public string SmtpPassword { get; set; } = string.Empty;

    /// <summary>Sender address — can be your own Gmail.</summary>
    public string FromEmail { get; set; } = string.Empty;

    public string FromName { get; set; } = "Project Ledger";

    /// <summary>Admin email that receives notifications of new users.</summary>
    public string AdminEmail { get; set; } = string.Empty;

    /// <summary>If true, emails are logged to console instead of being sent (dev mode).</summary>
    public bool UseFakeProvider { get; set; } = false;
}
