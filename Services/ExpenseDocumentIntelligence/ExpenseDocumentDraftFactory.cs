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
            return ExpenseDocumentTextUtils.Truncate($"{movementTitle} for {primaryItemDescription} at {merchantName}", 255);

        if (!string.IsNullOrWhiteSpace(merchantName))
            return ExpenseDocumentTextUtils.Truncate($"{movementTitle} at {merchantName}", 255);

        if (!string.IsNullOrWhiteSpace(primaryItemDescription))
            return ExpenseDocumentTextUtils.Truncate($"{movementTitle} for {primaryItemDescription}", 255);

        var prettyReceiptType = HumanizeReceiptType(receiptType);
        if (!string.IsNullOrWhiteSpace(prettyReceiptType))
            return documentKind == "invoice"
                ? $"{movementTitle} ({prettyReceiptType}) from invoice"
                : $"{movementTitle} ({prettyReceiptType}) from receipt";

        return documentKind == "invoice"
            ? $"{movementNoun} from invoice"
            : $"{movementNoun} from receipt";
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
        var docTypeLabel = documentKind == "invoice" ? "invoice" : "receipt";
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(merchantName))
            parts.Add($"Merchant: {merchantName}");

        if (!string.IsNullOrWhiteSpace(primaryItemDescription))
            parts.Add($"Item: {primaryItemDescription}");

        var prettyReceiptType = HumanizeReceiptType(receiptType);
        if (!string.IsNullOrWhiteSpace(prettyReceiptType))
            parts.Add($"Type: {prettyReceiptType}");

        if (!string.IsNullOrWhiteSpace(detectedPaymentMethod))
            parts.Add($"Payment detected: {detectedPaymentMethod}");

        if (parts.Count == 0)
            return $"{movementNoun} detected from {docTypeLabel}.";

        var description = $"{movementNoun} detected from {docTypeLabel}. {string.Join(". ", parts)}.";
        return ExpenseDocumentTextUtils.Truncate(description, 1000);
    }

    private static string ResolveMovementNoun(string? transactionKind)
    {
        var normalized = ExpenseDocumentTextUtils.Normalize(transactionKind);
        return normalized switch
        {
            "income" or "ingreso" => "income",
            _ => "expense"
        };
    }

    private static string ResolveMovementTitle(string? transactionKind)
    {
        var normalized = ExpenseDocumentTextUtils.Normalize(transactionKind);
        return normalized switch
        {
            "income" or "ingreso" => "Income",
            _ => "Expense"
        };
    }

    private static string? HumanizeReceiptType(string? receiptType)
    {
        var normalizedType = ExpenseDocumentTextUtils.Normalize(receiptType);
        return normalizedType switch
        {
            "retailmeal" => "Retail/Food",
            "creditcard" => "Card",
            "hotel" => "Hotel",
            "transportationparking" => "Parking",
            "fuelenergygas" => "Fuel",
            _ => string.IsNullOrWhiteSpace(receiptType)
                ? null
                : ExpenseDocumentTextUtils.NormalizeWhitespace(receiptType).Replace(".", "/", StringComparison.Ordinal)
        };
    }
}
