using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectLedger.API.DTOs.Mcp;
using ProjectLedger.API.Services;

namespace ProjectLedger.API.Controllers;

[ApiController]
[Route("api/mcp")]
[Authorize]
[Authorize(Policy = "Plan:CanUseApi")]
[Tags("MCP Assistant")]
[Produces("application/json")]
public class McpController : ControllerBase
{
    private readonly IMcpService _mcpService;

    public McpController(IMcpService mcpService)
    {
        _mcpService = mcpService;
    }

    [HttpGet("context")]
    [ProducesResponseType(typeof(McpContextResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetContext(CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        var response = await _mcpService.GetContextAsync(userId, ct);
        return Ok(response);
    }

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
}
