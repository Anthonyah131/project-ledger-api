namespace ProjectLedger.API.DTOs.Report;

// ── Month Comparison ────────────────────────────────────────

/// <summary>Comparación de gasto mes actual vs mes anterior.</summary>
public class MonthComparisonResponse
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = null!;
    public string CurrencyCode { get; set; } = null!;
    public DateTime GeneratedAt { get; set; }
    public MonthSummary CurrentMonth { get; set; } = null!;
    public MonthSummary PreviousMonth { get; set; } = null!;

    /// <summary>Positivo = aumento, negativo = disminución.</summary>
    public decimal ChangeAmount { get; set; }

    /// <summary>Cambio porcentual vs mes anterior. null si previous = 0.</summary>
    public decimal? ChangePercentage { get; set; }

    /// <summary>false cuando previousMonth no tiene gastos registrados.</summary>
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

    /// <summary>Totales en monedas alternativas del proyecto. null si no hay monedas alternativas.</summary>
    public List<AlternativeCurrencyTotal>? AlternativeCurrencyTotals { get; set; }
}
