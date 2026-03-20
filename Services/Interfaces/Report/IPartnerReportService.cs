using ProjectLedger.API.DTOs.Report;

namespace ProjectLedger.API.Services;

public interface IPartnerReportService
{
    Task<PartnerGeneralReportResponse> GetGeneralReportAsync(
        Guid partnerId, DateOnly? from, DateOnly? to, CancellationToken ct = default);
}
