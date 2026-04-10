namespace ProjectLedger.API.Services;

/// <summary>
/// Service for sending transactional emails.
/// </summary>
public interface IEmailService
{
    /// <summary>Sends a welcome email to the new user.</summary>
    Task SendWelcomeEmailAsync(string toEmail, string fullName, CancellationToken ct = default);

    /// <summary>Notifies the administrator that a new user has registered.</summary>
    Task SendNewUserNotificationToAdminAsync(string userEmail, string fullName, CancellationToken ct = default);

    /// <summary>Notifies the user that their account has been activated.</summary>
    Task SendAccountActivatedEmailAsync(string toEmail, string fullName, CancellationToken ct = default);

    /// <summary>Notifies the user that their account has been deactivated.</summary>
    Task SendAccountDeactivatedEmailAsync(string toEmail, string fullName, CancellationToken ct = default);

    /// <summary>Notifies the user that they have been added to a project.</summary>
    Task SendProjectSharedEmailAsync(string toEmail, string fullName, string projectName, string role, string sharedByName, CancellationToken ct = default);

    /// <summary>Notifies the user that their access to a project has been revoked.</summary>
    Task SendProjectAccessRevokedEmailAsync(string toEmail, string fullName, string projectName, string revokedByName, CancellationToken ct = default);

    /// <summary>Sends the password reset OTP code to the user.</summary>
    Task SendPasswordResetEmailAsync(string toEmail, string fullName, string otpCode, CancellationToken ct = default);

    /// <summary>Notifies the user that their password was successfully changed.</summary>
    Task SendPasswordChangedEmailAsync(string toEmail, string fullName, CancellationToken ct = default);
}
