using ProjectLedger.API.DTOs.Report;

namespace ProjectLedger.API.Services;

public partial class ReportService
{
    public async Task<MonthComparisonResponse> GetMonthComparisonAsync(
        Guid projectId, CancellationToken ct = default)
    {
        var project = await _projectService.GetByIdAsync(projectId, ct)
            ?? throw new KeyNotFoundException("Project not found.");

        var now = DateTime.UtcNow;
        var currentMonth = new DateOnly(now.Year, now.Month, 1);
        var previousMonth = currentMonth.AddMonths(-1);

        var expenses = (await _expenseRepo.GetByProjectIdWithDetailsAsync(projectId, ct))
            .Where(e => !e.ExpIsTemplate)
            .ToList();

        var incomes = (await _incomeRepo.GetByProjectIdAsync(projectId, ct)).ToList();

        var currentExpenses = expenses
            .Where(e => e.ExpExpenseDate.Year == currentMonth.Year
                     && e.ExpExpenseDate.Month == currentMonth.Month)
            .ToList();

        var previousExpenses = expenses
            .Where(e => e.ExpExpenseDate.Year == previousMonth.Year
                     && e.ExpExpenseDate.Month == previousMonth.Month)
            .ToList();

        var currentIncomes = incomes
            .Where(i => i.IncIncomeDate.Year == currentMonth.Year
                     && i.IncIncomeDate.Month == currentMonth.Month)
            .ToList();

        var previousIncomes = incomes
            .Where(i => i.IncIncomeDate.Year == previousMonth.Year
                     && i.IncIncomeDate.Month == previousMonth.Month)
            .ToList();

        var currentTotal = currentExpenses.Sum(e => e.ExpConvertedAmount);
        var previousTotal = previousExpenses.Sum(e => e.ExpConvertedAmount);
        var currentIncomeTotal = currentIncomes.Sum(i => i.IncConvertedAmount);
        var previousIncomeTotal = previousIncomes.Sum(i => i.IncConvertedAmount);
        var change = currentTotal - previousTotal;

        // Build alternative currency totals if project has them
        var altCurrencies = (await _altCurrencyRepo.GetByProjectIdAsync(projectId, ct)).ToList();

        var currentMonthSummary = new MonthSummary
        {
            Year = currentMonth.Year,
            Month = currentMonth.Month,
            MonthLabel = $"{new DateTime(currentMonth.Year, currentMonth.Month, 1):MMMM yyyy}",
            TotalSpent = currentTotal,
            ExpenseCount = currentExpenses.Count,
            TotalIncome = currentIncomeTotal,
            IncomeCount = currentIncomes.Count,
            NetBalance = currentIncomeTotal - currentTotal
        };

        var previousMonthSummary = new MonthSummary
        {
            Year = previousMonth.Year,
            Month = previousMonth.Month,
            MonthLabel = $"{new DateTime(previousMonth.Year, previousMonth.Month, 1):MMMM yyyy}",
            TotalSpent = previousTotal,
            ExpenseCount = previousExpenses.Count,
            TotalIncome = previousIncomeTotal,
            IncomeCount = previousIncomes.Count,
            NetBalance = previousIncomeTotal - previousTotal
        };

        if (altCurrencies.Count > 0)
        {
            currentMonthSummary.AlternativeCurrencyTotals = BuildAltCurrencyTotals(
                altCurrencies, currentExpenses, currentIncomes);
            previousMonthSummary.AlternativeCurrencyTotals = BuildAltCurrencyTotals(
                altCurrencies, previousExpenses, previousIncomes);
        }

        return new MonthComparisonResponse
        {
            ProjectId = projectId,
            ProjectName = project.PrjName,
            CurrencyCode = project.PrjCurrencyCode,
            GeneratedAt = DateTime.UtcNow,
            CurrentMonth = currentMonthSummary,
            PreviousMonth = previousMonthSummary,
            ChangeAmount = change,
            ChangePercentage = previousTotal > 0
                ? Math.Round(change / previousTotal * 100, 2)
                : null,
            HasPreviousData = previousExpenses.Count > 0 || previousIncomes.Count > 0
        };
    }

    private static List<AlternativeCurrencyTotal>? BuildAltCurrencyTotals(
        List<Models.ProjectAlternativeCurrency> altCurrencies,
        List<Models.Expense> expenses,
        List<Models.Income> incomes)
    {
        var expCurrencyExchanges = expenses
            .SelectMany(e => e.CurrencyExchanges ?? [])
            .ToList();
        var incCurrencyExchanges = incomes
            .SelectMany(i => i.CurrencyExchanges ?? [])
            .ToList();

        var totals = altCurrencies.Select(ac =>
        {
            var altSpent = expCurrencyExchanges
                .Where(ce => ce.TceCurrencyCode == ac.PacCurrencyCode)
                .Sum(ce => ce.TceConvertedAmount);
            var altIncome = incCurrencyExchanges
                .Where(ce => ce.TceCurrencyCode == ac.PacCurrencyCode)
                .Sum(ce => ce.TceConvertedAmount);

            return new AlternativeCurrencyTotal
            {
                CurrencyCode = ac.PacCurrencyCode,
                TotalSpent = altSpent,
                TotalIncome = altIncome,
                NetBalance = altIncome - altSpent
            };
        })
        .Where(t => t.TotalSpent > 0 || t.TotalIncome > 0)
        .ToList();

        return totals.Count > 0 ? totals : null;
    }
}
