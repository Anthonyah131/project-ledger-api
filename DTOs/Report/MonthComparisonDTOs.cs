namespace ProjectLedger.API.DTOs.Report;

// ── Month Comparison ────────────────────────────────────────

/// <summary>Expense comparison current month vs previous month.</summary>
public class MonthComparisonResponse
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = null!;
    public string CurrencyCode { get; set; } = null!;
    public DateTime GeneratedAt { get; set; }
    public MonthSummary CurrentMonth { get; set; } = null!;
    public MonthSummary PreviousMonth { get; set; } = null!;

    /// <summary>Positive = increase, negative = decrease.</summary>
    public decimal ChangeAmount { get; set; }

    /// <summary>Percentage change vs previous month. null if previous = 0.</summary>
    public decimal? ChangePercentage { get; set; }

    /// <summary>false when previousMonth has no registered expenses.</summary>
    public bool HasPreviousData { get; set; }
}

public class MonthSummary
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthLabel { get; set; } = null!;
    public decimal TotalSpent { get; set; }
    public int ExpenseCount { get; set; }
    public decimal TotalIncome { get; set; }
    public int IncomeCount { get; set; }
    public decimal NetBalance { get; set; }

    /// <summary>Alternative currency totals of the project. null if no alternative currencies.</summary>
    public List<AlternativeCurrencyTotal>? AlternativeCurrencyTotals { get; set; }
}
