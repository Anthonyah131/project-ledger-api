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
        UpdatedAt = entity.ExpUpdatedAt
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
        ExpConvertedAmount = request.OriginalAmount * request.ExchangeRate,
        ExpTitle = request.Title,
        ExpDescription = request.Description,
        ExpExpenseDate = request.ExpenseDate,
        ExpReceiptNumber = request.ReceiptNumber,
        ExpNotes = request.Notes,
        ExpIsTemplate = request.IsTemplate,
        ExpAltCurrency = request.AltCurrency,
        ExpAltExchangeRate = request.AltExchangeRate,
        ExpAltAmount = request.AltCurrency is not null && request.AltExchangeRate.HasValue
            ? request.OriginalAmount * request.AltExchangeRate.Value
            : null,
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
        entity.ExpConvertedAmount = request.OriginalAmount * request.ExchangeRate;
        entity.ExpTitle = request.Title;
        entity.ExpDescription = request.Description;
        entity.ExpExpenseDate = request.ExpenseDate;
        entity.ExpReceiptNumber = request.ReceiptNumber;
        entity.ExpNotes = request.Notes;
        entity.ExpAltCurrency = request.AltCurrency;
        entity.ExpAltExchangeRate = request.AltExchangeRate;
        entity.ExpAltAmount = request.AltCurrency is not null && request.AltExchangeRate.HasValue
            ? request.OriginalAmount * request.AltExchangeRate.Value
            : null;
        entity.ExpUpdatedAt = DateTime.UtcNow;
    }

    // ── Collection helpers ──────────────────────────────────

    public static IEnumerable<ExpenseResponse> ToResponse(this IEnumerable<Expense> entities)
        => entities.Select(e => e.ToResponse());
}
