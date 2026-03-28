using System.Globalization;
using ProjectLedger.API.DTOs.Common;
using ProjectLedger.API.DTOs.Mcp;
using ProjectLedger.API.Models;
using ProjectLedger.API.Repositories;

namespace ProjectLedger.API.Services;

public partial class McpService : IMcpService
{
    private readonly IProjectService _projectService;
    private readonly IProjectAccessService _accessService;
    private readonly IExpenseRepository _expenseRepo;
    private readonly IIncomeRepository _incomeRepo;
    private readonly IObligationRepository _obligationRepo;
    private readonly IProjectBudgetRepository _budgetRepo;
    private readonly IPaymentMethodRepository _paymentMethodRepo;
    private readonly IPlanAuthorizationService _planAuth;
    private readonly IPartnerBalanceService _partnerBalanceService;
    private readonly IPartnerSettlementRepository _settlementRepo;

    public McpService(
        IProjectService projectService,
        IProjectAccessService accessService,
        IExpenseRepository expenseRepo,
        IIncomeRepository incomeRepo,
        IObligationRepository obligationRepo,
        IProjectBudgetRepository budgetRepo,
        IPaymentMethodRepository paymentMethodRepo,
        IPlanAuthorizationService planAuth,
        IPartnerBalanceService partnerBalanceService,
        IPartnerSettlementRepository settlementRepo)
    {
        _projectService = projectService;
        _accessService = accessService;
        _expenseRepo = expenseRepo;
        _incomeRepo = incomeRepo;
        _obligationRepo = obligationRepo;
        _budgetRepo = budgetRepo;
        _paymentMethodRepo = paymentMethodRepo;
        _planAuth = planAuth;
        _partnerBalanceService = partnerBalanceService;
        _settlementRepo = settlementRepo;
    }

    public async Task<McpContextResponse> GetContextAsync(Guid userId, CancellationToken ct = default)
    {
        var scope = await ResolveScopeAsync(userId, null, null, ct);
        var capabilities = await _planAuth.GetCapabilitiesAsync(userId, ct);
        var roleMap = await BuildRoleMapAsync(userId, scope.VisibleProjects, ct);

        return new McpContextResponse
        {
            UserId = userId,
            GeneratedAtUtc = DateTime.UtcNow,
            DefaultCurrencyCode = ResolveCurrencyCode(scope.VisibleProjects),
            Permissions = capabilities.Permissions,
            Limits = capabilities.Limits,
            VisibleProjects = scope.VisibleProjects
                .OrderBy(p => p.PrjName)
                .Select(p => new McpVisibleProjectResponse
                {
                    ProjectId = p.PrjId,
                    ProjectName = p.PrjName,
                    CurrencyCode = p.PrjCurrencyCode,
                    UserRole = roleMap.GetValueOrDefault(p.PrjId, ProjectRoles.Viewer)
                })
                .ToList()
        };
    }

    private async Task<McpScope> ResolveScopeAsync(
        Guid userId,
        Guid? projectId,
        string? projectName,
        CancellationToken ct)
    {
        var owned = (await _projectService.GetByOwnerUserIdAsync(userId, ct)).ToList();
        var member = (await _projectService.GetByMemberUserIdAsync(userId, ct)).ToList();

        var candidates = owned
            .Union(member, new ProjectIdComparer())
            .ToList();

        // Defense-in-depth: revalidate effective access to avoid accidental cross-tenant leakage.
        var visible = new List<Project>(candidates.Count);
        foreach (var project in candidates)
        {
            if (project.PrjOwnerUserId == userId)
            {
                visible.Add(project);
                continue;
            }

            if (await _accessService.HasAccessAsync(userId, project.PrjId, ProjectRoles.Viewer, ct))
                visible.Add(project);
        }

        if (projectId.HasValue)
        {
            await _accessService.ValidateAccessAsync(userId, projectId.Value, ProjectRoles.Viewer, ct);

            if (visible.All(p => p.PrjId != projectId.Value))
                throw new ForbiddenAccessException("ProjectAccessDenied");

            var selectedById = visible.Where(p => p.PrjId == projectId.Value).ToList();
            return new McpScope(visible, selectedById, null);
        }

        if (!string.IsNullOrWhiteSpace(projectName))
        {
            var term = Normalize(projectName)!;
            var selectedByName = FilterByNameWithPriority(visible, p => p.PrjName, term)
                .OrderBy(p => p.PrjName)
                .ToList();

            var searchNote = selectedByName.Count == 0
                ? $"No projects matched projectName '{projectName}'. Returned empty results."
                : null;

            return new McpScope(visible, selectedByName, searchNote);
        }

        return new McpScope(visible, visible, null);
    }

