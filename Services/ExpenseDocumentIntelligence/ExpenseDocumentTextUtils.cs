using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ProjectLedger.API.Services;

/// <summary>
/// Helper class for text normalization and tokenization used in expense document processing.
/// </summary>
internal static class ExpenseDocumentTextUtils
{
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var ch in decomposed)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category != UnicodeCategory.NonSpacingMark)
                builder.Append(ch);
        }

        var normalized = builder.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        normalized = Regex.Replace(normalized, "[^a-z0-9]+", " ");
        return normalized.Trim();
    }

    public static HashSet<string> Tokenize(string? value)
    {
        var normalized = Normalize(value);
        if (string.IsNullOrWhiteSpace(normalized))
            return [];

        return normalized
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => t.Length > 1)
            .ToHashSet(StringComparer.Ordinal);
    }

    public static bool IsGeneralCategory(string categoryName)
    {
        var normalized = Normalize(categoryName);
        return normalized is "general" or "otros" or "misc" or "varios";
    }

    public static string? LastDigits(string? raw, int count)
    {
        if (string.IsNullOrWhiteSpace(raw) || count <= 0)
            return null;

        var digits = new string(raw.Where(char.IsDigit).ToArray());
        if (digits.Length == 0)
            return null;

        return digits.Length <= count ? digits : digits[^count..];
    }

    public static string NormalizeWhitespace(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return Regex.Replace(value, "\\s+", " ").Trim();
    }

    public static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
            return value;

        return value[..maxLength].TrimEnd();
    }
}
