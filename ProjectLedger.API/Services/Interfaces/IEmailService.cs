namespace ProjectLedger.API.Services;

/// <summary>
/// Servicio de envío de correos electrónicos.
/// </summary>
public interface IEmailService
{
    /// <summary>Envía correo de bienvenida al nuevo usuario.</summary>
    Task SendWelcomeEmailAsync(string toEmail, string fullName, CancellationToken ct = default);

    /// <summary>Notifica al administrador que un nuevo usuario se registró.</summary>
    Task SendNewUserNotificationToAdminAsync(string userEmail, string fullName, CancellationToken ct = default);

    /// <summary>Notifica al usuario que su cuenta fue activada.</summary>
    Task SendAccountActivatedEmailAsync(string toEmail, string fullName, CancellationToken ct = default);

    /// <summary>Notifica al usuario que su cuenta fue desactivada.</summary>
    Task SendAccountDeactivatedEmailAsync(string toEmail, string fullName, CancellationToken ct = default);

    /// <summary>Notifica al usuario que fue agregado a un proyecto.</summary>
    Task SendProjectSharedEmailAsync(string toEmail, string fullName, string projectName, string role, string sharedByName, CancellationToken ct = default);

    /// <summary>Notifica al usuario que su acceso a un proyecto fue revocado.</summary>
    Task SendProjectAccessRevokedEmailAsync(string toEmail, string fullName, string projectName, string revokedByName, CancellationToken ct = default);

    /// <summary>Envía el código OTP de restablecimiento de contraseña al usuario.</summary>
    Task SendPasswordResetEmailAsync(string toEmail, string fullName, string otpCode, CancellationToken ct = default);

    /// <summary>Notifica al usuario que su contraseña fue cambiada exitosamente.</summary>
    Task SendPasswordChangedEmailAsync(string toEmail, string fullName, CancellationToken ct = default);
}
