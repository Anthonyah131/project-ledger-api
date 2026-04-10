namespace ProjectLedger.API.DTOs.Report;

// ── Payment Method Report (User-scoped) ─────────────────────

/// <summary>
/// User payment methods report. Without general totals;
/// each method shows its own totals in the method's currency.
/// Requires CanUseAdvancedReports.
/// </summary>
public class PaymentMethodReportResponse
{
    public Guid UserId { get; set; }
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
    public DateTime GeneratedAt { get; set; }

    public List<PaymentMethodReportRow> PaymentMethods { get; set; } = [];
    public List<PaymentMethodMonthlyRow> MonthlyTrend { get; set; } = [];
}

public class PaymentMethodReportRow
{
    public Guid PaymentMethodId { get; set; }
    public string Name { get; set; } = null!;
    public string Type { get; set; } = null!;
    public string Currency { get; set; } = null!;
    public string? BankName { get; set; }
    public string? OwnerPartnerName { get; set; }

    // Statistics (in the payment method's currency)
    public decimal TotalSpent { get; set; }
    public int ExpenseCount { get; set; }
    public decimal TotalIncome { get; set; }
    public int IncomeCount { get; set; }
    public decimal NetFlow { get; set; }
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
    public decimal Amount { get; set; }
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
    public decimal Amount { get; set; }
    public string? Description { get; set; }
}

public class PaymentMethodMonthlyRow
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthLabel { get; set; } = null!;
    public List<PaymentMethodMonthBreakdown> ByMethod { get; set; } = [];
}

public class PaymentMethodMonthBreakdown
{
    public Guid PaymentMethodId { get; set; }
    public string Name { get; set; } = null!;
    public string Currency { get; set; } = null!;
    public decimal TotalSpent { get; set; }
    public int ExpenseCount { get; set; }
    public decimal TotalIncome { get; set; }
    public int IncomeCount { get; set; }
    public decimal NetFlow { get; set; }
}
