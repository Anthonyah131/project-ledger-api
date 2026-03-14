namespace ProjectLedger.API.Common.Settings;

public class GoogleAuthSettings
{
    public const string SectionName = "Google";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string FrontendCallbackUrl { get; set; } = "http://localhost:3000/auth/callback";
}