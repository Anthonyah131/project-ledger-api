using ProjectLedger.API.DTOs.Common;

namespace ProjectLedger.API.Services;

public interface ISplitCurrencyExchangeService
{
    /// <summary>
    /// Creates SplitCurrencyExchanges for all splits of an expense,
    /// calculating the proportional amount for each alternative currency.
    /// </summary>
    Task SaveForExpenseAsync(Guid expenseId, decimal expenseOriginalAmount, IReadOnlyList<CurrencyExchangeRequest> exchanges, CancellationToken ct = default);

    /// <summary>
    /// Deletes and recreates SplitCurrencyExchanges for all splits of an expense.
    /// If exchanges is empty, it only deletes existing ones.
    /// </summary>
    Task ReplaceForExpenseAsync(Guid expenseId, decimal expenseOriginalAmount, IReadOnlyList<CurrencyExchangeRequest> exchanges, CancellationToken ct = default);

    /// <summary>Equivalent of SaveForExpenseAsync for incomes.</summary>
    Task SaveForIncomeAsync(Guid incomeId, decimal incomeOriginalAmount, IReadOnlyList<CurrencyExchangeRequest> exchanges, CancellationToken ct = default);

    /// <summary>Equivalent of ReplaceForExpenseAsync for incomes.</summary>
    Task ReplaceForIncomeAsync(Guid incomeId, decimal incomeOriginalAmount, IReadOnlyList<CurrencyExchangeRequest> exchanges, CancellationToken ct = default);
}
