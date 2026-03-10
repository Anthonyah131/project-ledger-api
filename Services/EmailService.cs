using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace ProjectLedger.API.Services;

/// <summary>
/// Implementación de IEmailService con soporte dual:
/// - UseFakeProvider = true  → solo loguea en consola (desarrollo)
/// - UseFakeProvider = false → envía vía SMTP de Gmail (producción)
/// </summary>
public class EmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<EmailSettings> settings, ILogger<EmailService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    // ── Welcome ─────────────────────────────────────────────

    public async Task SendWelcomeEmailAsync(string toEmail, string fullName, CancellationToken ct = default)
    {
        var subject = "¡Bienvenido a Project Ledger!";
        var body = $"""
            <h2>¡Hola {fullName}!</h2>
            <p>Tu cuenta en <strong>Project Ledger</strong> ha sido creada exitosamente.</p>
            <p>Tu cuenta está pendiente de activación por un administrador.
            Mientras tanto puedes iniciar sesión y explorar la plataforma en modo lectura.</p>
            <p>Recibirás un correo cuando tu cuenta sea activada.</p>
            <br/>
            <p>— El equipo de Project Ledger</p>
            """;

        await SendAsync(toEmail, subject, body, ct);
    }

    // ── Admin notification ──────────────────────────────────

    public async Task SendNewUserNotificationToAdminAsync(string userEmail, string fullName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.AdminEmail))
        {
            _logger.LogWarning("AdminEmail not configured — skipping new user notification.");
            return;
        }

        var subject = $"Nuevo usuario registrado: {fullName}";
        var body = $"""
            <h2>Nuevo registro en Project Ledger</h2>
            <p><strong>Nombre:</strong> {fullName}</p>
            <p><strong>Email:</strong> {userEmail}</p>
            <p>El usuario ha sido creado en estado <strong>desactivado</strong>.
            Accede al panel de administración para revisarlo y activarlo.</p>
            """;

        await SendAsync(_settings.AdminEmail, subject, body, ct);
    }

    // ── Account activated ───────────────────────────────────

    public async Task SendAccountActivatedEmailAsync(string toEmail, string fullName, CancellationToken ct = default)
    {
        var subject = "Tu cuenta ha sido activada — Project Ledger";
        var body = $"""
            <h2>¡Buenas noticias, {fullName}!</h2>
            <p>Tu cuenta en <strong>Project Ledger</strong> ha sido <strong>activada</strong> por un administrador.</p>
            <p>Ahora puedes crear proyectos, registrar gastos y usar todas las funcionalidades de tu plan.</p>
            <br/>
            <p>— El equipo de Project Ledger</p>
            """;

        await SendAsync(toEmail, subject, body, ct);
    }

    // ── Account deactivated ─────────────────────────────────

    public async Task SendAccountDeactivatedEmailAsync(string toEmail, string fullName, CancellationToken ct = default)
    {
        var subject = "Tu cuenta ha sido desactivada — Project Ledger";
        var body = $"""
            <h2>Hola {fullName}</h2>
            <p>Tu cuenta en <strong>Project Ledger</strong> ha sido <strong>desactivada</strong> por un administrador.</p>
            <p>Aún puedes iniciar sesión y consultar tus datos, pero no podrás crear ni modificar información
            hasta que tu cuenta sea reactivada.</p>
            <p>Si crees que esto es un error, contacta al administrador.</p>
            <br/>
            <p>— El equipo de Project Ledger</p>
            """;

        await SendAsync(toEmail, subject, body, ct);
    }

    // ── Project shared ──────────────────────────────────

    public async Task SendProjectSharedEmailAsync(
        string toEmail, string fullName, string projectName,
        string role, string sharedByName, CancellationToken ct = default)
    {
        var roleName = role switch
        {
            "editor" => "Editor",
            "viewer" => "Lector",
            _ => role
        };

        var subject = $"Te han compartido un proyecto — Project Ledger";
        var body = $"""
            <h2>¡Hola {fullName}!</h2>
            <p><strong>{sharedByName}</strong> te ha agregado al proyecto
            <strong>{projectName}</strong> con el rol de <strong>{roleName}</strong>.</p>
            <p>Inicia sesión en Project Ledger para ver el proyecto.</p>
            <br/>
            <p>— El equipo de Project Ledger</p>
            """;

        await SendAsync(toEmail, subject, body, ct);
    }

    // ── Project access revoked ──────────────────────────────

    public async Task SendProjectAccessRevokedEmailAsync(
        string toEmail, string fullName, string projectName,
        string revokedByName, CancellationToken ct = default)
    {
        var subject = $"Se ha revocado tu acceso a un proyecto — Project Ledger";
        var body = $"""
            <h2>Hola {fullName}</h2>
            <p><strong>{revokedByName}</strong> ha revocado tu acceso al proyecto
            <strong>{projectName}</strong>.</p>
            <p>Si crees que esto es un error, contacta al dueño del proyecto.</p>
            <br/>
            <p>— El equipo de Project Ledger</p>
            """;

        await SendAsync(toEmail, subject, body, ct);
    }

    // ── Password reset OTP ────────────────────────────────────

    public async Task SendPasswordResetEmailAsync(
        string toEmail, string fullName, string otpCode, CancellationToken ct = default)
    {
        var subject = "Código para restablecer tu contraseña — Project Ledger";
        var body = $"""
            <h2>Hola {fullName}</h2>
            <p>Recibimos una solicitud para restablecer la contraseña de tu cuenta.</p>
            <p>Usa el siguiente código de verificación. Es válido por <strong>15 minutos</strong>.</p>
            <h1 style="letter-spacing: 8px; font-size: 48px; color: #2563EB;">{otpCode}</h1>
            <p>Si no solicitaste este cambio, ignora este correo. Tu contraseña no será modificada.</p>
            <br/>
            <p>— El equipo de Project Ledger</p>
            """;

        await SendAsync(toEmail, subject, body, ct);
    }

    // ── Password changed notification ─────────────────────────────

    public async Task SendPasswordChangedEmailAsync(
        string toEmail, string fullName, CancellationToken ct = default)
    {
        var subject = "Tu contraseña ha sido actualizada — Project Ledger";
        var body = $"""
            <h2>Hola {fullName}</h2>
            <p>Te informamos que la contraseña de tu cuenta en <strong>Project Ledger</strong>
            ha sido cambiada exitosamente.</p>
            <p>Si no realizaste este cambio, contacta al soporte de inmediato y cambia tu contraseña
            desde la opción <em>¿Olvidaste tu contraseña?</em> en la pantalla de inicio de sesión.</p>
            <br/>
            <p>— El equipo de Project Ledger</p>
            """;

        await SendAsync(toEmail, subject, body, ct);
    }

    // ── Core send method ────────────────────────────────────────

    private async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct)
    {
        if (_settings.UseFakeProvider)
        {
            _logger.LogInformation("[EMAIL-FAKE] To: {To} | Subject: {Subject}", to, subject);
            return;
        }

        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(_settings.FromEmail, _settings.FromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            message.To.Add(to);

            using var client = new SmtpClient(_settings.SmtpHost, _settings.SmtpPort)
            {
                Credentials = new NetworkCredential(_settings.SmtpUser, _settings.SmtpPassword),
                EnableSsl = true
            };

            await client.SendMailAsync(message, ct);
            _logger.LogInformation("[EMAIL] Sent to {To}: {Subject}", to, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EMAIL] Failed to send email to {To}: {Subject}", to, subject);
        }
    }
}
