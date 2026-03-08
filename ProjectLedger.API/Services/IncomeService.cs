using ProjectLedger.API.Models;
using ProjectLedger.API.Repositories;

namespace ProjectLedger.API.Services;

/// <summary>
/// Servicio de ingresos. CRUD con soft delete.
/// Valida límite de ingresos por mes según el plan del owner del proyecto.
/// </summary>
public class IncomeService : IIncomeService
{
    private readonly IIncomeRepository _incomeRepo;
    private readonly IProjectRepository _projectRepo;
    private readonly IProjectPaymentMethodRepository _ppmRepo;
    private readonly IPlanAuthorizationService _planAuth;
    private readonly IAuditLogService _auditLog;

    public IncomeService(
        IIncomeRepository incomeRepo,
        IProjectRepository projectRepo,
        IProjectPaymentMethodRepository ppmRepo,
        IPlanAuthorizationService planAuth,
        IAuditLogService auditLog)
    {
        _incomeRepo = incomeRepo;
        _projectRepo = projectRepo;
        _ppmRepo = ppmRepo;
        _planAuth = planAuth;
        _auditLog = auditLog;
    }

    public async Task<Income?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var income = await _incomeRepo.GetByIdAsync(id, ct);
        return income is { IncIsDeleted: false } ? income : null;
    }

    public async Task<IEnumerable<Income>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default)
        => await _incomeRepo.GetByProjectIdAsync(projectId, ct);

    public async Task<IEnumerable<Income>> GetByProjectIdAsync(Guid projectId, bool includeDeleted, CancellationToken ct = default)
        => await _incomeRepo.GetByProjectIdAsync(projectId, includeDeleted, ct);

    public async Task<(IReadOnlyList<Income> Items, int TotalCount)> GetByProjectIdPagedAsync(
        Guid projectId, bool includeDeleted, int skip, int take, string? sortBy, bool descending, CancellationToken ct = default)
        => await _incomeRepo.GetByProjectIdPagedAsync(projectId, includeDeleted, skip, take, sortBy, descending, ct);

    public async Task<Income> CreateAsync(Income income, CancellationToken ct = default)
    {
        var project = await _projectRepo.GetByIdAsync(income.IncProjectId, ct)
            ?? throw new KeyNotFoundException($"Project '{income.IncProjectId}' not found.");

        // Validar límite de ingresos por mes
        var projectIncomes = await _incomeRepo.GetByProjectIdAsync(income.IncProjectId, ct);
        var thisMonthCount = projectIncomes
            .Count(i => i.IncCreatedAt.Year == DateTime.UtcNow.Year
                     && i.IncCreatedAt.Month == DateTime.UtcNow.Month);

        await _planAuth.ValidateLimitAsync(
            project.PrjOwnerUserId, PlanLimits.MaxIncomesPerMonth, thisMonthCount, ct);

        // Validar que el método de pago está vinculado al proyecto
        var isLinked = await _ppmRepo.IsPaymentMethodLinkedToProjectAsync(
            income.IncProjectId, income.IncPaymentMethodId, ct);

        if (!isLinked)
            throw new InvalidOperationException(
                "The payment method is not linked to this project. " +
                "Link it first via /api/projects/{projectId}/payment-methods.");

        income.IncCreatedAt = DateTime.UtcNow;
        income.IncUpdatedAt = DateTime.UtcNow;

        await _incomeRepo.AddAsync(income, ct);
        await _incomeRepo.SaveChangesAsync(ct);

        await _auditLog.LogAsync("Income", income.IncId, "create", income.IncCreatedByUserId,
            newValues: new { income.IncId, income.IncTitle, income.IncConvertedAmount, income.IncProjectId }, ct: ct);

        return income;
    }

    public async Task UpdateAsync(Income income, CancellationToken ct = default)
    {
        income.IncUpdatedAt = DateTime.UtcNow;
        _incomeRepo.Update(income);
        await _incomeRepo.SaveChangesAsync(ct);
    }

    public async Task SoftDeleteAsync(Guid id, Guid deletedByUserId, CancellationToken ct = default)
    {
        var income = await _incomeRepo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"Income '{id}' not found.");

        if (income.IncIsDeleted)
            throw new KeyNotFoundException($"Income '{id}' not found.");

        income.IncIsDeleted = true;
        income.IncDeletedAt = DateTime.UtcNow;
        income.IncDeletedByUserId = deletedByUserId;
        income.IncUpdatedAt = DateTime.UtcNow;

        _incomeRepo.Update(income);
        await _incomeRepo.SaveChangesAsync(ct);

        await _auditLog.LogAsync("Income", id, "delete", deletedByUserId,
            oldValues: new { income.IncTitle, income.IncConvertedAmount }, ct: ct);
    }
}
