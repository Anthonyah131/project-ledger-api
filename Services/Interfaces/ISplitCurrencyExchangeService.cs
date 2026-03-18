using ProjectLedger.API.DTOs.Common;

namespace ProjectLedger.API.Services;

public interface ISplitCurrencyExchangeService
{
    /// <summary>
    /// Crea los SplitCurrencyExchange para todos los splits de un gasto,
    /// calculando el monto proporcional por moneda alternativa.
    /// </summary>
    Task SaveForExpenseAsync(Guid expenseId, decimal expenseOriginalAmount, IReadOnlyList<CurrencyExchangeRequest> exchanges, CancellationToken ct = default);

    /// <summary>
    /// Elimina y recrea los SplitCurrencyExchange para todos los splits de un gasto.
    /// Si exchanges está vacío, solo elimina los existentes.
    /// </summary>
    Task ReplaceForExpenseAsync(Guid expenseId, decimal expenseOriginalAmount, IReadOnlyList<CurrencyExchangeRequest> exchanges, CancellationToken ct = default);

    /// <summary>Equivalente de SaveForExpenseAsync para ingresos.</summary>
    Task SaveForIncomeAsync(Guid incomeId, decimal incomeOriginalAmount, IReadOnlyList<CurrencyExchangeRequest> exchanges, CancellationToken ct = default);

    /// <summary>Equivalente de ReplaceForExpenseAsync para ingresos.</summary>
    Task ReplaceForIncomeAsync(Guid incomeId, decimal incomeOriginalAmount, IReadOnlyList<CurrencyExchangeRequest> exchanges, CancellationToken ct = default);
}
