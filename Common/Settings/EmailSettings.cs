namespace ProjectLedger.API.Common.Settings;

/// <summary>
/// SMTP configuration for sending emails.
/// By default points to Gmail. Configures SmtpUser + SmtpPassword (App Password).
/// </summary>
public class EmailSettings
{
    public const string SectionName = "EmailSettings";

    /// <summary>Hostname of the SMTP server (e.g., 'smtp.gmail.com').</summary>
    public string SmtpHost { get; set; } = "smtp.gmail.com";

    /// <summary>Port of the SMTP server (standard for TLS is 587).</summary>
    public int SmtpPort { get; set; } = 587;

    /// <summary>Your Gmail account (e.g. youraccount@gmail.com).</summary>
    public string SmtpUser { get; set; } = string.Empty;

    /// <summary>Gmail App Password (16 characters, no spaces).</summary>
    public string SmtpPassword { get; set; } = string.Empty;

    /// <summary>Sender address — can be your own Gmail.</summary>
    public string FromEmail { get; set; } = string.Empty;

    /// <summary>DisplayName shown as the sender of the email.</summary>
    public string FromName { get; set; } = "Project Ledger";

    /// <summary>Admin email that receives notifications of new users.</summary>
    public string AdminEmail { get; set; } = string.Empty;

    /// <summary>If true, emails are logged to console instead of being sent (dev mode).</summary>
    public bool UseFakeProvider { get; set; } = false;
}
