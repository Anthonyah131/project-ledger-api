using ProjectLedger.API.DTOs.Expense;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Services;

/// <summary>
/// Helper class for suggesting expense categories and payment methods.
/// </summary>
internal static class ExpenseDocumentSuggestionEngine
{
    public static SuggestedExpenseCategoryResponse? SuggestCategory(
        IReadOnlyCollection<Category> categories,
        ExpenseDocumentDraftResponse draft,
        string fullContent)
    {
        if (categories.Count == 0)
            return null;

        var rawText = string.Join(' ',
            draft.Title,
            draft.Description,
            draft.DetectedMerchantName,
            fullContent);

        var normalizedText = ExpenseDocumentTextUtils.Normalize(rawText);
        var textTokens = ExpenseDocumentTextUtils.Tokenize(rawText);

        decimal bestScore = 0m;
        Category? bestCategory = null;
        var reason = string.Empty;

        foreach (var category in categories)
        {
            var categoryName = ExpenseDocumentTextUtils.Normalize(category.CatName);
            var score = 0m;
            var currentReason = "Low lexical match";

            if (!string.IsNullOrWhiteSpace(categoryName)
                && normalizedText.Contains(categoryName, StringComparison.Ordinal))
            {
                score = 0.92m;
                currentReason = "Category name appears in extracted text";
            }
            else
            {
                var catTokens = ExpenseDocumentTextUtils.Tokenize(category.CatName);
                if (catTokens.Count > 0)
                {
                    var overlap = catTokens.Count(t => textTokens.Contains(t));
                    if (overlap > 0)
                    {
                        score = Math.Round((decimal)overlap / catTokens.Count * 0.80m, 2);
                        currentReason = "Category tokens overlap with extracted text";
                    }
                }
            }

            if (score < 0.40m && ExpenseDocumentTextUtils.IsGeneralCategory(category.CatName))
            {
                score = 0.40m;
                currentReason = "Fallback to project's general category";
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestCategory = category;
                reason = currentReason;
            }
        }

        if (bestCategory is null || bestScore < 0.40m)
            return null;

        return new SuggestedExpenseCategoryResponse
        {
            CategoryId = bestCategory.CatId,
            Name = bestCategory.CatName,
            Confidence = bestScore,
            Reason = reason
        };
    }

    public static SuggestedExpensePaymentMethodResponse? SuggestPaymentMethod(
        IReadOnlyCollection<ProjectPaymentMethod> linkedPaymentMethods,
        string? detectedPaymentMethod,
        string fullContent,
        string? detectedCurrency)
    {
        if (linkedPaymentMethods.Count == 0)
            return null;

        if (linkedPaymentMethods.Count == 1 && string.IsNullOrWhiteSpace(detectedPaymentMethod))
        {
            var only = linkedPaymentMethods.First();
            return new SuggestedExpensePaymentMethodResponse
            {
                PaymentMethodId = only.PpmPaymentMethodId,
                Name = only.PaymentMethod?.PmtName ?? string.Empty,
                Type = only.PaymentMethod?.PmtType ?? string.Empty,
                Confidence = 0.45m,
                Reason = "Only one payment method is linked to this project"
            };
        }

        var detectedText = ExpenseDocumentTextUtils.Normalize($"{detectedPaymentMethod} {fullContent}");
        decimal bestScore = 0m;
        ProjectPaymentMethod? best = null;
        var reason = string.Empty;

        foreach (var link in linkedPaymentMethods)
        {
            var pm = link.PaymentMethod;
            if (pm is null)
                continue;

            var score = 0m;
            var currentReason = "No clear signal";

            var pmName = ExpenseDocumentTextUtils.Normalize(pm.PmtName);
            if (!string.IsNullOrWhiteSpace(pmName) && detectedText.Contains(pmName, StringComparison.Ordinal))
            {
                score += 0.55m;
                currentReason = "Payment method name appears in extracted text";
            }

            var typeTokens = GetPaymentTypeTokens(pm.PmtType);
            if (typeTokens.Any(t => detectedText.Contains(t, StringComparison.Ordinal)))
            {
                score += 0.35m;
                currentReason = "Detected payment type matches linked method type";
            }

            var bankName = ExpenseDocumentTextUtils.Normalize(pm.PmtBankName);
            if (!string.IsNullOrWhiteSpace(bankName) && detectedText.Contains(bankName, StringComparison.Ordinal))
            {
                score += 0.20m;
                currentReason = "Detected text contains bank name";
            }

            var accountSuffix = ExpenseDocumentTextUtils.LastDigits(pm.PmtAccountNumber, 4);
            if (!string.IsNullOrWhiteSpace(accountSuffix)
                && detectedText.Contains(accountSuffix, StringComparison.Ordinal))
            {
                score += 0.20m;
                currentReason = "Detected text contains account suffix";
            }

            if (!string.IsNullOrWhiteSpace(detectedCurrency)
                && string.Equals(pm.PmtCurrency, detectedCurrency, StringComparison.OrdinalIgnoreCase))
            {
                score += 0.10m;
            }

            score = Math.Round(Math.Clamp(score, 0m, 0.99m), 2);
            if (score > bestScore)
            {
                bestScore = score;
                best = link;
                reason = currentReason;
            }
        }

        if (best is null || bestScore < 0.40m)
            return null;

        return new SuggestedExpensePaymentMethodResponse
        {
            PaymentMethodId = best.PpmPaymentMethodId,
            Name = best.PaymentMethod?.PmtName ?? string.Empty,
            Type = best.PaymentMethod?.PmtType ?? string.Empty,
            Confidence = bestScore,
            Reason = reason
        };
    }

    private static IEnumerable<string> GetPaymentTypeTokens(string? paymentType)
    {
        var normalizedType = ExpenseDocumentTextUtils.Normalize(paymentType);

        return normalizedType switch
        {
            "cash" => ["cash", "efectivo", "contado"],
            "card" => ["card", "tarjeta", "credito", "debito", "visa", "mastercard", "amex", "contactless", "pos"],
            "bank" => ["bank", "banco", "transfer", "transferencia", "wire", "ach", "iban", "spei"],
            "wallet" => ["wallet", "billetera", "digital", "paypal", "apple pay", "google pay"],
            _ => []
        };
    }
}