    private async Task<Dictionary<Guid, string>> BuildRoleMapAsync(
        Guid userId,
        IReadOnlyCollection<Project> projects,
        CancellationToken ct)
    {
        var roles = new Dictionary<Guid, string>();

        foreach (var project in projects)
        {
            if (project.PrjOwnerUserId == userId)
            {
                roles[project.PrjId] = ProjectRoles.Owner;
                continue;
            }

            var role = await _accessService.GetUserRoleAsync(userId, project.PrjId, ct);
            if (string.IsNullOrWhiteSpace(role))
                throw new ForbiddenAccessException("ProjectAccessDenied");

            roles[project.PrjId] = role;
        }

        return roles;
    }

    private async Task<List<Expense>> LoadExpensesAsync(
        IReadOnlyCollection<Project> projects,
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct)
    {
        EnsureValidDateRange(from, to);

        var result = new List<Expense>();
        foreach (var project in projects)
        {
            var rows = await _expenseRepo.GetByProjectIdWithDetailsAsync(project.PrjId, ct);
            result.AddRange(rows
                .Where(e => !e.ExpIsTemplate)
                .Where(e => !from.HasValue || e.ExpExpenseDate >= from.Value)
                .Where(e => !to.HasValue || e.ExpExpenseDate <= to.Value));
        }

        return result;
    }

    private async Task<List<Income>> LoadIncomesAsync(
        IReadOnlyCollection<Project> projects,
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct)
    {
        EnsureValidDateRange(from, to);

        var result = new List<Income>();
        foreach (var project in projects)
        {
            var rows = await _incomeRepo.GetByProjectIdAsync(project.PrjId, ct);
            result.AddRange(rows
                .Where(i => !from.HasValue || i.IncIncomeDate >= from.Value)
                .Where(i => !to.HasValue || i.IncIncomeDate <= to.Value));
        }

        return result;
    }

    private async Task<List<Obligation>> LoadObligationsWithPaymentsAsync(
        IReadOnlyCollection<Project> projects,
        CancellationToken ct)
    {
        var result = new List<Obligation>();
        foreach (var project in projects)
        {
            var rows = await _obligationRepo.GetByProjectIdWithPaymentsAsync(project.PrjId, ct);
            result.AddRange(rows);
        }

        return result;
    }

    private async Task<Dictionary<Guid, PaymentMethod>> BuildPaymentMethodMapAsync(
        IEnumerable<Guid> paymentMethodIds,
        CancellationToken ct)
    {
        var map = new Dictionary<Guid, PaymentMethod>();

        foreach (var id in paymentMethodIds.Distinct())
        {
            var paymentMethod = await _paymentMethodRepo.GetByIdAsync(id, ct);
            if (paymentMethod is not null && !paymentMethod.PmtIsDeleted)
                map[id] = paymentMethod;
        }

        return map;
    }

    private static McpPagedResponse<T> ToMcpPagedResponse<T>(IEnumerable<T> source, PagedRequest request, string? searchNote = null)
    {
        var list = source.ToList();
        var total = list.Count;
        var items = list.Skip(request.Skip).Take(request.PageSize).ToList();
        return McpPagedResponse<T>.Create(items, total, request, searchNote);
    }

    private static void EnsureValidDateRange(DateOnly? from, DateOnly? to)
    {
        if (from.HasValue && to.HasValue && from.Value > to.Value)
            throw new ArgumentException("InvalidDateRange");
    }

    private static (DateOnly? From, DateOnly? To) ResolveRangeOrDefaults(
        DateOnly? from,
        DateOnly? to,
        string granularity)
    {
        if (from.HasValue && to.HasValue)
            return (from, to);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        return granularity.ToLowerInvariant() switch
        {
            "day" => (from ?? today.AddDays(-29), to ?? today),
            "week" => (from ?? today.AddDays(-83), to ?? today),
            _ => (from ?? new DateOnly(today.Year, today.Month, 1).AddMonths(-11), to ?? today)
        };
    }

