namespace ProjectLedger.API.Common.Settings;

public class AzureDocumentIntelligenceSettings
{
    public const string SectionName = "AzureDocumentIntelligence";

    public bool Enabled { get; set; } = false;
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string DefaultModelId { get; set; } = "prebuilt-receipt";
    public int TimeoutSeconds { get; set; } = 30;
    public int PollingIntervalMilliseconds { get; set; } = 1000;
    public int MaxPollingAttempts { get; set; } = 30;
    public int MaxFileSizeMb { get; set; } = 10;
}
