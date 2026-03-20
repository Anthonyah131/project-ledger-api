using ProjectLedger.API.DTOs.Common;

namespace ProjectLedger.API.DTOs.Report;

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
    public decimal GrandTotalIncome { get; set; }
    public int GrandTotalIncomeCount { get; set; }
    public decimal GrandNetFlow { get; set; }
    public decimal GrandAverageExpenseAmount { get; set; }
    public decimal GrandAverageIncomeAmount { get; set; }
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
    public string? OwnerPartnerName { get; set; }

    // Estadísticas
    public decimal TotalSpent { get; set; }
    public int ExpenseCount { get; set; }
    public decimal TotalIncome { get; set; }
    public int IncomeCount { get; set; }
    public decimal NetFlow { get; set; }
    public decimal Percentage { get; set; }
    public decimal AverageExpenseAmount { get; set; }
    public decimal AverageIncomeAmount { get; set; }
    public DateOnly? FirstUseDate { get; set; }
    public DateOnly? LastUseDate { get; set; }
    public int DaysSinceLastUse { get; set; }
    public bool IsInactive { get; set; }
    public PaymentMethodTopExpense? TopExpense { get; set; }
    public List<PaymentMethodTopCategory> TopCategories { get; set; } = [];

    public List<PaymentMethodProjectBreakdown> Projects { get; set; } = [];
    public List<PaymentMethodExpenseRow> Expenses { get; set; } = [];
    public List<PaymentMethodIncomeRow> Incomes { get; set; } = [];
    public int TotalExpensesInPeriod { get; set; }
    public int ExpensesShown { get; set; }
    public int TotalIncomesInPeriod { get; set; }
    public int IncomesShown { get; set; }
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
    public decimal? AccountAmount { get; set; }
    public string? AccountCurrency { get; set; }
    public decimal ConvertedAmount { get; set; }
    public string ProjectCurrency { get; set; } = null!;
    public List<CurrencyExchangeResponse>? CurrencyExchanges { get; set; }
    public string? Description { get; set; }
}

public class PaymentMethodIncomeRow
{
    public Guid IncomeId { get; set; }
    public string Title { get; set; } = null!;
    public DateOnly IncomeDate { get; set; }
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = null!;
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = null!;
    public decimal OriginalAmount { get; set; }
    public string OriginalCurrency { get; set; } = null!;
    public decimal? AccountAmount { get; set; }
    public string? AccountCurrency { get; set; }
    public decimal ConvertedAmount { get; set; }
    public string ProjectCurrency { get; set; } = null!;
    public List<CurrencyExchangeResponse>? CurrencyExchanges { get; set; }
    public string? Description { get; set; }
}

public class PaymentMethodMonthlyRow
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthLabel { get; set; } = null!;
    public decimal TotalSpent { get; set; }
    public int ExpenseCount { get; set; }
    public decimal TotalIncome { get; set; }
    public int IncomeCount { get; set; }
    public decimal NetBalance { get; set; }
    public List<PaymentMethodMonthBreakdown> ByMethod { get; set; } = [];
}

public class PaymentMethodMonthBreakdown
{
    public Guid PaymentMethodId { get; set; }
    public string Name { get; set; } = null!;
    public decimal TotalSpent { get; set; }
    public int ExpenseCount { get; set; }
    public decimal TotalIncome { get; set; }
    public int IncomeCount { get; set; }
    public decimal NetFlow { get; set; }
    public decimal Percentage { get; set; }
}
