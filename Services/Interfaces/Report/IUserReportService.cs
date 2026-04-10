using ProjectLedger.API.DTOs.Report;

namespace ProjectLedger.API.Services;

public interface IUserReportService
{
    /// <summary>
    /// Generates a consolidated report of a user's payment method activity, 
    /// tracking usage and limits across all projects.
    /// </summary>
    Task<PaymentMethodReportResponse> GetPaymentMethodReportAsync(
        Guid userId, DateOnly? from, DateOnly? to,
        List<Guid>? paymentMethodIds, int? maxMovementsPerMethod,
        CancellationToken ct = default);
}
