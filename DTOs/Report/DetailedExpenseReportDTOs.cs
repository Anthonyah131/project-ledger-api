using ProjectLedger.API.DTOs.Common;

namespace ProjectLedger.API.DTOs.Report;

// ── Detailed Expense Report ─────────────────────────────────

/// <summary>
/// Detailed project expense report.
/// Basic: expense sections + totals.
/// Premium: + category analysis, budgets, obligations.
/// </summary>
public class DetailedExpenseReportResponse
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = null!;
    public string CurrencyCode { get; set; } = null!;
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
    public DateTime GeneratedAt { get; set; }

    public decimal TotalSpent { get; set; }
    public int TotalExpenseCount { get; set; }
    public decimal TotalIncome { get; set; }
    public int TotalIncomeCount { get; set; }
    public decimal NetBalance { get; set; }
    public decimal AverageExpenseAmount { get; set; }
    public decimal AverageMonthlySpend { get; set; }
    public PeakMonthInfo? PeakMonth { get; set; }
    public LargestExpenseInfo? LargestExpense { get; set; }

    /// <summary>Totals calculated in alternative currencies.</summary>
    public List<AlternativeCurrencyTotals>? AlternativeCurrencies { get; set; }

    /// <summary>Expenses grouped by month, ordered from oldest to newest.</summary>
    public List<MonthlyExpenseSection> Sections { get; set; } = [];

    // ── Only with CanUseAdvancedReports ──────────────────────

    /// <summary>Analysis by category with budget. null if basic plan.</summary>
    public List<CategoryAnalysisRow>? CategoryAnalysis { get; set; }

    /// <summary>Analysis by payment method. null if basic plan.</summary>
    public List<PaymentMethodAnalysisRow>? PaymentMethodAnalysis { get; set; }

    /// <summary>Summary of obligations/debts. null if basic plan.</summary>
    public ObligationSummarySection? ObligationSummary { get; set; }
}

public class PeakMonthInfo
{
    public string MonthLabel { get; set; } = null!;
    public decimal Total { get; set; }
}

public class LargestExpenseInfo
{
    public Guid ExpenseId { get; set; }
    public string Title { get; set; } = null!;
    public decimal Amount { get; set; }
    public DateOnly ExpenseDate { get; set; }
    public string CategoryName { get; set; } = null!;
    public string PaymentMethodName { get; set; } = null!;
}

public class MonthlyExpenseSection
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthLabel { get; set; } = null!;
    public decimal SectionTotal { get; set; }
    public int SectionCount { get; set; }
    public decimal SectionIncomeTotal { get; set; }
    public int SectionIncomeCount { get; set; }
    public decimal SectionNetBalance { get; set; }
    public decimal PercentageOfTotal { get; set; }
    public decimal AverageExpenseAmount { get; set; }
    public SectionTopExpense? TopExpense { get; set; }
    public List<AlternativeCurrencyTotals>? AlternativeCurrencies { get; set; }
    public List<DetailedExpenseRow> Expenses { get; set; } = [];
}

public class SectionTopExpense
{
    public string Title { get; set; } = null!;
    public decimal Amount { get; set; }
}

public class DetailedExpenseRow
{
    public Guid Id { get; set; }
    public string Title { get; set; } = null!;
    public DateOnly ExpenseDate { get; set; }
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = null!;
    public Guid PaymentMethodId { get; set; }
    public string PaymentMethodName { get; set; } = null!;
    public string PaymentMethodType { get; set; } = null!;
    public decimal OriginalAmount { get; set; }
    public string OriginalCurrency { get; set; } = null!;
    public decimal ExchangeRate { get; set; }
    public decimal ConvertedAmount { get; set; }
    public List<CurrencyExchangeResponse>? CurrencyExchanges { get; set; }
    public decimal? AccountAmount { get; set; }
    public string? AccountCurrency { get; set; }
    public string? Description { get; set; }
    public string? ReceiptNumber { get; set; }
    public string? Notes { get; set; }
    public bool IsObligationPayment { get; set; }
    public Guid? ObligationId { get; set; }
    public string? ObligationTitle { get; set; }
}

// ── Payment Method Analysis (Advanced) ─────────────────────

public class PaymentMethodAnalysisRow
{
    public Guid PaymentMethodId { get; set; }
    public string PaymentMethodName { get; set; } = null!;
    public string Type { get; set; } = null!;
    public decimal SpentAmount { get; set; }
    public int ExpenseCount { get; set; }
    public decimal Percentage { get; set; }
    public decimal AverageExpenseAmount { get; set; }
}

// ── Category Analysis (Advanced) ────────────────────────────

public class CategoryAnalysisRow
{
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = null!;
    public bool IsDefault { get; set; }
    public decimal? BudgetAmount { get; set; }
    public decimal SpentAmount { get; set; }
    public int ExpenseCount { get; set; }
    public decimal Percentage { get; set; }
    public decimal? BudgetRemaining { get; set; }
    public decimal? BudgetUsedPercentage { get; set; }
    public bool? BudgetExceeded { get; set; }
}

// ── Obligation Summary (Advanced) ───────────────────────────

public class ObligationSummarySection
{
    public int TotalObligations { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal TotalPending { get; set; }
    public int OverdueCount { get; set; }
    public decimal OverdueAmount { get; set; }
    public List<ObligationStatusGroup> ByStatus { get; set; } = [];
}

public class ObligationStatusGroup
{
    public string Status { get; set; } = null!;
    public int Count { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal TotalPaid { get; set; }
    public List<ObligationReportRow> Obligations { get; set; } = [];
}

public class ObligationReportRow
{
    public Guid OblId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public string Currency { get; set; } = null!;
    public DateOnly? DueDate { get; set; }
    public string Status { get; set; } = null!;
    public int PaymentCount { get; set; }
    public DateOnly? LastPaymentDate { get; set; }
}

// ── Alternative Currency Totals ──────────────────────────────

public class AlternativeCurrencyTotals
{
    public string CurrencyCode { get; set; } = null!;
    public decimal TotalSpent { get; set; }
    public decimal TotalIncome { get; set; }
    public decimal NetBalance { get; set; }
    public decimal AverageExpenseAmount { get; set; }
    public decimal AverageMonthlySpend { get; set; }
}
