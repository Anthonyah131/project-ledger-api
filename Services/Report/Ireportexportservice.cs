using ProjectLedger.API.DTOs.Report;

namespace ProjectLedger.API.Services.Report;

/// <summary>
/// Contrato para la generación de reportes en distintos formatos (Excel, PDF).
/// </summary>
public interface IReportExportService
{
    // ── Excel ────────────────────────────────────────────────────────────────

    /// <summary>Genera el reporte detallado de gastos en formato Excel (.xlsx).</summary>
    byte[] GenerateExpenseReportExcel(DetailedExpenseReportResponse report);

    /// <summary>Genera el reporte de métodos de pago en formato Excel (.xlsx).</summary>
    byte[] GeneratePaymentMethodReportExcel(PaymentMethodReportResponse report);

    // ── PDF ──────────────────────────────────────────────────────────────────

    /// <summary>Genera el reporte detallado de gastos en formato PDF.</summary>
    byte[] GenerateExpenseReportPdf(DetailedExpenseReportResponse report);

    /// <summary>Genera el reporte de métodos de pago en formato PDF.</summary>
    byte[] GeneratePaymentMethodReportPdf(PaymentMethodReportResponse report);
}