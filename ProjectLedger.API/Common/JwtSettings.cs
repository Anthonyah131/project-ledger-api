namespace ProjectLedger.API.Common;

/// <summary>
/// Configuración JWT leída desde appsettings.json → sección "Jwt".
/// La SecretKey se resuelve desde la variable de entorno JWT_SECRET_KEY.
/// </summary>
public class JwtSettings
{
    public const string SectionName = "Jwt";

    public string SecretKey { get; set; } = null!;
    public string Issuer { get; set; } = null!;
    public string Audience { get; set; } = null!;
    public int AccessTokenExpirationMinutes { get; set; } = 15;
    public int RefreshTokenExpirationDays { get; set; } = 7;
}
