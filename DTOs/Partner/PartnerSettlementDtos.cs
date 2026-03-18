using ProjectLedger.API.DTOs.Common;

namespace ProjectLedger.API.DTOs.Partner;

// ── POST /projects/:id/partner-settlements ────────────────

public record CreateSettlementRequest(
    Guid FromPartnerId,
    Guid ToPartnerId,
    decimal Amount,
    string Currency,
    decimal ExchangeRate,
    DateOnly SettlementDate,
    string? Description,
    string? Notes,
    /// <summary>
    /// Monto de la liquidación en monedas alternativas del proyecto.
    /// Si se omite o es null, no se guardan conversiones alternativas.
    /// </summary>
    List<CurrencyExchangeRequest>? CurrencyExchanges = null
);

// ── PATCH /projects/:id/partner-settlements/:id ───────────

public record UpdateSettlementRequest(
    decimal? Amount,
    string? Currency,
    decimal? ExchangeRate,
    DateOnly? SettlementDate,
    string? Description,
    string? Notes,
    /// <summary>
    /// Si se provee (incluso lista vacía), reemplaza todas las conversiones existentes.
    /// Si se omite (null), no modifica las conversiones existentes.
    /// </summary>
    List<CurrencyExchangeRequest>? CurrencyExchanges = null
);

// ── GET /projects/:id/partner-settlements (list item) ─────

public record SettlementResponse(
    Guid Id,
    Guid ProjectId,
    Guid FromPartnerId,
    string FromPartnerName,
    Guid ToPartnerId,
    string ToPartnerName,
    decimal Amount,
    string Currency,
    decimal ExchangeRate,
    decimal ConvertedAmount,
    DateOnly SettlementDate,
    string? Description,
    string? Notes,
    DateTime CreatedAt,
    IReadOnlyList<SettlementCurrencyExchangeItem> CurrencyExchanges
);

public record SettlementCurrencyExchangeItem(
    string CurrencyCode,
    decimal ExchangeRate,
    decimal ConvertedAmount
);
