using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using ProjectLedger.API.DTOs.Expense;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Services;

/// <summary>
/// Integracion con Azure Document Intelligence para extraer un borrador de gasto
/// desde imagen/PDF de recibo o factura.
/// </summary>
public class ExpenseDocumentIntelligenceService : IExpenseDocumentIntelligenceService
{
    private static readonly HashSet<string> SupportedContentTypes =
    [
        "application/pdf",
        "image/jpeg",
        "image/jpg",
        "image/png",
        "image/webp",
        "image/bmp",
        "image/tiff",
        "image/heif"
    ];

    private readonly HttpClient _httpClient;
    private readonly AzureDocumentIntelligenceSettings _settings;
    private readonly ICategoryService _categoryService;
    private readonly IProjectPaymentMethodService _projectPaymentMethodService;
    private readonly IProjectService _projectService;
    private readonly ILogger<ExpenseDocumentIntelligenceService> _logger;

    public ExpenseDocumentIntelligenceService(
        HttpClient httpClient,
        IOptions<AzureDocumentIntelligenceSettings> settings,
        ICategoryService categoryService,
        IProjectPaymentMethodService projectPaymentMethodService,
        IProjectService projectService,
        ILogger<ExpenseDocumentIntelligenceService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _categoryService = categoryService;
        _projectPaymentMethodService = projectPaymentMethodService;
        _projectService = projectService;
        _logger = logger;
    }

    public async Task<ExtractExpenseFromDocumentResponse> ExtractDraftAsync(
        Guid projectId,
        IFormFile file,
        string documentKind,
        CancellationToken ct = default)
    {
        EnsureConfigured();
        ValidateFile(file);

        var project = await _projectService.GetByIdAsync(projectId, ct)
            ?? throw new KeyNotFoundException($"Project '{projectId}' not found.");

        var normalizedKind = string.Equals(documentKind, "invoice", StringComparison.OrdinalIgnoreCase)
            ? "invoice"
            : "receipt";
        var modelId = normalizedKind == "invoice"
            ? "prebuilt-invoice"
            : _settings.DefaultModelId;

        var operationLocation = await StartAnalyzeAsync(file, modelId, ct);
        var resultJson = await PollAnalyzeResultAsync(operationLocation, ct);

        using var resultDoc = JsonDocument.Parse(resultJson);
        var root = resultDoc.RootElement;

        if (!root.TryGetProperty("analyzeResult", out var analyzeResult))
            throw new InvalidOperationException("Azure Document Intelligence did not return analyzeResult.");

        if (!analyzeResult.TryGetProperty("documents", out var documents)
            || documents.ValueKind != JsonValueKind.Array
            || documents.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("No document data could be extracted from the file.");
        }

        var firstDocument = documents[0];
        if (!firstDocument.TryGetProperty("fields", out var fields)
            || fields.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Document fields were not found in the analysis result.");
        }

        var fullContent = GetOptionalString(analyzeResult, "content") ?? string.Empty;

        var merchantName = GetStringField(fields, "MerchantName", "VendorName", "SupplierName", "SellerName");
        var receiptNumber = GetStringField(fields, "ReceiptNumber", "InvoiceId", "DocumentNumber");
        var detectedPaymentMethod = GetStringField(fields, "PaymentMethod", "PaymentDetails", "PaymentTerms");
        var extractedDate = GetDateField(fields, "TransactionDate", "InvoiceDate", "IssueDate", "BillDate");
        var currencyResult = GetCurrencyField(fields, "Total", "TotalAmount", "InvoiceTotal", "AmountDue", "TotalPrice");

        var categories = (await _categoryService.GetByProjectIdAsync(projectId, ct)).ToList();
        var linkedPaymentMethods = (await _projectPaymentMethodService.GetByProjectIdAsync(projectId, ct)).ToList();

        var draft = BuildDraft(
            merchantName,
            receiptNumber,
            detectedPaymentMethod,
            extractedDate,
            currencyResult,
            project.PrjCurrencyCode,
            normalizedKind);

        var categorySuggestion = SuggestCategory(categories, draft, fullContent);
        var paymentMethodSuggestion = SuggestPaymentMethod(
            linkedPaymentMethods,
            detectedPaymentMethod,
            fullContent,
            draft.OriginalCurrency);

        draft.CategoryId = categorySuggestion?.CategoryId;
        draft.PaymentMethodId = paymentMethodSuggestion?.PaymentMethodId;
        draft.ObligationId = null;
        draft.ObligationEquivalentAmount = null;
        draft.Notes = null;
        draft.IsTemplate = false;
        draft.CurrencyExchanges = null;

        var warnings = BuildWarnings(draft, categorySuggestion, paymentMethodSuggestion);

        return new ExtractExpenseFromDocumentResponse
        {
            Provider = "azure-document-intelligence",
            DocumentKind = normalizedKind,
            ModelId = modelId,
            Draft = draft,
            SuggestedCategory = categorySuggestion,
            SuggestedPaymentMethod = paymentMethodSuggestion,
            AvailableCategories = categories
                .Select(c => new ExpenseCategoryOptionResponse
                {
                    CategoryId = c.CatId,
                    Name = c.CatName,
                    IsDefault = c.CatIsDefault
                })
                .ToList(),
            AvailablePaymentMethods = linkedPaymentMethods
                .Select(pm => new ExpensePaymentMethodOptionResponse
                {
                    PaymentMethodId = pm.PpmPaymentMethodId,
                    Name = pm.PaymentMethod?.PmtName ?? string.Empty,
                    Type = pm.PaymentMethod?.PmtType ?? string.Empty,
                    Currency = pm.PaymentMethod?.PmtCurrency ?? string.Empty,
                    BankName = pm.PaymentMethod?.PmtBankName,
                    AccountNumber = pm.PaymentMethod?.PmtAccountNumber
                })
                .ToList(),
            Warnings = warnings
        };
    }

