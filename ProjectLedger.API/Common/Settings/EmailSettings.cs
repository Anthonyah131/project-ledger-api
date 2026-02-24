namespace ProjectLedger.API.Common.Settings;

/// <summary>
/// Configuración SMTP para envío de correos.
/// Por defecto apunta a Gmail. Configura SmtpUser + SmtpPassword (App Password).
/// </summary>
public class EmailSettings
{
    public const string SectionName = "EmailSettings";

    public string SmtpHost { get; set; } = "smtp.gmail.com";
    public int SmtpPort { get; set; } = 587;

    /// <summary>Tu cuenta Gmail (ej: tucuenta@gmail.com).</summary>
    public string SmtpUser { get; set; } = string.Empty;

    /// <summary>App Password de Gmail (16 caracteres, sin espacios).</summary>
    public string SmtpPassword { get; set; } = string.Empty;

    /// <summary>Dirección remitente — puede ser tu mismo Gmail.</summary>
    public string FromEmail { get; set; } = string.Empty;

    public string FromName { get; set; } = "Project Ledger";

    /// <summary>Email del administrador que recibe notificaciones de nuevos usuarios.</summary>
    public string AdminEmail { get; set; } = string.Empty;

    /// <summary>Si true, los correos se loguean en consola en lugar de enviarse (dev mode).</summary>
    public bool UseFakeProvider { get; set; } = true;
}
