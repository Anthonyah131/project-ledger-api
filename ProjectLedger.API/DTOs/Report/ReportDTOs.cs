namespace ProjectLedger.API.DTOs.Report;

// ── Project Summary ─────────────────────────────────────────

/// <summary>Resumen financiero del proyecto.</summary>
public class ProjectReportResponse
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = null!;
    public string CurrencyCode { get; set; } = null!;

    public decimal TotalSpent { get; set; }
    public int ExpenseCount { get; set; }

    public IEnumerable<CategoryBreakdown> ByCategory { get; set; } = [];
    public IEnumerable<PaymentMethodBreakdown> ByPaymentMethod { get; set; } = [];
}

/// <summary>Desglose por categoría.</summary>
public class CategoryBreakdown
{
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = null!;
    public decimal TotalAmount { get; set; }
    public int ExpenseCount { get; set; }
    public decimal Percentage { get; set; }
}

/// <summary>Desglose por método de pago.</summary>
public class PaymentMethodBreakdown
{
    public Guid PaymentMethodId { get; set; }
    public string PaymentMethodName { get; set; } = null!;
    public decimal TotalAmount { get; set; }
    public int ExpenseCount { get; set; }
    public decimal Percentage { get; set; }
}

// ── Month Comparison ────────────────────────────────────────

/// <summary>Comparación de gasto mes actual vs mes anterior.</summary>
public class MonthComparisonResponse
{
    public Guid ProjectId { get; set; }
    public MonthSummary CurrentMonth { get; set; } = null!;
    public MonthSummary PreviousMonth { get; set; } = null!;

    /// <summary>Positivo = aumento, negativo = disminución.</summary>
    public decimal ChangeAmount { get; set; }

    /// <summary>Cambio porcentual vs mes anterior. null si previous = 0.</summary>
    public decimal? ChangePercentage { get; set; }
}

public class MonthSummary
{
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal TotalSpent { get; set; }
    public int ExpenseCount { get; set; }
}

// ── Category Growth ─────────────────────────────────────────

/// <summary>Crecimiento por categoría entre dos meses.</summary>
public class CategoryGrowthResponse
{
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = null!;
    public decimal CurrentMonthAmount { get; set; }
    public decimal PreviousMonthAmount { get; set; }
    public decimal GrowthAmount { get; set; }

    /// <summary>Porcentaje de crecimiento. null si previous = 0.</summary>
    public decimal? GrowthPercentage { get; set; }
}