    private void EnsureConfigured()
    {
        if (!_settings.Enabled)
            throw new InvalidOperationException("Azure Document Intelligence is disabled by configuration.");

        if (string.IsNullOrWhiteSpace(_settings.Endpoint))
            throw new InvalidOperationException("Azure Document Intelligence endpoint is missing.");

        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
            throw new InvalidOperationException("Azure Document Intelligence API key is missing.");
    }

    private void ValidateFile(IFormFile file)
    {
        if (file is null || file.Length == 0)
            throw new InvalidOperationException("A non-empty file is required.");

        var maxBytes = Math.Clamp(_settings.MaxFileSizeMb, 1, 50) * 1024L * 1024L;
        if (file.Length > maxBytes)
            throw new InvalidOperationException($"File is too large. Max allowed size is {_settings.MaxFileSizeMb} MB.");

        if (!string.IsNullOrWhiteSpace(file.ContentType)
            && !SupportedContentTypes.Contains(file.ContentType.ToLowerInvariant()))
        {
            throw new InvalidOperationException(
                "Unsupported content type. Allowed: PDF, JPG, PNG, WEBP, BMP, TIFF, HEIF.");
        }
    }

    private async Task<string> StartAnalyzeAsync(IFormFile file, string modelId, CancellationToken ct)
    {
        var requestUri = $"documentintelligence/documentModels/{Uri.EscapeDataString(modelId)}:analyze?api-version=2024-11-30";

        await using var fileStream = file.OpenReadStream();
        using var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(ResolveContentType(file));

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = streamContent
        };
        request.Headers.Add("Ocp-Apim-Subscription-Key", _settings.ApiKey);

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("Azure Document Intelligence start analyze failed: {StatusCode} - {Body}", (int)response.StatusCode, body);
            throw new InvalidOperationException("Unable to analyze document with Azure Document Intelligence.");
        }

        if (!response.Headers.TryGetValues("Operation-Location", out var values))
            throw new InvalidOperationException("Azure Document Intelligence did not return an operation location.");

        var operationLocation = values.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(operationLocation))
            throw new InvalidOperationException("Azure Document Intelligence operation location is empty.");

        return operationLocation;
    }

    private async Task<string> PollAnalyzeResultAsync(string operationLocation, CancellationToken ct)
    {
        var pollInterval = Math.Clamp(_settings.PollingIntervalMilliseconds, 300, 5000);
        var maxAttempts = Math.Clamp(_settings.MaxPollingAttempts, 5, 120);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, operationLocation);
            request.Headers.Add("Ocp-Apim-Subscription-Key", _settings.ApiKey);

            using var response = await _httpClient.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Azure Document Intelligence poll failed: {StatusCode} - {Body}", (int)response.StatusCode, body);
                throw new InvalidOperationException("Azure Document Intelligence polling failed.");
            }

            using var pollDoc = JsonDocument.Parse(body);
            var status = GetOptionalString(pollDoc.RootElement, "status")?.ToLowerInvariant();

            if (status == "succeeded")
                return body;

            if (status == "failed" || status == "partiallysucceeded")
                throw new InvalidOperationException("Azure Document Intelligence could not extract data from the document.");

            await Task.Delay(pollInterval, ct);
        }

        throw new InvalidOperationException("Azure Document Intelligence timed out while processing the document.");
    }

    private static string ResolveContentType(IFormFile file)
    {
        if (!string.IsNullOrWhiteSpace(file.ContentType))
            return file.ContentType;

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        return extension switch
        {
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".tif" or ".tiff" => "image/tiff",
            ".heif" => "image/heif",
            _ => "application/octet-stream"
        };
    }

    private static ExpenseDocumentDraftResponse BuildDraft(
        string? merchantName,
        string? receiptNumber,
        string? detectedPaymentMethod,
        DateOnly? extractedDate,
        (decimal? Amount, string? CurrencyCode) currencyResult,
        string projectCurrency,
        string documentKind)
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

        var title = BuildTitle(merchantName, documentKind);
        var description = string.IsNullOrWhiteSpace(merchantName)
            ? $"{documentKind} extracted by AI"
            : $"{documentKind} from {merchantName}";

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
            ReceiptNumber = receiptNumber,
            Notes = null,
            IsTemplate = false,
            CurrencyExchanges = null,
            DetectedMerchantName = merchantName,
            DetectedPaymentMethodText = detectedPaymentMethod
        };
    }

    private static string BuildTitle(string? merchantName, string documentKind)
    {
        if (!string.IsNullOrWhiteSpace(merchantName))
            return $"Compra - {merchantName}";

        return documentKind == "invoice" ? "Gasto desde factura" : "Gasto desde recibo";
    }

    private static SuggestedExpenseCategoryResponse? SuggestCategory(
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
        var normalizedText = Normalize(rawText);

        decimal bestScore = 0m;
        Category? bestCategory = null;
        var reason = string.Empty;

        foreach (var category in categories)
        {
            var categoryName = Normalize(category.CatName);
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
                var catTokens = Tokenize(category.CatName);
                if (catTokens.Count > 0)
                {
                    var textTokens = Tokenize(rawText);
                    var overlap = catTokens.Count(t => textTokens.Contains(t));
                    if (overlap > 0)
                    {
                        score = Math.Round((decimal)overlap / catTokens.Count * 0.80m, 2);
                        currentReason = "Category tokens overlap with extracted text";
                    }
                }
            }

            if (score < 0.40m && IsGeneralCategory(category.CatName))
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

    private static SuggestedExpensePaymentMethodResponse? SuggestPaymentMethod(
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

        var detectedText = Normalize($"{detectedPaymentMethod} {fullContent}");
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

            var pmName = Normalize(pm.PmtName);
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

            var bankName = Normalize(pm.PmtBankName);
            if (!string.IsNullOrWhiteSpace(bankName) && detectedText.Contains(bankName, StringComparison.Ordinal))
            {
                score += 0.20m;
                currentReason = "Detected text contains bank name";
            }

            var accountSuffix = LastDigits(pm.PmtAccountNumber, 4);
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

    private static List<string> BuildWarnings(
        ExpenseDocumentDraftResponse draft,
        SuggestedExpenseCategoryResponse? categorySuggestion,
        SuggestedExpensePaymentMethodResponse? paymentMethodSuggestion)
    {
        var warnings = new List<string>();

        if (draft.OriginalAmount is null)
            warnings.Add("AI could not detect total amount. User must complete it manually.");

        if (string.IsNullOrWhiteSpace(draft.OriginalCurrency))
            warnings.Add("AI could not detect currency. User must complete it manually.");

        if (draft.ConvertedAmount is null)
            warnings.Add("ConvertedAmount was not auto-computed. Frontend should calculate it before creating the expense.");

        if (categorySuggestion is null)
            warnings.Add("No category suggestion could be inferred with enough confidence.");

        if (paymentMethodSuggestion is null)
            warnings.Add("No payment method suggestion could be inferred with enough confidence.");

        return warnings;
    }

    private static string? GetStringField(JsonElement fields, params string[] names)
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

    private static DateOnly? GetDateField(JsonElement fields, params string[] names)
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

    private static (decimal? Amount, string? CurrencyCode) GetCurrencyField(JsonElement fields, params string[] names)
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

    private static bool TryGetField(JsonElement fields, string name, out JsonElement field)
    {
        field = default;
        return fields.ValueKind == JsonValueKind.Object
            && fields.TryGetProperty(name, out field)
            && field.ValueKind == JsonValueKind.Object;
    }

    private static string? GetOptionalString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
            return null;

        return property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static bool TryParseDecimal(string? value, out decimal result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var clean = value.Replace(",", ".", StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);

        return decimal.TryParse(clean, NumberStyles.Number, CultureInfo.InvariantCulture, out result);
    }

    private static string Normalize(string? value)
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

    private static HashSet<string> Tokenize(string? value)
    {
        var normalized = Normalize(value);
        if (string.IsNullOrWhiteSpace(normalized))
            return [];

        return normalized
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => t.Length > 1)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static bool IsGeneralCategory(string categoryName)
    {
        var normalized = Normalize(categoryName);
        return normalized is "general" or "otros" or "misc" or "varios";
    }

    private static IEnumerable<string> GetPaymentTypeTokens(string? paymentType)
    {
        var normalizedType = Normalize(paymentType);
        return normalizedType switch
        {
            "cash" => ["cash", "efectivo", "contado"],
            "card" => ["card", "tarjeta", "credito", "debito", "visa", "mastercard", "amex"],
            "bank" => ["bank", "banco", "transfer", "transferencia", "wire", "ach", "iban"],
            _ => []
        };
    }

    private static string? LastDigits(string? raw, int count)
    {
        if (string.IsNullOrWhiteSpace(raw) || count <= 0)
            return null;

        var digits = new string(raw.Where(char.IsDigit).ToArray());
        if (digits.Length == 0)
            return null;

        return digits.Length <= count ? digits : digits[^count..];
    }
}
