namespace ProjectLedger.API.Common.Settings;

/// <summary>
/// JWT configuration read from appsettings.json → "Jwt" section.
/// The SecretKey is resolved from the JWT_SECRET_KEY environment variable.
/// </summary>
public class JwtSettings
{
    public const string SectionName = "Jwt";

    public string SecretKey { get; set; } = null!;
    public string Issuer { get; set; } = null!;
    public string Audience { get; set; } = null!;
    public int AccessTokenExpirationMinutes { get; set; } = 1440;
    public int RefreshTokenExpirationDays { get; set; } = 7;
}
