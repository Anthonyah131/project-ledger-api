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
    private readonly IPaymentMethodRepository _paymentMethodRepo;
    private readonly IPlanAuthorizationService _planAuth;
    private readonly IAuditLogService _auditLog;

    public IncomeService(
        IIncomeRepository incomeRepo,
        IProjectRepository projectRepo,
        IProjectPaymentMethodRepository ppmRepo,
        IPaymentMethodRepository paymentMethodRepo,
        IPlanAuthorizationService planAuth,
        IAuditLogService auditLog)
    {
        _incomeRepo = incomeRepo;
        _projectRepo = projectRepo;
        _ppmRepo = ppmRepo;
        _paymentMethodRepo = paymentMethodRepo;
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
        Guid projectId, bool includeDeleted, bool? isActive, int skip, int take, string? sortBy, bool descending, CancellationToken ct = default)
        => await _incomeRepo.GetByProjectIdPagedAsync(projectId, includeDeleted, isActive, skip, take, sortBy, descending, ct);

    public async Task<IEnumerable<Income>> GetByPaymentMethodIdAsync(
        Guid paymentMethodId, CancellationToken ct = default)
        => await _incomeRepo.GetByPaymentMethodIdAsync(paymentMethodId, ct);

    public async Task<(IReadOnlyList<Income> Items, int TotalCount)> GetByPaymentMethodIdPagedAsync(
        Guid paymentMethodId,
        bool? isActive,
        int skip,
        int take,
        string? sortBy,
        bool descending,
        DateOnly? from,
        DateOnly? to,
        Guid? projectId,
        CancellationToken ct = default)
        => await _incomeRepo.GetByPaymentMethodIdPagedAsync(
            paymentMethodId,
            isActive,
            skip,
            take,
            sortBy,
            descending,
            from,
            to,
            projectId,
            ct);

    public async Task<Income> CreateAsync(Income income, CancellationToken ct = default)
    {
        if (income.IncIsActive)
            ValidateAccountingReadinessForActivation(income);

        var project = await _projectRepo.GetByIdAsync(income.IncProjectId, ct)
            ?? throw new KeyNotFoundException($"Project '{income.IncProjectId}' not found.");

        // Validar límite de ingresos por mes
        var projectIncomes = await _incomeRepo.GetByProjectIdAsync(income.IncProjectId, ct);
        var thisMonthCount = projectIncomes
            .Count(i => i.IncCreatedAt.Year == DateTime.UtcNow.Year
                     && i.IncCreatedAt.Month == DateTime.UtcNow.Month);

        await _planAuth.ValidateLimitAsync(
            project.PrjOwnerUserId, PlanLimits.MaxIncomesPerMonth, thisMonthCount, ct);

        // Validar que la cuenta destino está vinculada al proyecto
        var paymentMethod = await GetLinkedPaymentMethodAsync(
            income.IncProjectId, income.IncPaymentMethodId, ct);

        income.IncAccountCurrency = paymentMethod.PmtCurrency;
        income.IncAccountAmount = ResolveAccountAmount(
            income,
            paymentMethod.PmtCurrency,
            project.PrjCurrencyCode);

        income.IncCreatedAt = DateTime.UtcNow;
        income.IncUpdatedAt = DateTime.UtcNow;

        await _incomeRepo.AddAsync(income, ct);
        await _incomeRepo.SaveChangesAsync(ct);

        await _auditLog.LogAsync("Income", income.IncId, "create", income.IncCreatedByUserId,
            newValues: new
            {
                income.IncId,
                income.IncTitle,
                income.IncConvertedAmount,
                income.IncProjectId,
                income.IncAccountAmount,
                income.IncAccountCurrency
            },
            ct: ct);

        return income;
    }

    public async Task UpdateAsync(Income income, CancellationToken ct = default)
    {
        if (income.IncIsActive)
            ValidateAccountingReadinessForActivation(income);

        var project = await _projectRepo.GetByIdAsync(income.IncProjectId, ct)
            ?? throw new KeyNotFoundException($"Project '{income.IncProjectId}' not found.");

        var paymentMethod = await GetLinkedPaymentMethodAsync(
            income.IncProjectId, income.IncPaymentMethodId, ct);

        income.IncAccountCurrency = paymentMethod.PmtCurrency;
        income.IncAccountAmount = ResolveAccountAmount(
            income,
            paymentMethod.PmtCurrency,
            project.PrjCurrencyCode);

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

    private async Task<PaymentMethod> GetLinkedPaymentMethodAsync(
        Guid projectId,
        Guid paymentMethodId,
        CancellationToken ct)
    {
        var isLinked = await _ppmRepo.IsPaymentMethodLinkedToProjectAsync(projectId, paymentMethodId, ct);
        if (!isLinked)
            throw new InvalidOperationException(
                "The payment method is not linked to this project. " +
                "Link it first via /api/projects/{projectId}/payment-methods.");

        var paymentMethod = await _paymentMethodRepo.GetByIdAsync(paymentMethodId, ct)
            ?? throw new KeyNotFoundException($"Payment method '{paymentMethodId}' not found.");

        if (paymentMethod.PmtIsDeleted)
            throw new KeyNotFoundException($"Payment method '{paymentMethodId}' not found.");

        return paymentMethod;
    }

    private static decimal ResolveAccountAmount(
        Income income,
        string paymentMethodCurrency,
        string projectCurrency)
    {
        if (income.IncAccountAmount is > 0)
            return income.IncAccountAmount.Value;

        if (string.Equals(paymentMethodCurrency, income.IncOriginalCurrency, StringComparison.OrdinalIgnoreCase))
            return income.IncOriginalAmount;

        if (string.Equals(paymentMethodCurrency, projectCurrency, StringComparison.OrdinalIgnoreCase))
            return income.IncConvertedAmount;

        throw new InvalidOperationException(
            "AccountAmount is required when payment method currency differs from both original and project currencies.");
    }

    private static void ValidateAccountingReadinessForActivation(Income income)
    {
        if (income.IncOriginalAmount <= 0)
            throw new InvalidOperationException("Cannot activate income: OriginalAmount must be greater than 0.");

        if (income.IncConvertedAmount <= 0)
            throw new InvalidOperationException("Cannot activate income: ConvertedAmount must be greater than 0.");

        if (income.IncExchangeRate <= 0)
            throw new InvalidOperationException("Cannot activate income: ExchangeRate must be greater than 0.");

        if (string.IsNullOrWhiteSpace(income.IncOriginalCurrency) || income.IncOriginalCurrency.Length != 3)
            throw new InvalidOperationException("Cannot activate income: OriginalCurrency must be a valid 3-letter ISO code.");

        if (string.IsNullOrWhiteSpace(income.IncTitle))
            throw new InvalidOperationException("Cannot activate income: Title is required.");

        if (income.IncIncomeDate == default)
            throw new InvalidOperationException("Cannot activate income: IncomeDate is required.");
    }
}
