namespace ProjectLedger.API.DTOs.Report;

// ── Project Summary ─────────────────────────────────────────

/// <summary>Resumen financiero del proyecto.</summary>
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

    // Optional — only when project has a budget set
    public decimal? Budget { get; set; }
    public decimal? BudgetUsedPercentage { get; set; }

    // Optional — only when plan allows advanced reports
    public decimal? ObligationSpent { get; set; }
    public decimal? RegularSpent { get; set; }
    public TopExpenseInfo? TopExpense { get; set; }

    public IEnumerable<CategoryBreakdown> ByCategory { get; set; } = [];
    public IEnumerable<PaymentMethodBreakdown> ByPaymentMethod { get; set; } = [];
}

public class TopExpenseInfo
{
    public Guid ExpenseId { get; set; }
    public string Title { get; set; } = null!;
    public decimal Amount { get; set; }
    public string CategoryName { get; set; } = null!;
    public DateOnly ExpenseDate { get; set; }
}

/// <summary>Desglose por categoría.</summary>
public class CategoryBreakdown
{
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = null!;
    public decimal TotalAmount { get; set; }
    public int ExpenseCount { get; set; }
    public decimal Percentage { get; set; }
    public decimal AverageAmount { get; set; }
}

/// <summary>Desglose por método de pago.</summary>
public class PaymentMethodBreakdown
{
    public Guid PaymentMethodId { get; set; }
    public string PaymentMethodName { get; set; } = null!;
    public decimal TotalAmount { get; set; }
    public int ExpenseCount { get; set; }
    public decimal Percentage { get; set; }
    public decimal AverageAmount { get; set; }
}

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
}

// ── Category Growth ─────────────────────────────────────────

/// <summary>Envelope de crecimiento por categoría entre dos meses.</summary>
public class CategoryGrowthEnvelopeResponse
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = null!;
    public string CurrencyCode { get; set; } = null!;
    public string CurrentMonthLabel { get; set; } = null!;
    public string PreviousMonthLabel { get; set; } = null!;
    public DateTime GeneratedAt { get; set; }
    public List<CategoryGrowthResponse> Categories { get; set; } = [];
}

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

    public int CurrentMonthCount { get; set; }
    public int PreviousMonthCount { get; set; }
    public decimal AverageAmountCurrent { get; set; }
    public decimal AverageAmountPrevious { get; set; }

    /// <summary>true cuando previousMonthAmount == 0 y currentMonthAmount > 0.</summary>
    public bool IsNew { get; set; }

    /// <summary>true cuando currentMonthAmount == 0 y previousMonthAmount > 0.</summary>
    public bool IsDisappeared { get; set; }
}

// ── Detailed Expense Report ─────────────────────────────────

/// <summary>
/// Reporte detallado de gastos del proyecto.
/// Basic: secciones de gastos + totales.
/// Premium: + análisis de categorías, presupuestos, obligaciones.
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
    public decimal AverageExpenseAmount { get; set; }
    public decimal AverageMonthlySpend { get; set; }
    public PeakMonthInfo? PeakMonth { get; set; }
    public LargestExpenseInfo? LargestExpense { get; set; }

    /// <summary>Gastos agrupados por mes, ordenados del más viejo al más nuevo.</summary>
    public List<MonthlyExpenseSection> Sections { get; set; } = [];

    // ── Solo con CanUseAdvancedReports ──────────────────────

    /// <summary>Análisis por categoría con presupuesto. null si plan básico.</summary>
    public List<CategoryAnalysisRow>? CategoryAnalysis { get; set; }

    /// <summary>Análisis por método de pago. null si plan básico.</summary>
    public List<PaymentMethodAnalysisRow>? PaymentMethodAnalysis { get; set; }

    /// <summary>Resumen de obligaciones/deudas. null si plan básico.</summary>
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
    public decimal PercentageOfTotal { get; set; }
    public decimal AverageExpenseAmount { get; set; }
    public SectionTopExpense? TopExpense { get; set; }
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
    public decimal? AltAmount { get; set; }
    public string? AltCurrency { get; set; }
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

