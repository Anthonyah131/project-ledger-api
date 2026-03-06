using ProjectLedger.API.DTOs.Report;

namespace ProjectLedger.API.Services;

/// <summary>
/// Servicio de exportación de reportes a Excel y PDF.
/// </summary>
public interface IReportExportService
{
    byte[] GenerateExpenseReportExcel(DetailedExpenseReportResponse report);
    byte[] GenerateExpenseReportPdf(DetailedExpenseReportResponse report);
    byte[] GeneratePaymentMethodReportExcel(PaymentMethodReportResponse report);
    byte[] GeneratePaymentMethodReportPdf(PaymentMethodReportResponse report);
}
