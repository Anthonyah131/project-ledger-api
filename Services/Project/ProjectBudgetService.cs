using ProjectLedger.API.Models;
using ProjectLedger.API.Repositories;

namespace ProjectLedger.API.Services;

/// <summary>
/// Servicio de presupuesto de proyecto. Un solo presupuesto activo por proyecto.
/// Valida permiso del plan (CanSetBudgets) antes de crear/actualizar.
/// </summary>
public class ProjectBudgetService : IProjectBudgetService
{
    private readonly IProjectBudgetRepository _budgetRepo;
    private readonly IProjectRepository _projectRepo;
    private readonly IPlanAuthorizationService _planAuth;

    public ProjectBudgetService(
        IProjectBudgetRepository budgetRepo,
        IProjectRepository projectRepo,
        IPlanAuthorizationService planAuth)
    {
        _budgetRepo = budgetRepo;
        _projectRepo = projectRepo;
        _planAuth = planAuth;
    }

    public async Task<ProjectBudget?> GetActiveByProjectIdAsync(
        Guid projectId, CancellationToken ct = default)
        => await _budgetRepo.GetActiveByProjectIdAsync(projectId, ct);

    public async Task<ProjectBudget> CreateAsync(ProjectBudget budget, CancellationToken ct = default)
    {
        // Validar que el plan del owner permite definir presupuestos
        var project = await _projectRepo.GetByIdAsync(budget.PjbProjectId, ct)
            ?? throw new KeyNotFoundException("ProjectNotFound");

        await _planAuth.ValidatePermissionAsync(
            project.PrjOwnerUserId, PlanPermission.CanSetBudgets, ct);

        // Verificar que no exista un presupuesto activo — solo uno por proyecto
        var existing = await _budgetRepo.GetActiveByProjectIdAsync(budget.PjbProjectId, ct);
        if (existing is not null)
            throw new InvalidOperationException("BudgetAlreadyExists");

        budget.PjbCreatedAt = DateTime.UtcNow;
        budget.PjbUpdatedAt = DateTime.UtcNow;

        await _budgetRepo.AddAsync(budget, ct);
        await _budgetRepo.SaveChangesAsync(ct);

        return budget;
    }

    public async Task UpdateAsync(ProjectBudget budget, CancellationToken ct = default)
    {
        // Validar que el plan del owner permite definir presupuestos
        var project = await _projectRepo.GetByIdAsync(budget.PjbProjectId, ct)
            ?? throw new KeyNotFoundException("ProjectNotFound");

        await _planAuth.ValidatePermissionAsync(
            project.PrjOwnerUserId, PlanPermission.CanSetBudgets, ct);

        budget.PjbUpdatedAt = DateTime.UtcNow;
        _budgetRepo.Update(budget);
        await _budgetRepo.SaveChangesAsync(ct);
    }

    public async Task SoftDeleteAsync(Guid id, Guid deletedByUserId, CancellationToken ct = default)
    {
        var budget = await _budgetRepo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException("BudgetNotFound");

        if (budget.PjbIsDeleted)
            throw new KeyNotFoundException("BudgetNotFound");

        budget.PjbIsDeleted = true;
        budget.PjbDeletedAt = DateTime.UtcNow;
        budget.PjbDeletedByUserId = deletedByUserId;
        budget.PjbUpdatedAt = DateTime.UtcNow;

        _budgetRepo.Update(budget);
        await _budgetRepo.SaveChangesAsync(ct);
    }
}
