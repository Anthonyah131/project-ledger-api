using ProjectLedger.API.DTOs.Common;
using ProjectLedger.API.DTOs.Currency;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Extensions.Mappings;

/// <summary>
/// Mapping extensions for TransactionCurrencyExchange and ProjectAlternativeCurrency entity-to-DTO conversions.
/// </summary>
public static class TransactionCurrencyExchangeMappingExtensions
{
    public static CurrencyExchangeResponse ToResponse(this TransactionCurrencyExchange entity) => new()
    {
        Id = entity.TceId,
        CurrencyCode = entity.TceCurrencyCode,
        ExchangeRate = entity.TceExchangeRate,
        ConvertedAmount = entity.TceConvertedAmount
    };

    public static TransactionCurrencyExchange ToEntity(
        this CurrencyExchangeRequest request, string entityType, Guid entityId) => new()
    {
        TceId = Guid.NewGuid(),
        TceExpenseId = entityType == "expense" ? entityId : null,
        TceIncomeId = entityType == "income" ? entityId : null,
        TceCurrencyCode = request.CurrencyCode,
        TceExchangeRate = request.ExchangeRate,
        TceConvertedAmount = request.ConvertedAmount,
        TceCreatedAt = DateTime.UtcNow
    };

    public static ProjectAlternativeCurrencyResponse ToResponse(this ProjectAlternativeCurrency entity) => new()
    {
        Id = entity.PacId,
        CurrencyCode = entity.PacCurrencyCode,
        CurrencyName = entity.Currency?.CurName ?? string.Empty,
        CurrencySymbol = entity.Currency?.CurSymbol ?? string.Empty,
        CreatedAt = entity.PacCreatedAt
    };
}
