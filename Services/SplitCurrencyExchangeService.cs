using ProjectLedger.API.DTOs.Common;
using ProjectLedger.API.Models;
using ProjectLedger.API.Repositories;

namespace ProjectLedger.API.Services;

/// <summary>
/// Gestiona las equivalencias de splits en monedas alternativas del proyecto.
/// Los montos se calculan proporcionalmente a partir de los exchanges del movimiento padre.
/// </summary>
public class SplitCurrencyExchangeService : ISplitCurrencyExchangeService
{
    private readonly ISplitCurrencyExchangeRepository _repo;
    private readonly IExpenseSplitRepository _expenseSplitRepo;
    private readonly IIncomeSplitRepository _incomeSplitRepo;

    public SplitCurrencyExchangeService(
        ISplitCurrencyExchangeRepository repo,
        IExpenseSplitRepository expenseSplitRepo,
        IIncomeSplitRepository incomeSplitRepo)
    {
        _repo = repo;
        _expenseSplitRepo = expenseSplitRepo;
        _incomeSplitRepo = incomeSplitRepo;
    }

    public async Task SaveForExpenseAsync(
        Guid expenseId,
        decimal expenseOriginalAmount,
        IReadOnlyList<CurrencyExchangeRequest> exchanges,
        CancellationToken ct = default)
    {
        if (exchanges.Count == 0) return;

        var splits = await _expenseSplitRepo.GetByExpenseIdAsync(expenseId, ct);
        foreach (var split in splits)
        {
            var ratio = expenseOriginalAmount > 0
                ? split.ExsResolvedAmount / expenseOriginalAmount
                : 0m;

            foreach (var exchange in exchanges)
            {
                await _repo.AddAsync(new SplitCurrencyExchange
                {
                    SceId = Guid.NewGuid(),
                    SceExpenseSplitId = split.ExsId,
                    SceCurrencyCode = exchange.CurrencyCode,
                    SceExchangeRate = exchange.ExchangeRate,
                    SceConvertedAmount = Math.Round(ratio * exchange.ConvertedAmount, 2)
                }, ct);
            }
        }
        await _repo.SaveChangesAsync(ct);
    }

    public async Task ReplaceForExpenseAsync(
        Guid expenseId,
        decimal expenseOriginalAmount,
        IReadOnlyList<CurrencyExchangeRequest> exchanges,
        CancellationToken ct = default)
    {
        await _repo.DeleteByExpenseIdAsync(expenseId, ct);
        await _repo.SaveChangesAsync(ct);
        if (exchanges.Count > 0)
            await SaveForExpenseAsync(expenseId, expenseOriginalAmount, exchanges, ct);
    }

    public async Task SaveForIncomeAsync(
        Guid incomeId,
        decimal incomeOriginalAmount,
        IReadOnlyList<CurrencyExchangeRequest> exchanges,
        CancellationToken ct = default)
    {
        if (exchanges.Count == 0) return;

        var splits = await _incomeSplitRepo.GetByIncomeIdAsync(incomeId, ct);
        foreach (var split in splits)
        {
            var ratio = incomeOriginalAmount > 0
                ? split.InsResolvedAmount / incomeOriginalAmount
                : 0m;

            foreach (var exchange in exchanges)
            {
                await _repo.AddAsync(new SplitCurrencyExchange
                {
                    SceId = Guid.NewGuid(),
                    SceIncomeSplitId = split.InsId,
                    SceCurrencyCode = exchange.CurrencyCode,
                    SceExchangeRate = exchange.ExchangeRate,
                    SceConvertedAmount = Math.Round(ratio * exchange.ConvertedAmount, 2)
                }, ct);
            }
        }
        await _repo.SaveChangesAsync(ct);
    }

    public async Task ReplaceForIncomeAsync(
        Guid incomeId,
        decimal incomeOriginalAmount,
        IReadOnlyList<CurrencyExchangeRequest> exchanges,
        CancellationToken ct = default)
    {
        await _repo.DeleteByIncomeIdAsync(incomeId, ct);
        await _repo.SaveChangesAsync(ct);
        if (exchanges.Count > 0)
            await SaveForIncomeAsync(incomeId, incomeOriginalAmount, exchanges, ct);
    }
}
