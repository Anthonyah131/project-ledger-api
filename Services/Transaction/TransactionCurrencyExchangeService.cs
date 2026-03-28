using ProjectLedger.API.DTOs.Common;
using ProjectLedger.API.Extensions.Mappings;
using ProjectLedger.API.Models;
using ProjectLedger.API.Repositories;

namespace ProjectLedger.API.Services;

/// <summary>
/// Gestión de valores de tipo de cambio por transacción (gasto/ingreso).
/// </summary>
public class TransactionCurrencyExchangeService : ITransactionCurrencyExchangeService
{
    private readonly ITransactionCurrencyExchangeRepository _repo;

    public TransactionCurrencyExchangeService(ITransactionCurrencyExchangeRepository repo)
    {
        _repo = repo;
    }

    public async Task<IEnumerable<TransactionCurrencyExchange>> GetByEntityAsync(
        string entityType, Guid entityId, CancellationToken ct = default)
        => await _repo.GetByEntityAsync(entityType, entityId, ct);

    public async Task SaveExchangesAsync(
        string entityType, Guid entityId, List<CurrencyExchangeRequest> exchanges, CancellationToken ct = default)
    {
        foreach (var exchange in exchanges)
        {
            var entity = exchange.ToEntity(entityType, entityId);
            await _repo.AddAsync(entity, ct);
        }
        await _repo.SaveChangesAsync(ct);
    }

    public async Task SaveExchangesAsync(
        string entityType, Guid entityId, IReadOnlyList<TransactionExchangeInput> exchanges, CancellationToken ct = default)
    {
        foreach (var exchange in exchanges)
        {
            var entity = new TransactionCurrencyExchange
            {
                TceId = Guid.NewGuid(),
                TceExpenseId = entityType == "expense" ? entityId : null,
                TceIncomeId = entityType == "income" ? entityId : null,
                TceCurrencyCode = exchange.CurrencyCode,
                TceExchangeRate = exchange.ExchangeRate,
                TceConvertedAmount = exchange.ConvertedAmount,
                TceCreatedAt = DateTime.UtcNow
            };
            await _repo.AddAsync(entity, ct);
        }
        await _repo.SaveChangesAsync(ct);
    }

    public async Task ReplaceExchangesAsync(
        string entityType, Guid entityId, List<CurrencyExchangeRequest> exchanges, CancellationToken ct = default)
    {
        // Eliminar exchanges existentes
        await _repo.DeleteByEntityAsync(entityType, entityId, ct);

        // Insertar nuevos
        foreach (var exchange in exchanges)
        {
            var entity = exchange.ToEntity(entityType, entityId);
            await _repo.AddAsync(entity, ct);
        }
        await _repo.SaveChangesAsync(ct);
    }
}
