using ProjectLedger.API.DTOs.Common;
using ProjectLedger.API.DTOs.Income;
using ProjectLedger.API.DTOs.Split;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Extensions.Mappings;

public static class IncomeMappingExtensions
{
    // ── Entity → Response ───────────────────────────────────

    public static IncomeResponse ToResponse(this Income entity) => new()
    {
        Id = entity.IncId,
        ProjectId = entity.IncProjectId,
        ProjectName = entity.Project?.PrjName,
        ProjectCurrency = entity.Project?.PrjCurrencyCode,
        CategoryId = entity.IncCategoryId,
        CategoryName = entity.Category?.CatName ?? string.Empty,
        PaymentMethodId = entity.IncPaymentMethodId,
        CreatedByUserId = entity.IncCreatedByUserId,
        OriginalAmount = entity.IncOriginalAmount,
        OriginalCurrency = entity.IncOriginalCurrency,
        ExchangeRate = entity.IncExchangeRate,
        ConvertedAmount = entity.IncConvertedAmount,
        AccountAmount = entity.IncAccountAmount,
        AccountCurrency = entity.IncAccountCurrency,
        Title = entity.IncTitle,
        Description = entity.IncDescription,
        IncomeDate = entity.IncIncomeDate,
        ReceiptNumber = entity.IncReceiptNumber,
        Notes = entity.IncNotes,
        IsActive = entity.IncIsActive,
        CreatedAt = entity.IncCreatedAt,
        UpdatedAt = entity.IncUpdatedAt,
        IsDeleted = entity.IncIsDeleted,
        DeletedAt = entity.IncDeletedAt,
        DeletedByUserId = entity.IncDeletedByUserId,
        CurrencyExchanges = entity.CurrencyExchanges?.Select(e => e.ToResponse()).ToList(),
        HasSplits = entity.Splits?.Count > 0,
        Splits = entity.Splits?.Count > 0
            ? entity.Splits.Select(s => new SplitResponseDto
            {
                PartnerId = s.InsPartnerId,
                PartnerName = s.Partner?.PtrName ?? string.Empty,
                SplitType = s.InsSplitType,
                SplitValue = s.InsSplitValue,
                ResolvedAmount = s.InsResolvedAmount,
                CurrencyExchanges = s.CurrencyExchanges?.Count > 0
                    ? s.CurrencyExchanges.Select(e => new CurrencyExchangeResponse
                    {
                        Id = e.SceId,
                        CurrencyCode = e.SceCurrencyCode,
                        ExchangeRate = e.SceExchangeRate,
                        ConvertedAmount = e.SceConvertedAmount
                    }).ToList()
                    : null
            }).ToList()
            : null
    };

    // ── Request → Entity ────────────────────────────────────

    public static Income ToEntity(this CreateIncomeRequest request, Guid projectId, Guid userId) => new()
    {
        IncId = Guid.NewGuid(),
        IncProjectId = projectId,
        IncCreatedByUserId = userId,
        IncCategoryId = request.CategoryId,
        IncPaymentMethodId = request.PaymentMethodId,
        IncOriginalAmount = request.OriginalAmount,
        IncOriginalCurrency = request.OriginalCurrency,
        IncExchangeRate = request.ExchangeRate,
        IncConvertedAmount = request.ConvertedAmount,
        IncAccountAmount = request.AccountAmount,
        IncTitle = request.Title,
        IncDescription = request.Description,
        IncIncomeDate = request.IncomeDate,
        IncReceiptNumber = request.ReceiptNumber,
        IncNotes = request.Notes,
        IncIsActive = request.IsActive,
        IncCreatedAt = DateTime.UtcNow,
        IncUpdatedAt = DateTime.UtcNow
    };

    // ── Apply update from DTO ──────────────────────────────

    public static void ApplyUpdate(this Income entity, UpdateIncomeRequest request)
    {
        entity.IncCategoryId = request.CategoryId;
        entity.IncPaymentMethodId = request.PaymentMethodId;
        entity.IncOriginalAmount = request.OriginalAmount;
        entity.IncOriginalCurrency = request.OriginalCurrency;
        entity.IncExchangeRate = request.ExchangeRate;
        entity.IncConvertedAmount = request.ConvertedAmount;
        entity.IncAccountAmount = request.AccountAmount;
        entity.IncTitle = request.Title;
        entity.IncDescription = request.Description;
        entity.IncIncomeDate = request.IncomeDate;
        entity.IncReceiptNumber = request.ReceiptNumber;
        entity.IncNotes = request.Notes;
        if (request.IsActive.HasValue)
            entity.IncIsActive = request.IsActive.Value;
        entity.IncUpdatedAt = DateTime.UtcNow;
    }

    // ── Collection helper ───────────────────────────────────

    public static IEnumerable<IncomeResponse> ToResponse(this IEnumerable<Income> entities)
        => entities.Select(e => e.ToResponse());
}
