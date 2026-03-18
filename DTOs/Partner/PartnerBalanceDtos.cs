using ProjectLedger.API.DTOs.Common;

namespace ProjectLedger.API.DTOs.Partner;

// ── GET /projects/:id/partners/balance ────────────────────

public record PartnerBalanceResponse(
    Guid ProjectId,
    string Currency,
    IReadOnlyList<PartnerBalanceItem> Partners,
    /// <summary>
    /// Balance entre cada par de socios.
    /// NetBalance positivo = PartnerA le debe a PartnerB.
    /// NetBalance negativo = PartnerB le debe a PartnerA.
    /// </summary>
    IReadOnlyList<PairwiseBalanceItem> PairwiseBalances,
    /// <summary>
    /// Splits que no tienen currency exchanges configurados cuando otros en el proyecto sí los tienen.
    /// El usuario debe editar o recrear esas transacciones para ver montos en monedas alternativas.
    /// </summary>
    IReadOnlyList<MissingCurrencyExchangeWarning> Warnings
);

// NetBalance > 0 → PartnerA owes PartnerB | NetBalance < 0 → PartnerB owes PartnerA
public record PairwiseBalanceItem(
    Guid PartnerAId,
    string PartnerAName,
    Guid PartnerBId,
    string PartnerBName,
    decimal AOwesB,              // gross: A owes B from transactions (before settlements)
    decimal BOwesA,              // gross: B owes A from transactions (before settlements)
    decimal SettlementsAToB,     // settlements A has already paid to B
    decimal SettlementsBToA,     // settlements B has already paid to A
    decimal NetBalance,          // positive = A owes B net; negative = B owes A net
    IReadOnlyList<PairwiseCurrencyTotal> CurrencyTotals
);

public record PairwiseCurrencyTotal(
    string CurrencyCode,
    decimal AOwesB,
    decimal BOwesA,
    decimal SettlementsAToB,
    decimal SettlementsBToA,
    decimal NetBalance          // (AOwesB - SettlementsAToB) - (BOwesA - SettlementsBToA)
);

// ── GET /projects/:id/partners/settlement-suggestions ─────

public record SettlementSuggestionsResponse(
    Guid ProjectId,
    string Currency,
    IReadOnlyList<SettlementSuggestionItem> Suggestions,
    string Note
);

public record SettlementSuggestionItem(
    Guid FromPartnerId,
    string FromPartnerName,
    Guid ToPartnerId,
    string ToPartnerName,
    decimal Amount,
    string Currency
);

public record PartnerBalanceItem(
    Guid PartnerId,
    string PartnerName,
    decimal PaidPhysically,
    decimal OthersOweHim,
    decimal HeOwesOthers,
    decimal SettlementsReceived,
    decimal SettlementsPaid,
    decimal NetBalance,
    /// <summary>
    /// Mismos valores de OthersOweHim / HeOwesOthers expresados en otras monedas.
    /// NetBalance por moneda = OthersOweHim - HeOwesOthers (sin incluir liquidaciones).
    /// </summary>
    IReadOnlyList<PartnerCurrencyTotal> CurrencyTotals
);

public record PartnerCurrencyTotal(
    string CurrencyCode,
    decimal OthersOweHim,
    decimal HeOwesOthers,
    decimal SettlementsPaid,
    decimal SettlementsReceived,
    decimal NetBalance          // OthersOweHim - HeOwesOthers + SettlementsPaid - SettlementsReceived
);

// ── GET /projects/:id/partners/:partnerId/history ─────────
// Transactions are paginated; settlements always returned in full.

public record PartnerHistoryResponse(
    Guid PartnerId,
    string PartnerName,
    PagedResponse<PartnerTransactionItem> Transactions,
    IReadOnlyList<PartnerSettlementHistoryItem> Settlements
);

public record PartnerTransactionItem(
    string Type,                // "expense" | "income"
    Guid TransactionId,
    string Title,
    DateOnly Date,
    decimal SplitAmount,        // resolved in project base currency
    string SplitType,           // "percentage" | "fixed"
    decimal SplitValue,
    string? PayingPartner,      // name of partner who paid (via their PM); null if no owner partner
    IReadOnlyList<SplitCurrencyExchangeItem> CurrencyExchanges
);

public record SplitCurrencyExchangeItem(
    string CurrencyCode,
    decimal ExchangeRate,
    decimal ConvertedAmount     // split amount expressed in this currency
);

public record PartnerSettlementHistoryItem(
    string Type,                // "settlement_paid" | "settlement_received"
    Guid Id,
    DateOnly Date,
    decimal Amount,
    string Currency,
    string? ToPartner,          // set when type == "settlement_paid"
    string? FromPartner         // set when type == "settlement_received"
);

// ── Warnings ───────────────────────────────────────────────

/// <summary>
/// Split sin currency exchanges cuando otros en el proyecto sí los tienen.
/// El usuario debe editar la transacción (o eliminarla y recrearla) para que aparezca en monedas alternativas.
/// </summary>
public record MissingCurrencyExchangeWarning(
    string TransactionType,     // "expense" | "income"
    Guid TransactionId,
    string Title,
    DateOnly Date,
    decimal ConvertedAmount     // monto completo de la transacción en moneda base
);
