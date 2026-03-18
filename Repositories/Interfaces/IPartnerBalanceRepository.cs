using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

public interface IPartnerBalanceRepository
{
    Task<PartnerBalanceSummary> GetBalancesAsync(Guid projectId, CancellationToken ct = default);

    Task<PartnerHistoryData> GetPartnerHistoryAsync(
        Guid projectId, Guid partnerId,
        int transactionsSkip, int transactionsTake,
        CancellationToken ct = default);
}

// ── Internal data structures (not DTOs) ──────────────────

public record PartnerBalanceSummary(
    IReadOnlyList<PartnerBalanceData> Individuals,
    IReadOnlyList<PairwiseBalanceData> Pairwise,
    IReadOnlyList<MissingExchangeWarning> Warnings
);

public record PartnerBalanceData(
    Guid PartnerId,
    string PartnerName,
    decimal PaidPhysically,
    decimal OthersOweHimExpenses,
    decimal HeOwesOthersExpenses,
    decimal OthersOweHimIncomes,
    decimal HeOwesOthersIncomes,
    decimal SettlementsReceived,
    decimal SettlementsPaid,
    // Per-currency breakdown of othersOweHim and heOwesOthers (excludes settlements)
    IReadOnlyList<CurrencyTotalData> CurrencyTotals
);

// NetBalance > 0 → A owes B | NetBalance < 0 → B owes A
public record PairwiseBalanceData(
    Guid PartnerAId,
    string PartnerAName,
    Guid PartnerBId,
    string PartnerBName,
    decimal AOwesB,
    decimal BOwesA,
    decimal SettlementsAToB,
    decimal SettlementsBToA,
    decimal NetBalance,
    IReadOnlyList<PairwiseCurrencyData> CurrencyTotals
);

// Per-currency total for an individual partner balance (includes settlements)
public record CurrencyTotalData(
    string CurrencyCode,
    decimal OthersOweHim,
    decimal HeOwesOthers,
    decimal SettlementsPaid,
    decimal SettlementsReceived
);

// Per-currency total for a pairwise balance (includes settlements)
public record PairwiseCurrencyData(
    string CurrencyCode,
    decimal AOwesB,
    decimal BOwesA,
    decimal SettlementsAToB,
    decimal SettlementsBToA
);

// Warning: a split has no currency exchanges while others in the project do
public record MissingExchangeWarning(
    string TransactionType,     // "expense" | "income"
    Guid TransactionId,
    string Title,
    DateOnly Date,
    decimal ConvertedAmount     // full transaction amount in project base currency
);

public record PartnerHistoryData(
    Guid PartnerId,
    string PartnerName,
    IReadOnlyList<PartnerTransactionData> Transactions,
    int TransactionsTotalCount,
    IReadOnlyList<PartnerSettlementData> Settlements
);

public record PartnerTransactionData(
    string Type,
    Guid TransactionId,
    string Title,
    DateOnly Date,
    decimal SplitAmountConverted,
    string SplitType,
    decimal SplitValue,
    string? PayingPartnerName,
    IReadOnlyList<SplitCurrencyExchangeData> CurrencyExchanges
);

public record SplitCurrencyExchangeData(
    string CurrencyCode,
    decimal ExchangeRate,
    decimal ConvertedAmount
);

public record PartnerSettlementData(
    string Type,
    Guid Id,
    DateOnly Date,
    decimal Amount,
    string Currency,
    string? ToPartnerName,
    string? FromPartnerName
);
