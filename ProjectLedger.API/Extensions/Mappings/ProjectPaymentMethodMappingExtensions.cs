using ProjectLedger.API.Models;
using ProjectLedger.API.DTOs.Project;

namespace ProjectLedger.API.Extensions.Mappings;

public static class ProjectPaymentMethodMappingExtensions
{
    public static ProjectPaymentMethodResponse ToResponse(this ProjectPaymentMethod entity) => new()
    {
        Id = entity.PpmId,
        PaymentMethodId = entity.PpmPaymentMethodId,
        PaymentMethodName = entity.PaymentMethod?.PmtName ?? string.Empty,
        Type = entity.PaymentMethod?.PmtType ?? string.Empty,
        Currency = entity.PaymentMethod?.PmtCurrency ?? string.Empty,
        BankName = entity.PaymentMethod?.PmtBankName,
        AccountNumber = entity.PaymentMethod?.PmtAccountNumber,
        OwnerUserName = entity.PaymentMethod?.OwnerUser?.UsrFullName ?? string.Empty,
        LinkedAt = entity.PpmCreatedAt
    };

    public static IEnumerable<ProjectPaymentMethodResponse> ToResponse(
        this IEnumerable<ProjectPaymentMethod> entities)
        => entities.Select(e => e.ToResponse());
}
