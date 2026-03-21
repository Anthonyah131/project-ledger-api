using ProjectLedger.API.DTOs.Report;

namespace ProjectLedger.API.Services;

public interface IUserReportService
{
    Task<PaymentMethodReportResponse> GetPaymentMethodReportAsync(
        Guid userId, DateOnly? from, DateOnly? to,
        List<Guid>? paymentMethodIds, int? maxMovementsPerMethod,
        CancellationToken ct = default);
}
