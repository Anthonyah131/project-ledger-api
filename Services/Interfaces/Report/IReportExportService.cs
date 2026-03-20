using ProjectLedger.API.DTOs.Report;

namespace ProjectLedger.API.Services;

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

    /// <summary>Genera el reporte detallado de ingresos en formato Excel (.xlsx).</summary>
    byte[] GenerateIncomeReportExcel(DetailedIncomeReportResponse report);

    /// <summary>Genera el reporte de balances de partners en formato Excel (.xlsx).</summary>
    byte[] GeneratePartnerBalanceReportExcel(PartnerBalanceReportResponse report);

    /// <summary>Genera el reporte de workspace en formato Excel (.xlsx).</summary>
    byte[] GenerateWorkspaceReportExcel(WorkspaceReportResponse report);

    /// <summary>Genera el reporte general del partner en formato Excel (.xlsx).</summary>
    byte[] GeneratePartnerGeneralReportExcel(PartnerGeneralReportResponse report);

    // ── PDF ──────────────────────────────────────────────────────────────────

    /// <summary>Genera el reporte detallado de gastos en formato PDF.</summary>
    byte[] GenerateExpenseReportPdf(DetailedExpenseReportResponse report);

    /// <summary>Genera el reporte de métodos de pago en formato PDF.</summary>
    byte[] GeneratePaymentMethodReportPdf(PaymentMethodReportResponse report);

    /// <summary>Genera el reporte detallado de ingresos en formato PDF.</summary>
    byte[] GenerateIncomeReportPdf(DetailedIncomeReportResponse report);

    /// <summary>Genera el reporte de balances de partners en formato PDF.</summary>
    byte[] GeneratePartnerBalanceReportPdf(PartnerBalanceReportResponse report);

    /// <summary>Genera el reporte de workspace en formato PDF.</summary>
    byte[] GenerateWorkspaceReportPdf(WorkspaceReportResponse report);
}
