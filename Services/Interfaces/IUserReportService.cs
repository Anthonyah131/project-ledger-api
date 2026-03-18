using ProjectLedger.API.DTOs.Report;

namespace ProjectLedger.API.Services;

public interface IUserReportService
{
    Task<PaymentMethodReportResponse> GetPaymentMethodReportAsync(
        Guid userId, DateOnly? from, DateOnly? to, Guid? paymentMethodId, CancellationToken ct = default);
}
