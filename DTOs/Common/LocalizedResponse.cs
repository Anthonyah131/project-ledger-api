namespace ProjectLedger.API.DTOs.Common;

/// <summary>
/// Standardized API response with localized message.
/// </summary>
public class LocalizedResponse
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    public static LocalizedResponse Create(string code, string message)
        => new() { Code = code, Message = message };
}
