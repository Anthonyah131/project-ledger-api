using ProjectLedger.API.Models;
using ProjectLedger.API.Repositories;

namespace ProjectLedger.API.Services;

/// <summary>
/// Project alternative currencies management.
/// Validates CanUseMultiCurrency permission and plan limits.
/// </summary>
public class ProjectAlternativeCurrencyService : IProjectAlternativeCurrencyService
{
    private readonly IProjectAlternativeCurrencyRepository _pacRepo;
    private readonly IProjectRepository _projectRepo;
    private readonly ICurrencyRepository _currencyRepo;
    private readonly IPlanAuthorizationService _planAuth;
    private readonly IAuditLogService _auditLog;
    private readonly ITransactionReferenceGuardService _transactionReferenceGuard;

    public ProjectAlternativeCurrencyService(
        IProjectAlternativeCurrencyRepository pacRepo,
        IProjectRepository projectRepo,
        ICurrencyRepository currencyRepo,
        IPlanAuthorizationService planAuth,
        IAuditLogService auditLog,
        ITransactionReferenceGuardService transactionReferenceGuard)
    {
        _pacRepo = pacRepo;
        _projectRepo = projectRepo;
        _currencyRepo = currencyRepo;
        _planAuth = planAuth;
        _auditLog = auditLog;
        _transactionReferenceGuard = transactionReferenceGuard;
    }

    public async Task<IEnumerable<ProjectAlternativeCurrency>> GetByProjectIdAsync(
        Guid projectId, CancellationToken ct = default)
        => await _pacRepo.GetByProjectIdAsync(projectId, ct);

    public async Task<ProjectAlternativeCurrency> AddAsync(
        Guid projectId, string currencyCode, CancellationToken ct = default)
    {
        var project = await _projectRepo.GetByIdAsync(projectId, ct)
            ?? throw new KeyNotFoundException("ProjectNotFound");

        if (project.PrjIsDeleted)
            throw new KeyNotFoundException("ProjectNotFound");

        // Validate multi-currency permission
        await _planAuth.ValidatePermissionAsync(
            project.PrjOwnerUserId, PlanPermission.CanUseMultiCurrency, ct);

        // Validate that the currency exists and is active
        var currency = await _currencyRepo.GetByCodeAsync(currencyCode, ct)
            ?? throw new KeyNotFoundException("CurrencyNotFound");

        if (!currency.CurIsActive)
            throw new InvalidOperationException("CurrencyNotActive");

        // Do not allow the alternative currency to be the same as the project's base currency
        if (string.Equals(project.PrjCurrencyCode, currencyCode, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("AlternativeCurrencyCannotBeBaseCurrency");

        // Verify if it already exists
        var existing = await _pacRepo.GetByProjectAndCurrencyAsync(projectId, currencyCode, ct);
        if (existing is not null)
            throw new InvalidOperationException("AlternativeCurrencyAlreadyExists");

        // Validate alternative currencies limit
        var currentCount = await _pacRepo.CountByProjectIdAsync(projectId, ct);
        await _planAuth.ValidateLimitAsync(
            project.PrjOwnerUserId, PlanLimits.MaxAlternativeCurrenciesPerProject, currentCount, ct);

        var pac = new ProjectAlternativeCurrency
        {
            PacId = Guid.NewGuid(),
            PacProjectId = projectId,
            PacCurrencyCode = currencyCode,
            PacCreatedAt = DateTime.UtcNow
        };

        await _pacRepo.AddAsync(pac, ct);
        await _pacRepo.SaveChangesAsync(ct);

        await _auditLog.LogAsync("ProjectAlternativeCurrency", pac.PacId, "create",
            project.PrjOwnerUserId,
            newValues: new { pac.PacId, pac.PacProjectId, pac.PacCurrencyCode }, ct: ct);

        // Re-fetch with navigation properties
        return (await _pacRepo.GetByProjectAndCurrencyAsync(projectId, currencyCode, ct))!;
    }

    public async Task RemoveAsync(Guid projectId, string currencyCode, CancellationToken ct = default)
    {
        var pac = await _pacRepo.GetByProjectAndCurrencyAsync(projectId, currencyCode, ct)
            ?? throw new KeyNotFoundException("AlternativeCurrencyNotFound");

        await _transactionReferenceGuard.EnsureAlternativeCurrencyCanBeRemovedAsync(
            projectId,
            pac.PacCurrencyCode,
            ct);

        var project = await _projectRepo.GetByIdAsync(projectId, ct);

        _pacRepo.Remove(pac);
        await _pacRepo.SaveChangesAsync(ct);

        await _auditLog.LogAsync("ProjectAlternativeCurrency", pac.PacId, "delete",
            project?.PrjOwnerUserId ?? Guid.Empty,
            oldValues: new { pac.PacCurrencyCode, pac.PacProjectId }, ct: ct);
    }
}
