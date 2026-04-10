using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace ProjectLedger.API.Services;

/// <summary>
/// Implementation of IEmailService with dual support:
/// - UseFakeProvider = true  → log to console only (development)
/// - UseFakeProvider = false → send via SMTP (production)
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
        var subject = "Welcome to Project Ledger!";
        var body = $"""
            <h2>Hello {fullName}!</h2>
            <p>Your account at <strong>Project Ledger</strong> has been successfully created.</p>
            <p>Your account is pending activation by an administrator.
            In the meantime, you can log in and explore the platform in read-only mode.</p>
            <p>You will receive an email once your account is activated.</p>
            <br/>
            <p>— The Project Ledger Team</p>
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

        var subject = $"New user registered: {fullName}";
        var body = $"""
            <h2>New registration in Project Ledger</h2>
            <p><strong>Name:</strong> {fullName}</p>
            <p><strong>Email:</strong> {userEmail}</p>
            <p>The user has been created in a <strong>deactivated</strong> state.
            Access the admin panel to review and activate them.</p>
            """;

        await SendAsync(_settings.AdminEmail, subject, body, ct);
    }

    // ── Account activated ───────────────────────────────────

    public async Task SendAccountActivatedEmailAsync(string toEmail, string fullName, CancellationToken ct = default)
    {
        var subject = "Your account has been activated — Project Ledger";
        var body = $"""
            <h2>Great news, {fullName}!</h2>
            <p>Your account at <strong>Project Ledger</strong> has been <strong>activated</strong> by an administrator.</p>
            <p>Now you can create projects, register expenses, and use all the features of your plan.</p>
            <br/>
            <p>— The Project Ledger Team</p>
            """;

        await SendAsync(toEmail, subject, body, ct);
    }

    // ── Account deactivated ─────────────────────────────────

    public async Task SendAccountDeactivatedEmailAsync(string toEmail, string fullName, CancellationToken ct = default)
    {
        var subject = "Your account has been deactivated — Project Ledger";
        var body = $"""
            <h2>Hello {fullName}</h2>
            <p>Your account at <strong>Project Ledger</strong> has been <strong>deactivated</strong> by an administrator.</p>
            <p>You can still log in and view your data, but you won't be able to create or modify information
            until your account is reactivated.</p>
            <p>If you believe this is an error, please contact the administrator.</p>
            <br/>
            <p>— The Project Ledger Team</p>
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
            "viewer" => "Viewer",
            _ => role
        };

        var subject = $"A project has been shared with you — Project Ledger";
        var body = $"""
            <h2>Hello {fullName}!</h2>
            <p><strong>{sharedByName}</strong> has added you to the project
            <strong>{projectName}</strong> with the role of <strong>{roleName}</strong>.</p>
            <p>Log in to Project Ledger to view the project.</p>
            <br/>
            <p>— The Project Ledger Team</p>
            """;

        await SendAsync(toEmail, subject, body, ct);
    }

    // ── Project access revoked ──────────────────────────────

    public async Task SendProjectAccessRevokedEmailAsync(
        string toEmail, string fullName, string projectName,
        string revokedByName, CancellationToken ct = default)
    {
        var subject = $"Your access to a project has been revoked — Project Ledger";
        var body = $"""
            <h2>Hello {fullName}</h2>
            <p><strong>{revokedByName}</strong> has revoked your access to the project
            <strong>{projectName}</strong>.</p>
            <p>If you believe this is an error, please contact the project owner.</p>
            <br/>
            <p>— The Project Ledger Team</p>
            """;

        await SendAsync(toEmail, subject, body, ct);
    }

    // ── Password reset OTP ────────────────────────────────────

    public async Task SendPasswordResetEmailAsync(
        string toEmail, string fullName, string otpCode, CancellationToken ct = default)
    {
        var subject = "Code to reset your password — Project Ledger";
        var body = $"""
            <h2>Hello {fullName}</h2>
            <p>We received a request to reset the password for your account.</p>
            <p>Use the following verification code. It is valid for <strong>15 minutes</strong>.</p>
            <h1 style="letter-spacing: 8px; font-size: 48px; color: #2563EB;">{otpCode}</h1>
            <p>If you did not request this change, please ignore this email. Your password will not be modified.</p>
            <br/>
            <p>— The Project Ledger Team</p>
            """;

        await SendAsync(toEmail, subject, body, ct);
    }

    // ── Password changed notification ─────────────────────────────

    public async Task SendPasswordChangedEmailAsync(
        string toEmail, string fullName, CancellationToken ct = default)
    {
        var subject = "Your password has been updated — Project Ledger";
        var body = $"""
            <h2>Hello {fullName}</h2>
            <p>We inform you that the password for your account at <strong>Project Ledger</strong>
            has been successfully changed.</p>
            <p>If you did not make this change, please contact support immediately and change your password
            using the <em>Forgot your password?</em> option on the login screen.</p>
            <br/>
            <p>— The Project Ledger Team</p>
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
