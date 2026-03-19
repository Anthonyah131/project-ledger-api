using ProjectLedger.API.DTOs.Report;
using ProjectLedger.API.Extensions.Mappings;
using ProjectLedger.API.Repositories;

namespace ProjectLedger.API.Services;

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

    public async Task<PaymentMethodReportResponse> GetPaymentMethodReportAsync(
        Guid userId, DateOnly? from, DateOnly? to, Guid? paymentMethodId, CancellationToken ct = default)
    {
        // Obtener solo los métodos de pago del usuario autenticado
        var userPaymentMethods = (await _paymentMethodService.GetByOwnerUserIdAsync(userId, ct)).ToList();

        // Si se filtra por un método específico, validar que pertenece al usuario
        if (paymentMethodId.HasValue)
        {
            var exists = userPaymentMethods.Any(pm => pm.PmtId == paymentMethodId.Value);
            if (!exists)
                throw new KeyNotFoundException("Payment method not found.");

            userPaymentMethods = userPaymentMethods
                .Where(pm => pm.PmtId == paymentMethodId.Value)
                .ToList();
        }

        var pmIds = userPaymentMethods.Select(pm => pm.PmtId).ToList();

        // Obtener todos los gastos vinculados a esos métodos de pago
        var allExpenses = (await _expenseRepo.GetByPaymentMethodIdsWithDetailsAsync(pmIds, from, to, ct))
            .ToList();

        var allIncomes = (await _incomeRepo.GetByPaymentMethodIdsWithDetailsAsync(pmIds, from, to, ct))
            .ToList();

        // Filtrar: solo gastos de proyectos donde el usuario es owner
        var ownerExpenses = allExpenses
            .Where(e => e.Project?.PrjOwnerUserId == userId)
            .ToList();

        // Filtrar: solo ingresos de proyectos donde el usuario es owner
        var ownerIncomes = allIncomes
            .Where(i => i.Project?.PrjOwnerUserId == userId)
            .ToList();

        var grandTotal = ownerExpenses.Sum(e => e.ExpConvertedAmount);
        var grandTotalIncome = ownerIncomes.Sum(i => i.IncConvertedAmount);

        // Construir filas por método de pago
        var pmRows = userPaymentMethods.Select(pm =>
        {
            var pmExpenses = ownerExpenses
                .Where(e => e.ExpPaymentMethodId == pm.PmtId)
                .ToList();

            var pmIncomes = ownerIncomes
                .Where(i => i.IncPaymentMethodId == pm.PmtId)
                .ToList();

            // pmTotal: sum in the payment method's own currency (for per-method stats)
            var pmTotal = pmExpenses.Sum(e => e.ExpOriginalAmount);
            var pmIncomeTotal = pmIncomes.Sum(i => ResolveIncomeAccountAmount(i, pm.PmtCurrency));
            // pmTotalConverted: sum in project currency (for cross-method % comparisons)
            var pmTotalConverted = pmExpenses.Sum(e => e.ExpConvertedAmount);
            var pmIncomeTotalConverted = pmIncomes.Sum(i => i.IncConvertedAmount);
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var lastUsed = pmExpenses
                .OrderByDescending(e => e.ExpExpenseDate)
                .Select(e => (DateOnly?)e.ExpExpenseDate)
                .FirstOrDefault();

            var daysSinceLastUse = lastUsed.HasValue
                ? today.DayNumber - lastUsed.Value.DayNumber
                : int.MaxValue;

            // Top expense: pick the largest by original (native) amount
            var topExpenseEntity = pmExpenses.OrderByDescending(e => e.ExpOriginalAmount).FirstOrDefault();

            // Top categories (top 5 by total original amount in method's native currency)
            var topCategories = pmExpenses
                .GroupBy(e => e.Category?.CatName ?? "Unknown")
                .Select(g =>
                {
                    var catTotal = g.Sum(e => e.ExpOriginalAmount);
                    return new PaymentMethodTopCategory
                    {
                        CategoryName = g.Key,
                        TotalAmount = catTotal,
                        ExpenseCount = g.Count(),
                        Percentage = pmTotal > 0 ? Math.Round(catTotal / pmTotal * 100, 2) : 0
                    };
                })
                .OrderByDescending(c => c.TotalAmount)
                .Take(5)
                .ToList();

            // Desglose por proyecto
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

            // Cap expenses at 20 most recent
            const int maxExpenses = 20;
            var expenseRows = pmExpenses
                .OrderByDescending(e => e.ExpExpenseDate)
                .Take(maxExpenses)
                .Select(e => new PaymentMethodExpenseRow
                {
                    ExpenseId = e.ExpId,
                    Title = e.ExpTitle,
                    ExpenseDate = e.ExpExpenseDate,
                    ProjectId = e.ExpProjectId,
                    ProjectName = e.Project?.PrjName ?? "Unknown",
                    CategoryId = e.ExpCategoryId,
                    CategoryName = e.Category?.CatName ?? "Unknown",
                    OriginalAmount = e.ExpOriginalAmount,
                    OriginalCurrency = e.ExpOriginalCurrency,
                    ConvertedAmount = e.ExpConvertedAmount,
                    ProjectCurrency = e.Project?.PrjCurrencyCode ?? "USD",
                    CurrencyExchanges = e.CurrencyExchanges?.Select(x => x.ToResponse()).ToList(),
                    Description = e.ExpDescription
                })
                .ToList();

            const int maxIncomes = 20;
            var incomeRows = pmIncomes
                .OrderByDescending(i => i.IncIncomeDate)
                .Take(maxIncomes)
                .Select(i => new PaymentMethodIncomeRow
                {
                    IncomeId = i.IncId,
                    Title = i.IncTitle,
                    IncomeDate = i.IncIncomeDate,
                    ProjectId = i.IncProjectId,
                    ProjectName = i.Project?.PrjName ?? "Unknown",
                    CategoryId = i.IncCategoryId,
                    CategoryName = i.Category?.CatName ?? "Unknown",
                    OriginalAmount = i.IncOriginalAmount,
                    OriginalCurrency = i.IncOriginalCurrency,
                    AccountAmount = i.IncAccountAmount ?? ResolveIncomeAccountAmount(i, pm.PmtCurrency),
                    AccountCurrency = i.IncAccountCurrency ?? pm.PmtCurrency,
                    ConvertedAmount = i.IncConvertedAmount,
                    ProjectCurrency = i.Project?.PrjCurrencyCode ?? "USD",
                    CurrencyExchanges = i.CurrencyExchanges?.Select(x => x.ToResponse()).ToList(),
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
                TotalSpent = pmTotal,
                ExpenseCount = pmExpenses.Count,
                TotalIncome = pmIncomeTotal,
                IncomeCount = pmIncomes.Count,
                NetFlow = pmIncomeTotalConverted - pmTotalConverted,
                Percentage = grandTotal > 0 ? Math.Round(pmTotalConverted / grandTotal * 100, 2) : 0,
                AverageExpenseAmount = pmExpenses.Count > 0
                    ? Math.Round(pmTotal / pmExpenses.Count, 2)
                    : 0,
                AverageIncomeAmount = pmIncomes.Count > 0
                    ? Math.Round(pmIncomeTotal / pmIncomes.Count, 2)
                    : 0,
                FirstUseDate = pmExpenses
                    .OrderBy(e => e.ExpExpenseDate)
                    .Select(e => (DateOnly?)e.ExpExpenseDate)
                    .FirstOrDefault(),
                LastUseDate = lastUsed,
                DaysSinceLastUse = lastUsed.HasValue ? daysSinceLastUse : 0,
                IsInactive = lastUsed.HasValue && daysSinceLastUse > 90,
                TopExpense = topExpenseEntity is null ? null : new PaymentMethodTopExpense
                {
                    ExpenseId = topExpenseEntity.ExpId,
                    Title = topExpenseEntity.ExpTitle,
                    Amount = topExpenseEntity.ExpOriginalAmount,
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

        // Tendencia mensual
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
                var monthTotal = monthExpenses.Sum(e => e.ExpConvertedAmount);
                var monthIncome = monthIncomes.Sum(i => i.IncConvertedAmount);

                var monthMethodIds = monthExpenses.Select(e => e.ExpPaymentMethodId)
                    .Union(monthIncomes.Select(i => i.IncPaymentMethodId))
                    .Distinct()
                    .ToList();

                return new PaymentMethodMonthlyRow
                {
                    Year = m.Year,
                    Month = m.Month,
                    MonthLabel = $"{new DateTime(m.Year, m.Month, 1):MMMM yyyy}",
                    TotalSpent = monthTotal,
                    ExpenseCount = monthExpenses.Count,
                    TotalIncome = monthIncome,
                    IncomeCount = monthIncomes.Count,
                    NetBalance = monthIncome - monthTotal,
                    ByMethod = monthMethodIds
                        .Select(methodId =>
                        {
                            var methodExpenses = monthExpenses.Where(e => e.ExpPaymentMethodId == methodId).ToList();
                            var methodIncomes = monthIncomes.Where(i => i.IncPaymentMethodId == methodId).ToList();
                            var methodTotal = methodExpenses.Sum(e => e.ExpConvertedAmount);
                            var methodIncome = methodIncomes.Sum(i => i.IncConvertedAmount);
                            var methodName = userPaymentMethods.FirstOrDefault(pm => pm.PmtId == methodId)?.PmtName ?? "Unknown";

                            return new PaymentMethodMonthBreakdown
                            {
                                PaymentMethodId = methodId,
                                Name = methodName,
                                TotalSpent = methodTotal,
                                ExpenseCount = methodExpenses.Count,
                                TotalIncome = methodIncome,
                                IncomeCount = methodIncomes.Count,
                                NetFlow = methodIncome - methodTotal,
                                Percentage = monthTotal > 0
                                    ? Math.Round(methodTotal / monthTotal * 100, 2)
                                    : 0
                            };
                        })
                        .OrderByDescending(m => m.TotalSpent)
                        .ToList()
                };
            })
            .ToList();

        // Root-level aggregate stats
        var peakTrendMonth = monthlyTrend.OrderByDescending(m => m.TotalSpent).FirstOrDefault();
        var mostUsed = pmRows.OrderByDescending(p => p.ExpenseCount).FirstOrDefault();
        var highestSpend = pmRows.OrderByDescending(p => p.TotalSpent).FirstOrDefault();

        return new PaymentMethodReportResponse
        {
            UserId = userId,
            DateFrom = from,
            DateTo = to,
            GeneratedAt = DateTime.UtcNow,
            GrandTotalSpent = grandTotal,
            GrandTotalExpenseCount = ownerExpenses.Count,
            GrandTotalIncome = grandTotalIncome,
            GrandTotalIncomeCount = ownerIncomes.Count,
            GrandNetFlow = grandTotalIncome - grandTotal,
            GrandAverageExpenseAmount = ownerExpenses.Count > 0
                ? Math.Round(grandTotal / ownerExpenses.Count, 2)
                : 0,
            GrandAverageIncomeAmount = ownerIncomes.Count > 0
                ? Math.Round(grandTotalIncome / ownerIncomes.Count, 2)
                : 0,
            AverageMonthlySpend = monthlyTrend.Count > 0
                ? Math.Round(monthlyTrend.Sum(m => m.TotalSpent) / monthlyTrend.Count, 2)
                : 0,
            PeakMonth = peakTrendMonth is null ? null : new PeakMonthInfo
            {
                MonthLabel = peakTrendMonth.MonthLabel,
                Total = peakTrendMonth.TotalSpent
            },
            MostUsedMethod = mostUsed is null ? null : new MethodReference
            {
                PaymentMethodId = mostUsed.PaymentMethodId,
                Name = mostUsed.Name
            },
            HighestSpendMethod = highestSpend is null ? null : new MethodReference
            {
                PaymentMethodId = highestSpend.PaymentMethodId,
                Name = highestSpend.Name
            },
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
