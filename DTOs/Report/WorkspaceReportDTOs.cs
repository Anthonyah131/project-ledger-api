namespace ProjectLedger.API.DTOs.Report;

// ── Workspace Report ─────────────────────────────────────────

/// <summary>
/// Reporte agregado a nivel de workspace que consolida datos de múltiples proyectos.
/// Los totales consolidados están en la moneda de referencia indicada.
/// Requiere CanUseAdvancedReports.
/// </summary>
public class WorkspaceReportResponse
{
    public Guid WorkspaceId { get; set; }
    public string WorkspaceName { get; set; } = null!;
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
    public DateTime GeneratedAt { get; set; }

    /// <summary>Moneda de referencia para totales consolidados. null si no se especificó.</summary>
    public string? ReferenceCurrency { get; set; }

    /// <summary>Totales consolidados (solo si se especificó moneda de referencia).</summary>
    public WorkspaceConsolidatedTotals? ConsolidatedTotals { get; set; }

    public int ProjectCount { get; set; }
    public List<WorkspaceProjectSummary> Projects { get; set; } = [];
    public List<WorkspaceCategoryBreakdown> ConsolidatedByCategory { get; set; } = [];
    public List<WorkspaceMonthlyRow> MonthlyTrend { get; set; } = [];
}

public class WorkspaceConsolidatedTotals
{
    public decimal TotalSpent { get; set; }
    public decimal TotalIncome { get; set; }
    public decimal NetBalance { get; set; }
    public int TotalExpenseCount { get; set; }
    public int TotalIncomeCount { get; set; }
}

public class WorkspaceProjectSummary
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = null!;
    public string CurrencyCode { get; set; } = null!;
    public decimal TotalSpent { get; set; }
    public decimal TotalIncome { get; set; }
    public decimal NetBalance { get; set; }
    public int ExpenseCount { get; set; }
    public int IncomeCount { get; set; }

    /// <summary>Porcentaje del total del workspace (solo con moneda de referencia).</summary>
    public decimal? Percentage { get; set; }
}

/// <summary>Categorías agrupadas por nombre (cross-project).</summary>
public class WorkspaceCategoryBreakdown
{
    public string CategoryName { get; set; } = null!;
    public decimal TotalAmount { get; set; }
    public decimal Percentage { get; set; }
    public int ProjectCount { get; set; }
    public int ExpenseCount { get; set; }
}

public class WorkspaceMonthlyRow
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthLabel { get; set; } = null!;
    public decimal TotalSpent { get; set; }
    public decimal TotalIncome { get; set; }
    public decimal NetBalance { get; set; }
    public int ExpenseCount { get; set; }
    public int IncomeCount { get; set; }
    public List<WorkspaceProjectMonthBreakdown> ByProject { get; set; } = [];
}

public class WorkspaceProjectMonthBreakdown
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = null!;
    public string CurrencyCode { get; set; } = null!;
    public decimal TotalSpent { get; set; }
    public decimal TotalIncome { get; set; }
    public decimal NetBalance { get; set; }
}
