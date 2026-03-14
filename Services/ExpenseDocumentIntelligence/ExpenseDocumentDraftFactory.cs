using ProjectLedger.API.DTOs.Expense;

namespace ProjectLedger.API.Services;

internal static class ExpenseDocumentDraftFactory
{
    public static ExpenseDocumentDraftResponse BuildDraft(
        string? merchantName,
        string? receiptNumber,
        string? detectedPaymentMethod,
        DateOnly? extractedDate,
        (decimal? Amount, string? CurrencyCode) currencyResult,
        string projectCurrency,
        string documentKind,
        string transactionKind,
        string? receiptType,
        string? primaryItemDescription)
    {
        var hasAmount = currencyResult.Amount.HasValue;
        var currencyCode = string.IsNullOrWhiteSpace(currencyResult.CurrencyCode)
            ? projectCurrency
            : currencyResult.CurrencyCode;

        decimal? exchangeRate = null;
        decimal? convertedAmount = null;
        if (hasAmount && string.Equals(currencyCode, projectCurrency, StringComparison.OrdinalIgnoreCase))
        {
            exchangeRate = 1m;
            convertedAmount = currencyResult.Amount;
        }

        var normalizedMerchant = ExpenseDocumentTextUtils.NormalizeWhitespace(merchantName);
        var normalizedReference = ExpenseDocumentTextUtils.NormalizeWhitespace(receiptNumber);
        var normalizedPaymentMethod = ExpenseDocumentTextUtils.NormalizeWhitespace(detectedPaymentMethod);
        var normalizedItem = ExpenseDocumentTextUtils.NormalizeWhitespace(primaryItemDescription);

        var title = BuildTitle(normalizedMerchant, documentKind, transactionKind, normalizedItem, receiptType);
        var description = BuildDescription(
            transactionKind,
            normalizedMerchant,
            normalizedPaymentMethod,
            normalizedItem,
            receiptType,
            documentKind);

        return new ExpenseDocumentDraftResponse
        {
            CategoryId = null,
            PaymentMethodId = null,
            ObligationId = null,
            ObligationEquivalentAmount = null,
            Title = title,
            Description = description,
            OriginalAmount = currencyResult.Amount,
            OriginalCurrency = currencyCode?.ToUpperInvariant(),
            ExchangeRate = exchangeRate,
            ConvertedAmount = convertedAmount,
            ExpenseDate = extractedDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
            ReceiptNumber = normalizedReference,
            Notes = null,
            IsTemplate = false,
            CurrencyExchanges = null,
            DetectedMerchantName = normalizedMerchant,
            DetectedPaymentMethodText = normalizedPaymentMethod
        };
    }

    private static string BuildTitle(
        string merchantName,
        string documentKind,
        string transactionKind,
        string primaryItemDescription,
        string? receiptType)
    {
        var movementNoun = ResolveMovementNoun(transactionKind);
        var movementTitle = ResolveMovementTitle(transactionKind);

        if (!string.IsNullOrWhiteSpace(merchantName) && !string.IsNullOrWhiteSpace(primaryItemDescription))
            return ExpenseDocumentTextUtils.Truncate($"{movementTitle} por {primaryItemDescription} en {merchantName}", 255);

        if (!string.IsNullOrWhiteSpace(merchantName))
            return ExpenseDocumentTextUtils.Truncate($"{movementTitle} en {merchantName}", 255);

        if (!string.IsNullOrWhiteSpace(primaryItemDescription))
            return ExpenseDocumentTextUtils.Truncate($"{movementTitle} por {primaryItemDescription}", 255);

        var prettyReceiptType = HumanizeReceiptType(receiptType);
        if (!string.IsNullOrWhiteSpace(prettyReceiptType))
            return documentKind == "invoice"
                ? $"{movementTitle} ({prettyReceiptType}) desde factura"
                : $"{movementTitle} ({prettyReceiptType}) desde recibo";

        return documentKind == "invoice"
            ? $"{movementNoun} desde factura"
            : $"{movementNoun} desde recibo";
    }

    private static string BuildDescription(
        string transactionKind,
        string merchantName,
        string detectedPaymentMethod,
        string primaryItemDescription,
        string? receiptType,
        string documentKind)
    {
        var movementNoun = ResolveMovementNoun(transactionKind);
        var docTypeLabel = documentKind == "invoice" ? "factura" : "recibo";
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(merchantName))
            parts.Add($"Comercio: {merchantName}");

        if (!string.IsNullOrWhiteSpace(primaryItemDescription))
            parts.Add($"Concepto: {primaryItemDescription}");

        var prettyReceiptType = HumanizeReceiptType(receiptType);
        if (!string.IsNullOrWhiteSpace(prettyReceiptType))
            parts.Add($"Tipo: {prettyReceiptType}");

        if (!string.IsNullOrWhiteSpace(detectedPaymentMethod))
            parts.Add($"Pago detectado: {detectedPaymentMethod}");

        if (parts.Count == 0)
            return $"{movementNoun} detectado desde {docTypeLabel}.";

        var description = $"{movementNoun} detectado desde {docTypeLabel}. {string.Join(". ", parts)}.";
        return ExpenseDocumentTextUtils.Truncate(description, 1000);
    }

    private static string ResolveMovementNoun(string? transactionKind)
    {
        var normalized = ExpenseDocumentTextUtils.Normalize(transactionKind);
        return normalized switch
        {
            "income" or "ingreso" => "ingreso",
            _ => "gasto"
        };
    }

    private static string ResolveMovementTitle(string? transactionKind)
    {
        var normalized = ExpenseDocumentTextUtils.Normalize(transactionKind);
        return normalized switch
        {
            "income" or "ingreso" => "Ingreso",
            _ => "Gasto"
        };
    }

    private static string? HumanizeReceiptType(string? receiptType)
    {
        var normalizedType = ExpenseDocumentTextUtils.Normalize(receiptType);
        return normalizedType switch
        {
            "retailmeal" => "Retail/Comida",
            "creditcard" => "Tarjeta",
            "hotel" => "Hotel",
            "transportationparking" => "Parqueo",
            "fuelenergygas" => "Combustible",
            _ => string.IsNullOrWhiteSpace(receiptType)
                ? null
                : ExpenseDocumentTextUtils.NormalizeWhitespace(receiptType).Replace(".", "/", StringComparison.Ordinal)
        };
    }
}
