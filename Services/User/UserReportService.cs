using ProjectLedger.API.DTOs.Report;
using ProjectLedger.API.Repositories;

namespace ProjectLedger.API.Services;

/// <summary>
/// Implementation of IUserReportService.
/// Provides user-specific financial reporting, such as consolidated payment method activity.
/// </summary>
public class UserReportService : IUserReportService
{
    private readonly IExpenseRepository _expenseRepo;
    private readonly IIncomeRepository _incomeRepo;
    private readonly IPaymentMethodService _paymentMethodService;

    public UserReportService(
        IExpenseRepository expenseRepo,
        IIncomeRepository incomeRepo,
        IPaymentMethodService paymentMethodService)
    {
        _expenseRepo = expenseRepo;
        _incomeRepo = incomeRepo;
        _paymentMethodService = paymentMethodService;
    }

    /// <inheritdoc />
    public async Task<PaymentMethodReportResponse> GetPaymentMethodReportAsync(
        Guid userId, DateOnly? from, DateOnly? to,
        List<Guid>? paymentMethodIds, int? maxMovementsPerMethod,
        CancellationToken ct = default)
    {
        // Retrieve only payment methods belonging to the authenticated user
        var userPaymentMethods = (await _paymentMethodService.GetByOwnerUserIdAsync(userId, ct)).ToList();

        // If filtering by specific methods, validate that they belong to the user
        if (paymentMethodIds is { Count: > 0 })
        {
            var userPmIds = userPaymentMethods.Select(pm => pm.PmtId).ToHashSet();
            var invalid = paymentMethodIds.Where(id => !userPmIds.Contains(id)).ToList();
            if (invalid.Count > 0)
                throw new KeyNotFoundException("PaymentMethodNotFound");

            userPaymentMethods = userPaymentMethods
                .Where(pm => paymentMethodIds.Contains(pm.PmtId))
                .ToList();
        }

        var pmIds = userPaymentMethods.Select(pm => pm.PmtId).ToList();

        var allExpenses = (await _expenseRepo.GetByPaymentMethodIdsWithDetailsAsync(pmIds, from, to, ct))
            .ToList();

        var allIncomes = (await _incomeRepo.GetByPaymentMethodIdsWithDetailsAsync(pmIds, from, to, ct))
            .ToList();

        // Filter: only expenses/incomes from projects where the user is owner
        var ownerExpenses = allExpenses
            .Where(e => e.Project?.PrjOwnerUserId == userId)
            .ToList();

        var ownerIncomes = allIncomes
            .Where(i => i.Project?.PrjOwnerUserId == userId)
            .ToList();

        // Build rows per payment method (all amounts in the method's currency)
        var pmRows = userPaymentMethods.Select(pm =>
        {
            var pmExpenses = ownerExpenses
                .Where(e => e.ExpPaymentMethodId == pm.PmtId)
                .ToList();

            var pmIncomes = ownerIncomes
                .Where(i => i.IncPaymentMethodId == pm.PmtId)
                .ToList();

            // Amounts in the payment method's currency (AccountAmount)
            var pmTotalSpent = pmExpenses.Sum(e => e.ExpAccountAmount ?? e.ExpOriginalAmount);
            var pmTotalIncome = pmIncomes.Sum(i => ResolveIncomeAccountAmount(i, pm.PmtCurrency));
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var allDates = pmExpenses
                .Select(e => (DateOnly?)e.ExpExpenseDate)
                .Concat(pmIncomes.Select(i => (DateOnly?)i.IncIncomeDate))
                .Where(d => d.HasValue)
                .Select(d => d!.Value)
                .ToList();

            var firstUse = allDates.Count > 0 ? allDates.Min() : (DateOnly?)null;
            var lastUse = allDates.Count > 0 ? allDates.Max() : (DateOnly?)null;

            var daysSinceLastUse = lastUse.HasValue
                ? today.DayNumber - lastUse.Value.DayNumber
                : 0;

            // Top expense by AccountAmount
            var topExpenseEntity = pmExpenses
                .OrderByDescending(e => e.ExpAccountAmount ?? e.ExpOriginalAmount)
                .FirstOrDefault();

            // Top categories (top 5 by AccountAmount)
            var topCategories = pmExpenses
                .GroupBy(e => e.Category?.CatName ?? "Unknown")
                .Select(g =>
                {
                    var catTotal = g.Sum(e => e.ExpAccountAmount ?? e.ExpOriginalAmount);
                    return new PaymentMethodTopCategory
                    {
                        CategoryName = g.Key,
                        TotalAmount = catTotal,
                        ExpenseCount = g.Count(),
                        Percentage = pmTotalSpent > 0 ? Math.Round(catTotal / pmTotalSpent * 100, 2) : 0
                    };
                })
                .OrderByDescending(c => c.TotalAmount)
                .Take(5)
                .ToList();

            // Breakdown by project (using ConvertedAmount since it's the project currency)
            var pmTotalConverted = pmExpenses.Sum(e => e.ExpConvertedAmount);
            var projects = pmExpenses
                .GroupBy(e => new
                {
                    e.ExpProjectId,
                    Name = e.Project?.PrjName ?? "Unknown",
                    Currency = e.Project?.PrjCurrencyCode ?? "USD"
                })
                .Select(g => new PaymentMethodProjectBreakdown
                {
                    ProjectId = g.Key.ExpProjectId,
                    ProjectName = g.Key.Name,
                    ProjectCurrency = g.Key.Currency,
                    TotalSpent = g.Sum(e => e.ExpConvertedAmount),
                    ExpenseCount = g.Count(),
                    Percentage = pmTotalConverted > 0
                        ? Math.Round(g.Sum(e => e.ExpConvertedAmount) / pmTotalConverted * 100, 2)
                        : 0
                })
                .OrderByDescending(p => p.TotalSpent)
                .ToList();

            // Movements: use AccountAmount in the method's currency
            var orderedExpenses = pmExpenses.OrderByDescending(e => e.ExpExpenseDate).ToList();
            var expenseSource = maxMovementsPerMethod.HasValue
                ? orderedExpenses.Take(maxMovementsPerMethod.Value)
                : orderedExpenses;

            var expenseRows = expenseSource
                .Select(e => new PaymentMethodExpenseRow
                {
                    ExpenseId = e.ExpId,
                    Title = e.ExpTitle,
                    ExpenseDate = e.ExpExpenseDate,
                    ProjectId = e.ExpProjectId,
                    ProjectName = e.Project?.PrjName ?? "Unknown",
                    CategoryId = e.ExpCategoryId,
                    CategoryName = e.Category?.CatName ?? "Unknown",
                    Amount = e.ExpAccountAmount ?? e.ExpOriginalAmount,
                    Description = e.ExpDescription
                })
                .ToList();

            var orderedIncomes = pmIncomes.OrderByDescending(i => i.IncIncomeDate).ToList();
            var incomeSource = maxMovementsPerMethod.HasValue
                ? orderedIncomes.Take(maxMovementsPerMethod.Value)
                : orderedIncomes;

            var incomeRows = incomeSource
                .Select(i => new PaymentMethodIncomeRow
                {
                    IncomeId = i.IncId,
                    Title = i.IncTitle,
                    IncomeDate = i.IncIncomeDate,
                    ProjectId = i.IncProjectId,
                    ProjectName = i.Project?.PrjName ?? "Unknown",
                    CategoryId = i.IncCategoryId,
                    CategoryName = i.Category?.CatName ?? "Unknown",
                    Amount = ResolveIncomeAccountAmount(i, pm.PmtCurrency),
                    Description = i.IncDescription
                })
                .ToList();

            return new PaymentMethodReportRow
            {
                PaymentMethodId = pm.PmtId,
                Name = pm.PmtName,
                Type = pm.PmtType,
                Currency = pm.PmtCurrency,
                BankName = pm.PmtBankName,
                OwnerPartnerName = pm.OwnerPartner?.PtrName,
                TotalSpent = pmTotalSpent,
                ExpenseCount = pmExpenses.Count,
                TotalIncome = pmTotalIncome,
                IncomeCount = pmIncomes.Count,
                NetFlow = pmTotalIncome - pmTotalSpent,
                AverageExpenseAmount = pmExpenses.Count > 0
                    ? Math.Round(pmTotalSpent / pmExpenses.Count, 2)
                    : 0,
                AverageIncomeAmount = pmIncomes.Count > 0
                    ? Math.Round(pmTotalIncome / pmIncomes.Count, 2)
                    : 0,
                FirstUseDate = firstUse,
                LastUseDate = lastUse,
                DaysSinceLastUse = daysSinceLastUse,
                IsInactive = lastUse.HasValue && daysSinceLastUse > 90,
                TopExpense = topExpenseEntity is null ? null : new PaymentMethodTopExpense
                {
                    ExpenseId = topExpenseEntity.ExpId,
                    Title = topExpenseEntity.ExpTitle,
                    Amount = topExpenseEntity.ExpAccountAmount ?? topExpenseEntity.ExpOriginalAmount,
                    ExpenseDate = topExpenseEntity.ExpExpenseDate,
                    ProjectName = topExpenseEntity.Project?.PrjName ?? "Unknown",
                    CategoryName = topExpenseEntity.Category?.CatName ?? "Unknown"
                },
                TopCategories = topCategories,
                Projects = projects,
                Expenses = expenseRows,
                Incomes = incomeRows,
                TotalExpensesInPeriod = pmExpenses.Count,
                ExpensesShown = expenseRows.Count,
                TotalIncomesInPeriod = pmIncomes.Count,
                IncomesShown = incomeRows.Count
            };
        })
        .OrderByDescending(pm => pm.TotalSpent)
        .ToList();

        // Monthly trend (per method, in each method's currency)
        var monthlyTrend = ownerExpenses
            .Select(e => new { Year = e.ExpExpenseDate.Year, Month = e.ExpExpenseDate.Month })
            .Union(ownerIncomes.Select(i => new { Year = i.IncIncomeDate.Year, Month = i.IncIncomeDate.Month }))
            .Distinct()
            .OrderBy(x => x.Year)
            .ThenBy(x => x.Month)
            .Select(m =>
            {
                var monthExpenses = ownerExpenses
                    .Where(e => e.ExpExpenseDate.Year == m.Year && e.ExpExpenseDate.Month == m.Month)
                    .ToList();
                var monthIncomes = ownerIncomes
                    .Where(i => i.IncIncomeDate.Year == m.Year && i.IncIncomeDate.Month == m.Month)
                    .ToList();

                var monthMethodIds = monthExpenses.Select(e => e.ExpPaymentMethodId)
                    .Union(monthIncomes.Select(i => i.IncPaymentMethodId))
                    .Distinct()
                    .ToList();

                return new PaymentMethodMonthlyRow
                {
                    Year = m.Year,
                    Month = m.Month,
                    MonthLabel = $"{new DateTime(m.Year, m.Month, 1):MMMM yyyy}",
                    ByMethod = monthMethodIds
                        .Select(methodId =>
                        {
                            var pm = userPaymentMethods.FirstOrDefault(p => p.PmtId == methodId);
                            var pmCurrency = pm?.PmtCurrency ?? "USD";
                            var methodExpenses = monthExpenses.Where(e => e.ExpPaymentMethodId == methodId).ToList();
                            var methodIncomes = monthIncomes.Where(i => i.IncPaymentMethodId == methodId).ToList();
                            var methodSpent = methodExpenses.Sum(e => e.ExpAccountAmount ?? e.ExpOriginalAmount);
                            var methodIncome = methodIncomes.Sum(i => ResolveIncomeAccountAmount(i, pmCurrency));

                            return new PaymentMethodMonthBreakdown
                            {
                                PaymentMethodId = methodId,
                                Name = pm?.PmtName ?? "Unknown",
                                Currency = pmCurrency,
                                TotalSpent = methodSpent,
                                ExpenseCount = methodExpenses.Count,
                                TotalIncome = methodIncome,
                                IncomeCount = methodIncomes.Count,
                                NetFlow = methodIncome - methodSpent
                            };
                        })
                        .OrderByDescending(b => b.TotalSpent)
                        .ToList()
                };
            })
            .ToList();

        return new PaymentMethodReportResponse
        {
            UserId = userId,
            DateFrom = from,
            DateTo = to,
            GeneratedAt = DateTime.UtcNow,
            PaymentMethods = pmRows,
            MonthlyTrend = monthlyTrend
        };
    }

    private static decimal ResolveIncomeAccountAmount(Models.Income income, string paymentMethodCurrency)
    {
        if (income.IncAccountAmount is > 0)
            return income.IncAccountAmount.Value;

        if (string.Equals(income.IncOriginalCurrency, paymentMethodCurrency, StringComparison.OrdinalIgnoreCase))
            return income.IncOriginalAmount;

        var projectCurrency = income.Project?.PrjCurrencyCode;
        if (!string.IsNullOrWhiteSpace(projectCurrency)
            && string.Equals(projectCurrency, paymentMethodCurrency, StringComparison.OrdinalIgnoreCase))
        {
            return income.IncConvertedAmount;
        }

        return income.IncOriginalAmount;
    }
}
