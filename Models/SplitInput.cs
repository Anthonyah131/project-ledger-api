namespace ProjectLedger.API.Models;

/// <summary>
/// Explicit split data sent from the frontend.
/// Used as a service parameter; it is not a persisted entity.
/// </summary>
public record SplitInput(
    Guid PartnerId,
    string SplitType,
    decimal SplitValue,
    decimal ResolvedAmount,
    IReadOnlyList<SplitCurrencyExchangeInput>? CurrencyExchanges = null);

/// <summary>
/// Alternative currency equivalence for a split. Calculated by the frontend.
/// </summary>
public record SplitCurrencyExchangeInput(string CurrencyCode, decimal ExchangeRate, decimal ConvertedAmount);

/// <summary>
/// Conversion to an alternative currency of a transaction (expense/income).
/// Used as a service parameter for bulk create; not a directly persisted entity.
/// </summary>
public record TransactionExchangeInput(string CurrencyCode, decimal ExchangeRate, decimal ConvertedAmount);
