using Microsoft.Extensions.Localization;
using ProjectLedger.API.Resources;

namespace ProjectLedger.API.Services.Report;

/// <summary>
/// Constructor and shared dependencies for the report export service (partial class root).
/// </summary>
public partial class ReportExportService : IReportExportService
{
    private readonly IStringLocalizer<Messages> _localizer;

    public ReportExportService(IStringLocalizer<Messages> localizer)
    {
        _localizer = localizer;
    }
}
