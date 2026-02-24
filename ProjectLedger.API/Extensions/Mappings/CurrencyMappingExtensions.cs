using System.Text.Json;
using ProjectLedger.API.Models;
using ProjectLedger.API.DTOs.Currency;

namespace ProjectLedger.API.Extensions.Mappings;

public static class CurrencyMappingExtensions
{
    public static CurrencyResponse ToResponse(this Currency entity) => new()
    {
        Code = entity.CurCode,
        Name = entity.CurName,
        Symbol = entity.CurSymbol,
        DecimalPlaces = entity.CurDecimalPlaces,
        IsActive = entity.CurIsActive
    };

    public static IEnumerable<CurrencyResponse> ToResponse(this IEnumerable<Currency> entities)
        => entities.Select(e => e.ToResponse());
}
