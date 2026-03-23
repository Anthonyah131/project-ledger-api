using ProjectLedger.API.DTOs.Common;
using ProjectLedger.API.DTOs.Partner;
using ProjectLedger.API.Models;
using ProjectLedger.API.Repositories;

namespace ProjectLedger.API.Services;

public class PartnerSettlementService : IPartnerSettlementService
{
    private readonly IPartnerSettlementRepository _settlementRepo;
    private readonly IProjectPartnerRepository _projectPartnerRepo;

    public PartnerSettlementService(
        IPartnerSettlementRepository settlementRepo,
        IProjectPartnerRepository projectPartnerRepo)
    {
        _settlementRepo = settlementRepo;
        _projectPartnerRepo = projectPartnerRepo;
    }

    public async Task<(IReadOnlyList<PartnerSettlement> Items, int TotalCount)> GetPagedByProjectIdAsync(
        Guid projectId, int skip, int take, CancellationToken ct = default)
        => await _settlementRepo.GetPagedByProjectIdAsync(projectId, skip, take, ct);

    public async Task<PartnerSettlement> CreateAsync(
        PartnerSettlement settlement,
        IReadOnlyList<CurrencyExchangeRequest>? currencyExchanges = null,
        CancellationToken ct = default)
    {
        _ = await _projectPartnerRepo.GetActiveAsync(settlement.PstProjectId, settlement.PstFromPartnerId, ct)
            ?? throw new KeyNotFoundException("PartnerNotAssignedToProject");

        _ = await _projectPartnerRepo.GetActiveAsync(settlement.PstProjectId, settlement.PstToPartnerId, ct)
            ?? throw new KeyNotFoundException("PartnerNotAssignedToProject");

        settlement.PstConvertedAmount = Math.Round(settlement.PstAmount * settlement.PstExchangeRate, 2, MidpointRounding.AwayFromZero);
        settlement.PstCreatedAt = DateTime.UtcNow;
        settlement.PstUpdatedAt = DateTime.UtcNow;

        await _settlementRepo.ExecuteInTransactionAsync(async (ct) =>
        {
            await _settlementRepo.AddAsync(settlement, ct);
            await _settlementRepo.SaveChangesAsync(ct);

            if (currencyExchanges?.Count > 0)
                await _settlementRepo.SaveCurrencyExchangesAsync(settlement.PstId, currencyExchanges, ct);
        }, ct);

        return (await _settlementRepo.GetActiveByIdAsync(settlement.PstId, settlement.PstProjectId, ct))!;
    }

    public async Task<PartnerSettlement> UpdateAsync(
        Guid settlementId, Guid projectId,
        UpdateSettlementRequest request,
        CancellationToken ct = default)
    {
        var settlement = await _settlementRepo.GetActiveByIdAsync(settlementId, projectId, ct)
            ?? throw new KeyNotFoundException("SettlementNotFound");

        if (request.Amount.HasValue)
            settlement.PstAmount = request.Amount.Value;

        if (request.Currency is not null)
            settlement.PstCurrency = request.Currency;

        if (request.ExchangeRate.HasValue)
            settlement.PstExchangeRate = request.ExchangeRate.Value;

        if (request.SettlementDate.HasValue)
            settlement.PstSettlementDate = request.SettlementDate.Value;

        // Description and Notes allow explicit null to clear the value
        if (request.Description is not null || request.Notes is not null)
        {
            if (request.Description is not null)
                settlement.PstDescription = request.Description == "" ? null : request.Description;

            if (request.Notes is not null)
                settlement.PstNotes = request.Notes == "" ? null : request.Notes;
        }

        // Recalculate converted amount whenever amount or exchange rate changes
        settlement.PstConvertedAmount = Math.Round(settlement.PstAmount * settlement.PstExchangeRate, 2, MidpointRounding.AwayFromZero);
        settlement.PstUpdatedAt = DateTime.UtcNow;

        await _settlementRepo.ExecuteInTransactionAsync(async (ct) =>
        {
            await _settlementRepo.SaveChangesAsync(ct);

            if (request.CurrencyExchanges is not null)
                await _settlementRepo.ReplaceCurrencyExchangesAsync(settlement.PstId, request.CurrencyExchanges, ct);
        }, ct);

        return (await _settlementRepo.GetActiveByIdAsync(settlement.PstId, settlement.PstProjectId, ct))!;
    }

    public async Task SoftDeleteAsync(Guid settlementId, Guid projectId, Guid deletedByUserId, CancellationToken ct = default)
    {
        var settlement = await _settlementRepo.GetActiveByIdAsync(settlementId, projectId, ct)
            ?? throw new KeyNotFoundException("SettlementNotFound");

        settlement.PstIsDeleted = true;
        settlement.PstDeletedAt = DateTime.UtcNow;
        settlement.PstDeletedByUserId = deletedByUserId;
        settlement.PstUpdatedAt = DateTime.UtcNow;

        await _settlementRepo.SaveChangesAsync(ct);
    }
}
