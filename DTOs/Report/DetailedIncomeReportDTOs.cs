using ProjectLedger.API.DTOs.Common;

namespace ProjectLedger.API.DTOs.Report;

// ── Detailed Income Report ───────────────────────────────────

/// <summary>
/// Reporte detallado de ingresos del proyecto.
/// Basic: secciones de ingresos + totales.
/// Premium: + análisis de categorías + resumen de partners.
/// </summary>
public class DetailedIncomeReportResponse
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = null!;
    public string CurrencyCode { get; set; } = null!;
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
    public DateTime GeneratedAt { get; set; }

    public decimal TotalIncome { get; set; }
    public int TotalIncomeCount { get; set; }
    public decimal AverageIncomeAmount { get; set; }
    public decimal AverageMonthlyIncome { get; set; }
    public PeakMonthInfo? PeakMonth { get; set; }
    public LargestIncomeInfo? LargestIncome { get; set; }

    /// <summary>Totales calculados en monedas alternativas.</summary>
    public List<AlternativeCurrencyTotals>? AlternativeCurrencies { get; set; }

    /// <summary>Ingresos agrupados por mes, ordenados del más viejo al más nuevo.</summary>
    public List<MonthlyIncomeSection> Sections { get; set; } = [];

    // ── Solo con CanUseAdvancedReports ──────────────────────

    /// <summary>Análisis por categoría. null si plan básico.</summary>
    public List<CategoryIncomeAnalysisRow>? CategoryAnalysis { get; set; }

    /// <summary>Análisis por método de pago. null si plan básico.</summary>
    public List<PaymentMethodIncomeAnalysisRow>? PaymentMethodAnalysis { get; set; }
}

public class LargestIncomeInfo
{
    public Guid IncomeId { get; set; }
    public string Title { get; set; } = null!;
    public decimal Amount { get; set; }
    public DateOnly IncomeDate { get; set; }
    public string CategoryName { get; set; } = null!;
    public string PaymentMethodName { get; set; } = null!;
}

public class MonthlyIncomeSection
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthLabel { get; set; } = null!;
    public decimal SectionTotal { get; set; }
    public int SectionCount { get; set; }
    public decimal PercentageOfTotal { get; set; }
    public decimal AverageIncomeAmount { get; set; }
    public SectionTopIncome? TopIncome { get; set; }
    public List<AlternativeCurrencyTotals>? AlternativeCurrencies { get; set; }
    public List<DetailedIncomeRow> Incomes { get; set; } = [];
}

public class SectionTopIncome
{
    public string Title { get; set; } = null!;
    public decimal Amount { get; set; }
}

public class DetailedIncomeRow
{
    public Guid Id { get; set; }
    public string Title { get; set; } = null!;
    public DateOnly IncomeDate { get; set; }
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = null!;
    public Guid PaymentMethodId { get; set; }
    public string PaymentMethodName { get; set; } = null!;
    public string PaymentMethodType { get; set; } = null!;
    public decimal OriginalAmount { get; set; }
    public string OriginalCurrency { get; set; } = null!;
    public decimal ExchangeRate { get; set; }
    public decimal ConvertedAmount { get; set; }
    public decimal? AccountAmount { get; set; }
    public string? AccountCurrency { get; set; }
    public List<CurrencyExchangeResponse>? CurrencyExchanges { get; set; }
    public string? Description { get; set; }
    public string? ReceiptNumber { get; set; }
    public string? Notes { get; set; }
}

// ── Income Analysis (Advanced) ──────────────────────────────

public class CategoryIncomeAnalysisRow
{
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = null!;
    public decimal TotalAmount { get; set; }
    public int IncomeCount { get; set; }
    public decimal Percentage { get; set; }
    public decimal AverageAmount { get; set; }
}

public class PaymentMethodIncomeAnalysisRow
{
    public Guid PaymentMethodId { get; set; }
    public string PaymentMethodName { get; set; } = null!;
    public string Type { get; set; } = null!;
    public decimal TotalAmount { get; set; }
    public int IncomeCount { get; set; }
    public decimal Percentage { get; set; }
    public decimal AverageAmount { get; set; }
}

