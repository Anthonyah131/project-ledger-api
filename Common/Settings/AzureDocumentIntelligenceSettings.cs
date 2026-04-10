namespace ProjectLedger.API.Common.Settings;

/// <summary>
/// Configuration for Azure AI Document Intelligence (OCR) service.
/// </summary>
public class AzureDocumentIntelligenceSettings
{
    public const string SectionName = "AzureDocumentIntelligence";

    /// <summary>Whether the OCR service is enabled.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>Endpoint URL from your Azure resource.</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>API key for the Azure resource.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Model ID to use for analysis (e.g., 'prebuilt-receipt').</summary>
    public string DefaultModelId { get; set; } = "prebuilt-receipt";

    /// <summary>Timeout in seconds for analysis requests.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Interval between polling attempts for long-running operations.</summary>
    public int PollingIntervalMilliseconds { get; set; } = 1000;

    /// <summary>Maximum number of polling attempts before timing out.</summary>
    public int MaxPollingAttempts { get; set; } = 30;

    /// <summary>Maximum file size allowed for processing in MB.</summary>
    public int MaxFileSizeMb { get; set; } = 10;
}