// ── Payment Method Report (User-scoped) ─────────────────────

/// <summary>
/// Reporte de métodos de pago del usuario con estadísticas y desglose.
/// Requiere CanUseAdvancedReports.
/// </summary>
public class PaymentMethodReportResponse
{
    public Guid UserId { get; set; }
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
    public DateTime GeneratedAt { get; set; }

    public decimal GrandTotalSpent { get; set; }
    public int GrandTotalExpenseCount { get; set; }
    public decimal GrandAverageExpenseAmount { get; set; }
    public decimal AverageMonthlySpend { get; set; }
    public PeakMonthInfo? PeakMonth { get; set; }
    public MethodReference? MostUsedMethod { get; set; }
    public MethodReference? HighestSpendMethod { get; set; }

    public List<PaymentMethodReportRow> PaymentMethods { get; set; } = [];
    public List<PaymentMethodMonthlyRow> MonthlyTrend { get; set; } = [];
}

public class MethodReference
{
    public Guid PaymentMethodId { get; set; }
    public string Name { get; set; } = null!;
}

public class PaymentMethodReportRow
{
    public Guid PaymentMethodId { get; set; }
    public string Name { get; set; } = null!;
    public string Type { get; set; } = null!;
    public string Currency { get; set; } = null!;
    public string? BankName { get; set; }

    // Estadísticas
    public decimal TotalSpent { get; set; }
    public int ExpenseCount { get; set; }
    public decimal Percentage { get; set; }
    public decimal AverageExpenseAmount { get; set; }
    public DateOnly? FirstUseDate { get; set; }
    public DateOnly? LastUseDate { get; set; }
    public int DaysSinceLastUse { get; set; }
    public bool IsInactive { get; set; }
    public PaymentMethodTopExpense? TopExpense { get; set; }
    public List<PaymentMethodTopCategory> TopCategories { get; set; } = [];

    public List<PaymentMethodProjectBreakdown> Projects { get; set; } = [];
    public List<PaymentMethodExpenseRow> Expenses { get; set; } = [];
    public int TotalExpensesInPeriod { get; set; }
    public int ExpensesShown { get; set; }
}

public class PaymentMethodTopExpense
{
    public Guid ExpenseId { get; set; }
    public string Title { get; set; } = null!;
    public decimal Amount { get; set; }
    public DateOnly ExpenseDate { get; set; }
    public string ProjectName { get; set; } = null!;
    public string CategoryName { get; set; } = null!;
}

public class PaymentMethodTopCategory
{
    public string CategoryName { get; set; } = null!;
    public decimal TotalAmount { get; set; }
    public int ExpenseCount { get; set; }
    public decimal Percentage { get; set; }
}

public class PaymentMethodProjectBreakdown
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = null!;
    public string ProjectCurrency { get; set; } = null!;
    public decimal TotalSpent { get; set; }
    public int ExpenseCount { get; set; }
    public decimal Percentage { get; set; }
}

public class PaymentMethodExpenseRow
{
    public Guid ExpenseId { get; set; }
    public string Title { get; set; } = null!;
    public DateOnly ExpenseDate { get; set; }
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = null!;
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = null!;
    public decimal OriginalAmount { get; set; }
    public string OriginalCurrency { get; set; } = null!;
    public decimal ConvertedAmount { get; set; }
    public string ProjectCurrency { get; set; } = null!;
    public string? Description { get; set; }
}

public class PaymentMethodMonthlyRow
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthLabel { get; set; } = null!;
    public decimal TotalSpent { get; set; }
    public int ExpenseCount { get; set; }
    public List<PaymentMethodMonthBreakdown> ByMethod { get; set; } = [];
}

public class PaymentMethodMonthBreakdown
{
    public Guid PaymentMethodId { get; set; }
    public string Name { get; set; } = null!;
    public decimal TotalSpent { get; set; }
    public int ExpenseCount { get; set; }
    public decimal Percentage { get; set; }
}
