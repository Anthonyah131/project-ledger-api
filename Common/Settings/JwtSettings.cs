namespace ProjectLedger.API.Common.Settings;

/// <summary>
/// JWT configuration read from appsettings.json → "Jwt" section.
/// The SecretKey is resolved from the JWT_SECRET_KEY environment variable.
/// </summary>
public class JwtSettings
{
    public const string SectionName = "Jwt";

    /// <summary>Secret key used to sign the tokens. Must be at least 32 characters long.</summary>
    public string SecretKey { get; set; } = null!;

    /// <summary>The designated issuer of the tokens.</summary>
    public string Issuer { get; set; } = null!;

    /// <summary>The intended audience for the tokens.</summary>
    public string Audience { get; set; } = null!;

    /// <summary>Duration in minutes before an access token expires.</summary>
    public int AccessTokenExpirationMinutes { get; set; } = 1440;

    /// <summary>Duration in days before a refresh token expires.</summary>
    public int RefreshTokenExpirationDays { get; set; } = 7;
}
