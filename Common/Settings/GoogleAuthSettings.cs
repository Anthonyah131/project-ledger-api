namespace ProjectLedger.API.Common.Settings;

/// <summary>
/// Configuration for Google OAuth2 authentication.
/// </summary>
public class GoogleAuthSettings
{
    public const string SectionName = "Google";

    /// <summary>Client ID provided by Google Cloud Console.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Client Secret provided by Google Cloud Console.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>The URL where the user is redirected after a successful Google login.</summary>
    public string FrontendCallbackUrl { get; set; } = "http://localhost:3000/auth/callback";
}