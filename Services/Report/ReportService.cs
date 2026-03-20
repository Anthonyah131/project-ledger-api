using ProjectLedger.API.DTOs.Report;
using ProjectLedger.API.Repositories;

namespace ProjectLedger.API.Services;

public partial class ReportService : IReportService
{
    private readonly IExpenseRepository _expenseRepo;
    private readonly IIncomeRepository _incomeRepo;
    private readonly IObligationRepository _obligationRepo;
    private readonly ICategoryRepository _categoryRepo;
    private readonly IProjectBudgetRepository _budgetRepo;
    private readonly IProjectService _projectService;
    private readonly IPlanAuthorizationService _planAuth;
    private readonly IPartnerSettlementRepository _settlementRepo;
    private readonly IProjectAlternativeCurrencyRepository _altCurrencyRepo;
    private readonly IPartnerBalanceService _partnerBalanceService;

    public ReportService(
        IExpenseRepository expenseRepo,
        IIncomeRepository incomeRepo,
        IObligationRepository obligationRepo,
        ICategoryRepository categoryRepo,
        IProjectBudgetRepository budgetRepo,
        IProjectService projectService,
        IPlanAuthorizationService planAuth,
        IPartnerSettlementRepository settlementRepo,
        IProjectAlternativeCurrencyRepository altCurrencyRepo,
        IPartnerBalanceService partnerBalanceService)
    {
        _expenseRepo = expenseRepo;
        _incomeRepo = incomeRepo;
        _obligationRepo = obligationRepo;
        _categoryRepo = categoryRepo;
        _budgetRepo = budgetRepo;
        _projectService = projectService;
        _planAuth = planAuth;
        _settlementRepo = settlementRepo;
        _altCurrencyRepo = altCurrencyRepo;
        _partnerBalanceService = partnerBalanceService;
    }
}
