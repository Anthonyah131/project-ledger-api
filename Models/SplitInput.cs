namespace ProjectLedger.API.Models;

/// <summary>
/// Datos de un split explícito enviado desde el frontend.
/// Usado como parámetro de servicio; no es una entidad persistida.
/// </summary>
public record SplitInput(
    Guid PartnerId,
    string SplitType,
    decimal SplitValue,
    decimal ResolvedAmount,
    IReadOnlyList<SplitCurrencyExchangeInput>? CurrencyExchanges = null);

/// <summary>
/// Equivalencia en moneda alternativa para un split. Calculada por el frontend.
/// </summary>
public record SplitCurrencyExchangeInput(string CurrencyCode, decimal ExchangeRate, decimal ConvertedAmount);

/// <summary>
/// Conversión a moneda alternativa de una transacción (gasto/ingreso).
/// Usado como parámetro de servicio para bulk create; no es una entidad persistida directamente.
/// </summary>
public record TransactionExchangeInput(string CurrencyCode, decimal ExchangeRate, decimal ConvertedAmount);
