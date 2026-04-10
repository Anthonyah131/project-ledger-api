using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectLedger.API.DTOs.Mcp;
using ProjectLedger.API.Services;

namespace ProjectLedger.API.Controllers;

/// <summary>
/// API controller exposing read-only MCP (Model Context Protocol) assistant endpoints.
/// Requires authentication and the <c>Plan:CanUseApi</c> policy.
/// </summary>
[ApiController]
[Route("api/mcp")]
[Authorize]
[Authorize(Policy = "Plan:CanUseApi")]
[Tags("MCP Assistant")]
[Produces("application/json")]
public class McpController : ControllerBase
{
    private readonly IMcpService _mcpService;

    /// <summary>
    /// Initializes a new instance of <see cref="McpController"/>.
    /// </summary>
    public McpController(IMcpService mcpService)
    {
        _mcpService = mcpService;
    }

    /// <summary>
    /// Returns the global context for the authenticated user (projects, currency, plan capabilities).
    /// </summary>
    [HttpGet("context")]
    [ProducesResponseType(typeof(McpContextResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetContext(CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        var response = await _mcpService.GetContextAsync(userId, ct);
        return Ok(response);
    }

    /// <summary>
    /// Returns a paginated portfolio view of the user's projects with budget and expense summaries.
    /// </summary>
    [HttpGet("projects/portfolio")]
    [ProducesResponseType(typeof(McpPagedResponse<McpProjectPortfolioItemResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProjectsPortfolio([FromQuery] McpProjectPortfolioQuery query, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.GetRequiredUserId();
        var response = await _mcpService.GetProjectPortfolioAsync(userId, query, ct);
        return Ok(response);
    }

    /// <summary>
    /// Returns projects with upcoming obligation deadlines.
    /// </summary>
    [HttpGet("projects/deadlines")]
    [ProducesResponseType(typeof(McpPagedResponse<McpProjectDeadlineItemResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProjectDeadlines([FromQuery] McpProjectDeadlinesQuery query, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.GetRequiredUserId();
        var response = await _mcpService.GetProjectDeadlinesAsync(userId, query, ct);
        return Ok(response);
    }

    /// <summary>
    /// Returns a breakdown of active vs. completed projects within a workspace.
    /// </summary>
    [HttpGet("projects/active-vs-completed")]
    [ProducesResponseType(typeof(McpProjectActivitySplitResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProjectActivitySplit([FromQuery] McpProjectActivitySplitQuery query, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.GetRequiredUserId();
        var response = await _mcpService.GetProjectActivitySplitAsync(userId, query, ct);
        return Ok(response);
    }

    /// <summary>
    /// Returns paginated pending payment obligations.
    /// </summary>
    [HttpGet("payments/pending")]
    [ProducesResponseType(typeof(McpPagedResponse<McpPaymentObligationItemResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingPayments([FromQuery] McpPendingPaymentsQuery query, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.GetRequiredUserId();
        var response = await _mcpService.GetPendingPaymentsAsync(userId, query, ct);
        return Ok(response);
    }

    /// <summary>
    /// Returns paginated received payments (expenses linked to obligations).
    /// </summary>
    [HttpGet("payments/received")]
    [ProducesResponseType(typeof(McpPagedResponse<McpReceivedPaymentItemResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReceivedPayments([FromQuery] McpReceivedPaymentsQuery query, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.GetRequiredUserId();
        var response = await _mcpService.GetReceivedPaymentsAsync(userId, query, ct);
        return Ok(response);
    }

    /// <summary>
    /// Returns paginated overdue payment obligations.
    /// </summary>
    [HttpGet("payments/overdue")]
    [ProducesResponseType(typeof(McpPagedResponse<McpPaymentObligationItemResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOverduePayments([FromQuery] McpOverduePaymentsQuery query, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.GetRequiredUserId();
        var response = await _mcpService.GetOverduePaymentsAsync(userId, query, ct);
        return Ok(response);
    }

    /// <summary>
    /// Returns expense distribution grouped by payment method.
    /// </summary>
    [HttpGet("payments/by-method")]
    [ProducesResponseType(typeof(McpPaymentMethodUsageResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaymentMethodUsage([FromQuery] McpPaymentMethodUsageQuery query, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.GetRequiredUserId();
        var response = await _mcpService.GetPaymentMethodUsageAsync(userId, query, ct);
        return Ok(response);
    }

    /// <summary>
    /// Returns aggregated expense totals for a given period.
    /// </summary>
    [HttpGet("expenses/totals")]
    [ProducesResponseType(typeof(McpExpenseTotalsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExpenseTotals([FromQuery] McpExpenseTotalsQuery query, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.GetRequiredUserId();
        var response = await _mcpService.GetExpenseTotalsAsync(userId, query, ct);
        return Ok(response);
    }

    /// <summary>
    /// Returns expense distribution grouped by category.
    /// </summary>
    [HttpGet("expenses/by-category")]
    [ProducesResponseType(typeof(McpExpenseByCategoryResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExpenseByCategory([FromQuery] McpExpenseByCategoryQuery query, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.GetRequiredUserId();
        var response = await _mcpService.GetExpenseByCategoryAsync(userId, query, ct);
        return Ok(response);
    }

    /// <summary>
    /// Returns expense distribution grouped by project.
    /// </summary>
    [HttpGet("expenses/by-project")]
    [ProducesResponseType(typeof(McpExpenseByProjectResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExpenseByProject([FromQuery] McpExpenseByProjectQuery query, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.GetRequiredUserId();
        var response = await _mcpService.GetExpenseByProjectAsync(userId, query, ct);
        return Ok(response);
    }

    /// <summary>
    /// Returns monthly expense trend data over a configurable time window.
    /// </summary>
    [HttpGet("expenses/trends")]
    [ProducesResponseType(typeof(McpExpenseTrendsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExpenseTrends([FromQuery] McpExpenseTrendsQuery query, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.GetRequiredUserId();
        var response = await _mcpService.GetExpenseTrendsAsync(userId, query, ct);
        return Ok(response);
    }

    /// <summary>
    /// Returns income totals grouped by period (monthly).
    /// </summary>
    [HttpGet("income/by-period")]
    [ProducesResponseType(typeof(McpIncomeByPeriodResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetIncomeByPeriod([FromQuery] McpIncomeByPeriodQuery query, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.GetRequiredUserId();
        var response = await _mcpService.GetIncomeByPeriodAsync(userId, query, ct);
        return Ok(response);
    }

    /// <summary>
    /// Returns income distribution grouped by project.
    /// </summary>
    [HttpGet("income/by-project")]
    [ProducesResponseType(typeof(McpIncomeByProjectResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetIncomeByProject([FromQuery] McpIncomeByProjectQuery query, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.GetRequiredUserId();
        var response = await _mcpService.GetIncomeByProjectAsync(userId, query, ct);
        return Ok(response);
    }

    /// <summary>
    /// Returns paginated upcoming obligations sorted by due date.
    /// </summary>
    [HttpGet("obligations/upcoming")]
    [ProducesResponseType(typeof(McpPagedResponse<McpObligationItemResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUpcomingObligations([FromQuery] McpUpcomingObligationsQuery query, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.GetRequiredUserId();
        var response = await _mcpService.GetUpcomingObligationsAsync(userId, query, ct);
        return Ok(response);
    }

    /// <summary>
    /// Returns paginated unpaid obligations across all projects.
    /// </summary>
    [HttpGet("obligations/unpaid")]
    [ProducesResponseType(typeof(McpPagedResponse<McpObligationItemResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUnpaidObligations([FromQuery] McpUnpaidObligationsQuery query, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.GetRequiredUserId();
        var response = await _mcpService.GetUnpaidObligationsAsync(userId, query, ct);
        return Ok(response);
    }

    /// <summary>
    /// Returns a financial health snapshot including savings rate and burn rate indicators.
    /// </summary>
    [HttpGet("summary/financial-health")]
    [ProducesResponseType(typeof(McpFinancialHealthResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFinancialHealth([FromQuery] McpFinancialHealthQuery query, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.GetRequiredUserId();
        var response = await _mcpService.GetFinancialHealthAsync(userId, query, ct);
        return Ok(response);
    }

    /// <summary>
    /// Returns a consolidated monthly overview with income, expenses, and net balance.
    /// </summary>
    [HttpGet("summary/monthly-overview")]
    [ProducesResponseType(typeof(McpMonthlyOverviewResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMonthlyOverview([FromQuery] McpMonthlyOverviewQuery query, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.GetRequiredUserId();
        var response = await _mcpService.GetMonthlyOverviewAsync(userId, query, ct);
        return Ok(response);
    }

    /// <summary>
    /// Returns actionable alerts (budget warnings, overdue obligations, anomalies).
    /// </summary>
    [HttpGet("summary/alerts")]
    [ProducesResponseType(typeof(McpAlertsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAlerts([FromQuery] McpAlertsQuery query, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.GetRequiredUserId();
        var response = await _mcpService.GetAlertsAsync(userId, query, ct);
        return Ok(response);
    }

    /// <summary>
    /// Returns partner balance summaries showing amounts owed and receivable.
    /// </summary>
    [HttpGet("partners/balances")]
    [ProducesResponseType(typeof(McpPartnerBalancesResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPartnerBalances([FromQuery] McpPartnerBalancesQuery query, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.GetRequiredUserId();
        var response = await _mcpService.GetPartnerBalancesAsync(userId, query, ct);
        return Ok(response);
    }

    /// <summary>
    /// Returns paginated partner settlement history.
    /// </summary>
    [HttpGet("partners/settlements")]
    [ProducesResponseType(typeof(McpPagedResponse<McpPartnerSettlementItemResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPartnerSettlements([FromQuery] McpPartnerSettlementsQuery query, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.GetRequiredUserId();
        var response = await _mcpService.GetPartnerSettlementsAsync(userId, query, ct);
        return Ok(response);
    }

    /// <summary>
    /// Returns recent financial movements (expenses and incomes) in reverse chronological order.
    /// </summary>
    [HttpGet("movements/recent")]
    [ProducesResponseType(typeof(McpRecentMovementsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRecentMovements([FromQuery] McpRecentMovementsQuery query, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.GetRequiredUserId();
        var response = await _mcpService.GetRecentMovementsAsync(userId, query, ct);
        return Ok(response);
    }
}