    private static DateOnly ParseMonthOrDefault(string? month)
    {
        if (string.IsNullOrWhiteSpace(month))
            return new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);

        if (!DateOnly.TryParseExact(
                $"{month}-01",
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            throw new ArgumentException("InvalidMonth");
        }

        return parsed;
    }

    private static DateOnly GetPeriodStart(DateOnly date, string granularity)
    {
        return granularity.ToLowerInvariant() switch
        {
            "day" => date,
            "week" => date.AddDays(-((7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7)),
            _ => new DateOnly(date.Year, date.Month, 1)
        };
    }

    private static string BuildPeriodLabel(DateOnly periodStart, string granularity)
    {
        return granularity.ToLowerInvariant() switch
        {
            "day" => periodStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            "week" => $"Week of {periodStart:yyyy-MM-dd}",
            _ => periodStart.ToString("yyyy-MM", CultureInfo.InvariantCulture)
        };
    }

    private static string ResolveCurrencyCode(IReadOnlyCollection<Project> projects)
    {
        return projects
            .Where(p => !string.IsNullOrWhiteSpace(p.PrjCurrencyCode))
            .GroupBy(p => p.PrjCurrencyCode)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .Select(g => g.Key)
            .FirstOrDefault() ?? "USD";
    }

    private static decimal ComputePaidAmount(Obligation obligation)
    {
        return obligation.Payments
            .Where(payment => !payment.ExpIsDeleted && payment.ExpIsActive)
            .Sum(payment =>
            string.Equals(payment.ExpOriginalCurrency, obligation.OblCurrency, StringComparison.OrdinalIgnoreCase)
                ? payment.ExpOriginalAmount
                : payment.ExpObligationEquivalentAmount ?? payment.ExpConvertedAmount);
    }

    private static string ComputeObligationStatus(Obligation obligation, decimal paid, DateOnly today)
    {
        if (paid >= obligation.OblTotalAmount) return "paid";
        if (obligation.OblDueDate.HasValue && obligation.OblDueDate.Value < today) return "overdue";
        if (paid > 0) return "partially_paid";
        return "open";
    }

    private static DateTime? MaxDate(DateTime? left, DateTime? right)
    {
        if (!left.HasValue) return right;
        if (!right.HasValue) return left;
        return left > right ? left : right;
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

    private static bool ContainsText(string? source, string? term)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(term))
            return false;

        return source.ToLowerInvariant().Contains(term.ToLowerInvariant(), StringComparison.Ordinal);
    }

    private static List<T> FilterByNameWithPriority<T>(
        IEnumerable<T> source,
        Func<T, string?> selector,
        string term)
    {
        var normalized = term.Trim().ToLowerInvariant();

        var exact = source
            .Where(item => string.Equals(
                selector(item)?.Trim().ToLowerInvariant(),
                normalized,
                StringComparison.Ordinal))
            .ToList();
        if (exact.Count > 0)
            return exact;

        var startsWith = source
            .Where(item => (selector(item) ?? string.Empty).ToLowerInvariant()
                .StartsWith(normalized, StringComparison.Ordinal))
            .ToList();
        if (startsWith.Count > 0)
            return startsWith;

        return source
            .Where(item => ContainsText(selector(item), normalized))
            .ToList();
    }

    private static string? CombineSearchNotes(params string?[] notes)
    {
        var values = notes
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return values.Count == 0 ? null : string.Join(" ", values);
    }

    private static string NormalizeGranularity(string? raw) =>
        raw?.ToLowerInvariant() switch
        {
            "day" or "daily" => "day",
            "week" or "weekly" => "week",
            _ => "month"
        };

    private static string NormalizeDirection(string? raw) =>
        raw?.ToLowerInvariant() switch
        {
            "expense" or "expenses" or "out" or "outgoing" => "expense",
            "income" or "incomes" or "in" or "incoming" => "income",
            _ => "both"
        };

    private sealed class ProjectIdComparer : IEqualityComparer<Project>
    {
        public bool Equals(Project? x, Project? y) => x?.PrjId == y?.PrjId;
        public int GetHashCode(Project obj) => obj.PrjId.GetHashCode();
    }

    private sealed record McpScope(
        IReadOnlyCollection<Project> VisibleProjects,
        IReadOnlyCollection<Project> SelectedProjects,
        string? SearchNote);
}
