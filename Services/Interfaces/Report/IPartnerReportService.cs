using ProjectLedger.API.DTOs.Report;

namespace ProjectLedger.API.Services;

public interface IPartnerReportService
{
    /// <summary>
    /// Generates a comprehensive summary of a partner's activity across all shared projects.
    /// </summary>
    Task<PartnerGeneralReportResponse> GetGeneralReportAsync(
        Guid partnerId, DateOnly? from, DateOnly? to, CancellationToken ct = default);
}
