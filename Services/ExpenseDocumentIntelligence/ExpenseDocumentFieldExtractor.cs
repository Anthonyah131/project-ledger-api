using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ProjectLedger.API.Services;

/// <summary>
/// Helper class for extracting fields from expense document analysis results.
/// </summary>
internal static partial class ExpenseDocumentFieldExtractor
{
    private static readonly Regex[] ReceiptNumberRegexes =
    [
        new(@"(?im)\b(?:receipt|recibo|ticket|folio|invoice|factura|comprobante)\s*(?:no\.?|num(?:ber)?|nro\.?|n(?:u|um)?\.?|#|:)?\s*([a-z0-9][a-z0-9\-\/.]{3,40})\b", RegexOptions.Compiled),
        new(@"(?im)\b(?:doc(?:ument)?|refer(?:ence)?|ref)\s*(?:no\.?|num(?:ber)?|nro\.?|#|:)?\s*([a-z0-9][a-z0-9\-\/.]{3,40})\b", RegexOptions.Compiled),
        new(@"(?im)\b(?:n\s*[o0]|nro|num(?:ero)?|numero)\s*(?:[:#-])\s*([a-z0-9][a-z0-9\-\/.]{3,40})\b", RegexOptions.Compiled)
    ];

    public static string? ExtractReceiptNumber(JsonElement fields, string fullContent, string documentKind)
    {
        var direct = documentKind == "invoice"
            ? GetStringField(fields, "InvoiceId", "DocumentNumber", "PurchaseOrder", "ReceiptNumber")
            : GetStringField(fields, "ReceiptNumber", "DocumentNumber", "ReceiptId", "TicketNumber", "FolioNumber", "InvoiceId");

        var cleanedDirect = NormalizeDocumentReference(direct);
        if (!string.IsNullOrWhiteSpace(cleanedDirect))
            return cleanedDirect;

        return ExtractReceiptNumberFromContent(fullContent);
    }

    public static string? GetReceiptType(JsonElement fields)
        => GetStringField(fields, "ReceiptType", "DocumentType");

    public static string? GetPrimaryItemDescription(JsonElement fields)
    {
        if (!TryGetField(fields, "Items", out var itemsField)
            || !itemsField.TryGetProperty("valueArray", out var items)
            || items.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object
                || !item.TryGetProperty("valueObject", out var valueObject)
                || valueObject.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var description = GetStringField(valueObject, "Description", "Name", "ProductCode");
            var normalizedDescription = ExpenseDocumentTextUtils.NormalizeWhitespace(description);
            if (!string.IsNullOrWhiteSpace(normalizedDescription))
                return normalizedDescription;
        }

        return null;
    }

    public static string? GetStringField(JsonElement fields, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryGetField(fields, name, out var field))
                continue;

            var valueString = GetOptionalString(field, "valueString");
            if (!string.IsNullOrWhiteSpace(valueString))
                return valueString.Trim();

            var content = GetOptionalString(field, "content");
            if (!string.IsNullOrWhiteSpace(content))
                return content.Trim();

            if (field.TryGetProperty("valueSelectionGroup", out var selection)
                && selection.ValueKind == JsonValueKind.Array
                && selection.GetArrayLength() > 0)
            {
                var first = selection[0];
                var selected = GetOptionalString(first, "valueString");
                if (!string.IsNullOrWhiteSpace(selected))
                    return selected.Trim();
            }
        }

        return null;
    }

    public static DateOnly? GetDateField(JsonElement fields, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryGetField(fields, name, out var field))
                continue;

            var valueDate = GetOptionalString(field, "valueDate");
            if (DateOnly.TryParse(valueDate, out var date))
                return date;

            var valueDateTime = GetOptionalString(field, "valueDateTime");
            if (DateTime.TryParse(valueDateTime, out var dateTime))
                return DateOnly.FromDateTime(dateTime);

            var content = GetOptionalString(field, "content");
            if (DateOnly.TryParse(content, out date))
                return date;

            if (DateTime.TryParse(content, out dateTime))
                return DateOnly.FromDateTime(dateTime);
        }

        return null;
    }

    public static (decimal? Amount, string? CurrencyCode) GetCurrencyField(JsonElement fields, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryGetField(fields, name, out var field))
                continue;

            if (field.TryGetProperty("valueCurrency", out var valueCurrency)
                && valueCurrency.ValueKind == JsonValueKind.Object)
            {
                decimal? amount = null;
                if (valueCurrency.TryGetProperty("amount", out var amountEl)
                    && amountEl.ValueKind == JsonValueKind.Number
                    && amountEl.TryGetDecimal(out var amountParsed))
                {
                    amount = amountParsed;
                }

                var currencyCode = GetOptionalString(valueCurrency, "currencyCode");
                if (amount.HasValue)
                    return (amount, currencyCode?.ToUpperInvariant());
            }

            if (field.TryGetProperty("valueNumber", out var valueNumber)
                && valueNumber.ValueKind == JsonValueKind.Number
                && valueNumber.TryGetDecimal(out var numberAmount))
            {
                return (numberAmount, null);
            }

            var content = GetOptionalString(field, "content");
            if (TryParseDecimal(content, out var contentAmount))
                return (contentAmount, null);
        }

        return (null, null);
    }

    public static bool TryGetField(JsonElement fields, string name, out JsonElement field)
    {
        field = default;
        return fields.ValueKind == JsonValueKind.Object
            && fields.TryGetProperty(name, out field)
            && field.ValueKind == JsonValueKind.Object;
    }

    public static string? GetOptionalString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
            return null;

        return property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    /// <summary>Extracts a fallback receipt number using RegEx when the Azure invoice model misses it.</summary>
    private static string? ExtractReceiptNumberFromContent(string fullContent)
    {
        if (string.IsNullOrWhiteSpace(fullContent))
            return null;

        foreach (var regex in ReceiptNumberRegexes)
        {
            var matches = regex.Matches(fullContent);
            foreach (Match match in matches)
            {
                if (!match.Success || match.Groups.Count < 2)
                    continue;

                var candidate = NormalizeDocumentReference(match.Groups[1].Value);
                if (IsLikelyReceiptNumber(candidate))
                    return candidate;
            }
        }

        return null;
    }

    /// <summary>Cleans layout noise and punctuation from document reference values.</summary>
    private static string? NormalizeDocumentReference(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = ExpenseDocumentTextUtils.NormalizeWhitespace(value)
            .Trim('"', '\'', '.', ',', ':', ';', '-', '_', '#', '(', ')', '[', ']');

        return string.IsNullOrWhiteSpace(normalized)
            ? null
            : normalized.ToUpperInvariant();
    }

    /// <summary>Checks whether a string acts like a valid receipt number.</summary>
    private static bool IsLikelyReceiptNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var candidate = value.Trim();
        if (candidate.Length is < 4 or > 40)
            return false;

        if (!candidate.Any(char.IsDigit))
            return false;

        if (decimal.TryParse(candidate.Replace(",", ".", StringComparison.Ordinal), NumberStyles.Number, CultureInfo.InvariantCulture, out _))
            return false;

        if (DateOnly.TryParse(candidate, out _) || DateTime.TryParse(candidate, out _))
            return false;

        return true;
    }

    /// <summary>Safe numerical parser converting strings to decimals.</summary>
    private static bool TryParseDecimal(string? value, out decimal result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var clean = value.Replace(",", ".", StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);

        return decimal.TryParse(clean, NumberStyles.Number, CultureInfo.InvariantCulture, out result);
    }
}
