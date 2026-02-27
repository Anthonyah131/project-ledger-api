using ProjectLedger.API.Models;
using ProjectLedger.API.DTOs.Expense;

namespace ProjectLedger.API.Extensions.Mappings;

public static class ExpenseMappingExtensions
{
    // ── Entity → Response ───────────────────────────────────

    public static ExpenseResponse ToResponse(this Expense entity) => new()
    {
        Id = entity.ExpId,
        ProjectId = entity.ExpProjectId,
        CategoryId = entity.ExpCategoryId,
        CategoryName = entity.Category?.CatName ?? string.Empty,
        PaymentMethodId = entity.ExpPaymentMethodId,
        CreatedByUserId = entity.ExpCreatedByUserId,
        ObligationId = entity.ExpObligationId,
        OriginalAmount = entity.ExpOriginalAmount,
        OriginalCurrency = entity.ExpOriginalCurrency,
        ExchangeRate = entity.ExpExchangeRate,
        ConvertedAmount = entity.ExpConvertedAmount,
        Title = entity.ExpTitle,
        Description = entity.ExpDescription,
        ExpenseDate = entity.ExpExpenseDate,
        ReceiptNumber = entity.ExpReceiptNumber,
        Notes = entity.ExpNotes,
        IsTemplate = entity.ExpIsTemplate,
        AltCurrency = entity.ExpAltCurrency,
        AltExchangeRate = entity.ExpAltExchangeRate,
        AltAmount = entity.ExpAltAmount,
        CreatedAt = entity.ExpCreatedAt,
        UpdatedAt = entity.ExpUpdatedAt,
        IsDeleted = entity.ExpIsDeleted,
        DeletedAt = entity.ExpDeletedAt,
        DeletedByUserId = entity.ExpDeletedByUserId
    };

    // ── Request → Entity ────────────────────────────────────

    public static Expense ToEntity(this CreateExpenseRequest request, Guid projectId, Guid userId) => new()
    {
        ExpId = Guid.NewGuid(),
        ExpProjectId = projectId,
        ExpCreatedByUserId = userId,
        ExpCategoryId = request.CategoryId,
        ExpPaymentMethodId = request.PaymentMethodId,
        ExpObligationId = request.ObligationId,
        ExpOriginalAmount = request.OriginalAmount,
        ExpOriginalCurrency = request.OriginalCurrency,
        ExpExchangeRate = request.ExchangeRate,
        ExpConvertedAmount = request.ConvertedAmount,
        ExpTitle = request.Title,
        ExpDescription = request.Description,
        ExpExpenseDate = request.ExpenseDate,
        ExpReceiptNumber = request.ReceiptNumber,
        ExpNotes = request.Notes,
        ExpIsTemplate = request.IsTemplate,
        ExpAltCurrency = request.AltCurrency,
        ExpAltExchangeRate = request.AltExchangeRate,
        ExpAltAmount = request.AltAmount,
        ExpCreatedAt = DateTime.UtcNow,
        ExpUpdatedAt = DateTime.UtcNow
    };

    // ── Apply update from DTO ──────────────────────────────

    public static void ApplyUpdate(this Expense entity, UpdateExpenseRequest request)
    {
        entity.ExpCategoryId = request.CategoryId;
        entity.ExpPaymentMethodId = request.PaymentMethodId;
        entity.ExpOriginalAmount = request.OriginalAmount;
        entity.ExpOriginalCurrency = request.OriginalCurrency;
        entity.ExpExchangeRate = request.ExchangeRate;
        entity.ExpConvertedAmount = request.ConvertedAmount;
        entity.ExpTitle = request.Title;
        entity.ExpDescription = request.Description;
        entity.ExpExpenseDate = request.ExpenseDate;
        entity.ExpReceiptNumber = request.ReceiptNumber;
        entity.ExpNotes = request.Notes;
        entity.ExpAltCurrency = request.AltCurrency;
        entity.ExpAltExchangeRate = request.AltExchangeRate;
        entity.ExpAltAmount = request.AltAmount;
        entity.ExpUpdatedAt = DateTime.UtcNow;
    }

    // ── Create from template ────────────────────────────────

    /// <summary>
    /// Crea un gasto real (IsTemplate = false) a partir de una plantilla.
    /// Reutiliza categoría, método de pago, moneda, descripción, exchange rate y alt currency.
    /// </summary>
    public static Expense ToEntityFromTemplate(
        this Expense template,
        Guid projectId,
        Guid userId,
        CreateFromTemplateRequest request) => new()
    {
        ExpId = Guid.NewGuid(),
        ExpProjectId = projectId,
        ExpCreatedByUserId = userId,
        ExpCategoryId = template.ExpCategoryId,
        ExpPaymentMethodId = template.ExpPaymentMethodId,
        ExpObligationId = request.ObligationId,
        ExpOriginalAmount = request.OriginalAmount ?? template.ExpOriginalAmount,
        ExpOriginalCurrency = template.ExpOriginalCurrency,
        ExpExchangeRate = template.ExpExchangeRate,
        ExpConvertedAmount = request.ConvertedAmount ?? template.ExpConvertedAmount,
        ExpTitle = template.ExpTitle,
        ExpDescription = template.ExpDescription,
        ExpExpenseDate = request.ExpenseDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
        ExpReceiptNumber = null,
        ExpNotes = request.Notes,
        ExpIsTemplate = false,
        ExpAltCurrency = template.ExpAltCurrency,
        ExpAltExchangeRate = template.ExpAltExchangeRate,
        ExpAltAmount = request.AltAmount ?? template.ExpAltAmount,
        ExpCreatedAt = DateTime.UtcNow,
        ExpUpdatedAt = DateTime.UtcNow
    };

    // ── Collection helpers ──────────────────────────────────

    public static IEnumerable<ExpenseResponse> ToResponse(this IEnumerable<Expense> entities)
        => entities.Select(e => e.ToResponse());
}
