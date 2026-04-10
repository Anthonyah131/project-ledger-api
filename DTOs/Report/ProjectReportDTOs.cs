namespace ProjectLedger.API.DTOs.Report;

// ── Project Summary ─────────────────────────────────────────

/// <summary>Financial summary of the project.</summary>
public class ProjectReportResponse
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = null!;
    public string CurrencyCode { get; set; } = null!;
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
    public DateTime GeneratedAt { get; set; }

    public decimal TotalSpent { get; set; }
    public int ExpenseCount { get; set; }
    public decimal AverageExpenseAmount { get; set; }

    // ── Incomes ────────────────────────────────────────────
    public decimal TotalIncome { get; set; }
    public int IncomeCount { get; set; }
    public decimal NetBalance { get; set; }

    // Optional — only when project has a budget set
    public decimal? Budget { get; set; }
    public decimal? BudgetUsedPercentage { get; set; }

    // Optional — only when plan allows advanced reports
    public decimal? ObligationSpent { get; set; }
    public decimal? RegularSpent { get; set; }
    public TopExpenseInfo? TopExpense { get; set; }

    public IEnumerable<CategoryBreakdown> ByCategory { get; set; } = [];
    public IEnumerable<PaymentMethodBreakdown> ByPaymentMethod { get; set; } = [];

    // Optional — only when partnersEnabled and advanced plan
    public IEnumerable<PartnerBreakdown>? ByPartner { get; set; }

    // Optional — only when project has alternative currencies and advanced plan
    public IEnumerable<AlternativeCurrencyTotal>? AlternativeCurrencyTotals { get; set; }
}

public class TopExpenseInfo
{
    public Guid ExpenseId { get; set; }
    public string Title { get; set; } = null!;
    public decimal Amount { get; set; }
    public string CategoryName { get; set; } = null!;
    public DateOnly ExpenseDate { get; set; }
}

/// <summary>Breakdown by category.</summary>
public class CategoryBreakdown
{
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = null!;
    public decimal TotalAmount { get; set; }
    public int ExpenseCount { get; set; }
    public decimal Percentage { get; set; }
    public decimal AverageAmount { get; set; }
}

/// <summary>Breakdown by payment method.</summary>
public class PaymentMethodBreakdown
{
    public Guid PaymentMethodId { get; set; }
    public string PaymentMethodName { get; set; } = null!;
    public decimal TotalAmount { get; set; }
    public int ExpenseCount { get; set; }
    public decimal Percentage { get; set; }
    public decimal AverageAmount { get; set; }
}

/// <summary>Breakdown by partner (splits).</summary>
public class PartnerBreakdown
{
    public Guid PartnerId { get; set; }
    public string PartnerName { get; set; } = null!;
    public decimal TotalExpenseSplits { get; set; }
    public decimal TotalIncomeSplits { get; set; }
    public decimal TotalSettlementsPaid { get; set; }
    public decimal TotalSettlementsReceived { get; set; }
    public decimal NetBalance { get; set; }
    public int ExpenseSplitCount { get; set; }
    public int IncomeSplitCount { get; set; }
    public int SettlementCount { get; set; }
}

/// <summary>Totals converted to an alternative project currency.</summary>
public class AlternativeCurrencyTotal
{
    public string CurrencyCode { get; set; } = null!;
    public decimal TotalSpent { get; set; }
    public decimal TotalIncome { get; set; }
    public decimal NetBalance { get; set; }
}
