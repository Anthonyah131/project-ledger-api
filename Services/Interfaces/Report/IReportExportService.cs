using ProjectLedger.API.DTOs.Report;

namespace ProjectLedger.API.Services;

/// <summary>
/// Contract for generating reports in different formats (Excel, PDF).
/// </summary>
public interface IReportExportService
{
    // ── Excel ────────────────────────────────────────────────────────────────

    /// <summary>Generates the detailed expense report in Excel (.xlsx) format.</summary>
    byte[] GenerateExpenseReportExcel(DetailedExpenseReportResponse report);

    /// <summary>Generates the payment method report in Excel (.xlsx) format.</summary>
    byte[] GeneratePaymentMethodReportExcel(PaymentMethodReportResponse report);

    /// <summary>Generates the detailed income report in Excel (.xlsx) format.</summary>
    byte[] GenerateIncomeReportExcel(DetailedIncomeReportResponse report);

    /// <summary>Generates the partner balance report in Excel (.xlsx) format.</summary>
    byte[] GeneratePartnerBalanceReportExcel(PartnerBalanceReportResponse report);

    /// <summary>Generates the workspace report in Excel (.xlsx) format.</summary>
    byte[] GenerateWorkspaceReportExcel(WorkspaceReportResponse report);

    /// <summary>Generates the partner general report in Excel (.xlsx) format.</summary>
    byte[] GeneratePartnerGeneralReportExcel(PartnerGeneralReportResponse report);

    // ── PDF ──────────────────────────────────────────────────────────────────

    /// <summary>Generates the detailed expense report in PDF format.</summary>
    byte[] GenerateExpenseReportPdf(DetailedExpenseReportResponse report);

    /// <summary>Generates the payment method report in PDF format.</summary>
    byte[] GeneratePaymentMethodReportPdf(PaymentMethodReportResponse report);

    /// <summary>Generates the detailed income report in PDF format.</summary>
    byte[] GenerateIncomeReportPdf(DetailedIncomeReportResponse report);

    /// <summary>Generates the partner balance report in PDF format.</summary>
    byte[] GeneratePartnerBalanceReportPdf(PartnerBalanceReportResponse report);

    /// <summary>Generates the workspace report in PDF format.</summary>
    byte[] GenerateWorkspaceReportPdf(WorkspaceReportResponse report);
}
