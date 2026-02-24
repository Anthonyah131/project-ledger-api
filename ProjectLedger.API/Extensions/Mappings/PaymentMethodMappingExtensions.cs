using ProjectLedger.API.Models;
using ProjectLedger.API.DTOs.PaymentMethod;

namespace ProjectLedger.API.Extensions.Mappings;

public static class PaymentMethodMappingExtensions
{
    // ── Entity → Response ───────────────────────────────────

    public static PaymentMethodResponse ToResponse(this PaymentMethod entity) => new()
    {
        Id = entity.PmtId,
        Name = entity.PmtName,
        Type = entity.PmtType,
        Currency = entity.PmtCurrency,
        BankName = entity.PmtBankName,
        AccountNumber = entity.PmtAccountNumber,
        Description = entity.PmtDescription,
        CreatedAt = entity.PmtCreatedAt,
        UpdatedAt = entity.PmtUpdatedAt
    };

    // ── Request → Entity ────────────────────────────────────

    public static PaymentMethod ToEntity(this CreatePaymentMethodRequest request, Guid ownerUserId) => new()
    {
        PmtId = Guid.NewGuid(),
        PmtOwnerUserId = ownerUserId,
        PmtName = request.Name,
        PmtType = request.Type,
        PmtCurrency = request.Currency,
        PmtBankName = request.BankName,
        PmtAccountNumber = request.AccountNumber,
        PmtDescription = request.Description,
        PmtCreatedAt = DateTime.UtcNow,
        PmtUpdatedAt = DateTime.UtcNow
    };

    // ── Apply update from DTO ──────────────────────────────

    public static void ApplyUpdate(this PaymentMethod entity, UpdatePaymentMethodRequest request)
    {
        entity.PmtName = request.Name;
        entity.PmtType = request.Type;
        entity.PmtBankName = request.BankName;
        entity.PmtAccountNumber = request.AccountNumber;
        entity.PmtDescription = request.Description;
        entity.PmtUpdatedAt = DateTime.UtcNow;
    }

    // ── Collection helpers ──────────────────────────────────

    public static IEnumerable<PaymentMethodResponse> ToResponse(this IEnumerable<PaymentMethod> entities)
        => entities.Select(e => e.ToResponse());
}
