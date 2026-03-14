using System.Net.Http.Headers;
using System.Text.Json;
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
        string transactionKind = "expense",
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

        var operationLocation = await StartAnalyzeAsync(file, modelId, normalizedKind, ct);
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

        var fullContent = ExpenseDocumentFieldExtractor.GetOptionalString(analyzeResult, "content") ?? string.Empty;

        var merchantName = ExpenseDocumentFieldExtractor.GetStringField(fields, "MerchantName", "VendorName", "SupplierName", "SellerName");
        var receiptNumber = ExpenseDocumentFieldExtractor.ExtractReceiptNumber(fields, fullContent, normalizedKind);
        var detectedPaymentMethod = ExpenseDocumentFieldExtractor.GetStringField(fields, "PaymentMethod", "PaymentDetails", "PaymentTerms");
        var extractedDate = ExpenseDocumentFieldExtractor.GetDateField(fields, "TransactionDate", "InvoiceDate", "IssueDate", "BillDate");
        var currencyResult = ExpenseDocumentFieldExtractor.GetCurrencyField(fields, "Total", "TotalAmount", "InvoiceTotal", "AmountDue", "TotalPrice");
        var receiptType = ExpenseDocumentFieldExtractor.GetReceiptType(fields);
        var primaryItemDescription = ExpenseDocumentFieldExtractor.GetPrimaryItemDescription(fields);

        var categories = (await _categoryService.GetByProjectIdAsync(projectId, ct)).ToList();
        var linkedPaymentMethods = (await _projectPaymentMethodService.GetByProjectIdAsync(projectId, ct)).ToList();

        var draft = ExpenseDocumentDraftFactory.BuildDraft(
            merchantName,
            receiptNumber,
            detectedPaymentMethod,
            extractedDate,
            currencyResult,
            project.PrjCurrencyCode,
            normalizedKind,
            transactionKind,
            receiptType,
            primaryItemDescription);

        var categorySuggestion = ExpenseDocumentSuggestionEngine.SuggestCategory(categories, draft, fullContent);
        var paymentMethodSuggestion = ExpenseDocumentSuggestionEngine.SuggestPaymentMethod(
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

    private async Task<string> StartAnalyzeAsync(IFormFile file, string modelId, string documentKind, CancellationToken ct)
    {
        var requestUri = BuildAnalyzeRequestUri(modelId, documentKind, includeQueryFields: true);
        var fallbackUri = BuildAnalyzeRequestUri(modelId, documentKind, includeQueryFields: false);

        var response = await TryStartAnalyzeAsync(file, requestUri, ct);

        if (!response.IsSuccessStatusCode && !string.Equals(requestUri, fallbackUri, StringComparison.Ordinal))
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogInformation(
                "Retrying Azure Document Intelligence analyze without queryFields due to initial failure: {StatusCode} - {Body}",
                (int)response.StatusCode,
                body);

            response.Dispose();
            response = await TryStartAnalyzeAsync(file, fallbackUri, ct);
        }

        using (response)
        {
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
    }

    private async Task<HttpResponseMessage> TryStartAnalyzeAsync(IFormFile file, string requestUri, CancellationToken ct)
    {
        await using var fileStream = file.OpenReadStream();
        using var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(ResolveContentType(file));

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = streamContent
        };
        request.Headers.Add("Ocp-Apim-Subscription-Key", _settings.ApiKey);

        return await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
    }

    private static string BuildAnalyzeRequestUri(string modelId, string documentKind, bool includeQueryFields)
    {
        var baseUri = $"documentintelligence/documentModels/{Uri.EscapeDataString(modelId)}:analyze?api-version=2024-11-30";
        if (!includeQueryFields || !modelId.StartsWith("prebuilt-", StringComparison.OrdinalIgnoreCase))
            return baseUri;

        var queryFields = documentKind == "invoice"
            ? new[] { "InvoiceId", "DocumentNumber", "PurchaseOrder", "ReceiptNumber" }
            : new[] { "ReceiptNumber", "ReceiptId", "TicketNumber", "FolioNumber", "DocumentNumber", "InvoiceId" };

        var encodedQueryFields = string.Join(",", queryFields.Select(Uri.EscapeDataString));
        return $"{baseUri}&features=queryFields&queryFields={encodedQueryFields}";
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
            var status = ExpenseDocumentFieldExtractor.GetOptionalString(pollDoc.RootElement, "status")?.ToLowerInvariant();

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

        if (string.IsNullOrWhiteSpace(draft.ReceiptNumber))
            warnings.Add("AI could not detect a reliable receipt or invoice reference number.");

        if (draft.ConvertedAmount is null)
            warnings.Add("ConvertedAmount was not auto-computed. Frontend should calculate it before creating the expense.");

        if (categorySuggestion is null)
            warnings.Add("No category suggestion could be inferred with enough confidence.");

        if (paymentMethodSuggestion is null)
            warnings.Add("No payment method suggestion could be inferred with enough confidence.");

        return warnings;
    }
}
