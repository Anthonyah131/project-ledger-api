using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using ProjectLedger.API.DTOs.Dashboard;
using ProjectLedger.API.DTOs.Common;
using ProjectLedger.API.Resources;
using ProjectLedger.API.Services;

namespace ProjectLedger.API.Controllers;

/// <summary>
/// User-level monthly dashboard (cross-cutting visible projects).
/// Main rule: month navigation with YYYY-MM format.
/// </summary>
[ApiController]
[Route("api/dashboard")]
[Authorize]
[Tags("Dashboard")]
[Produces("application/json")]
public class DashboardController : ControllerBase
{
    private static readonly Regex MonthPattern = new(@"^\d{4}-(0[1-9]|1[0-2])$", RegexOptions.Compiled);

    private readonly IDashboardService _dashboardService;
    private readonly IStringLocalizer<Messages> _localizer;

    public DashboardController(IDashboardService dashboardService,
    IStringLocalizer<Messages> localizer)
    {
        _dashboardService = dashboardService;
        _localizer = localizer;
    }

    /// <summary>
    /// Returns the top block of the monthly dashboard (navigation, summary, and alerts).
    /// </summary>
    [HttpGet("monthly-summary")]
    [ProducesResponseType(typeof(MonthlySummaryDashboardResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMonthlySummary(
        [FromQuery] string? month,
        [FromQuery] Guid? projectId,
        [FromQuery] int comparisonMonths = 1,
        CancellationToken ct = default)
    {
        if (!TryParseMonth(month, out var monthStart))
            return InvalidMonth();

        if (IsAdminUser())
        {
            return Ok(new MonthlySummaryDashboardResponse
            {
                Month = ToMonthKey(monthStart),
                Navigation = BuildEmptyNavigation(monthStart),
                CurrencyCode = "USD",
                ProjectId = projectId,
                GeneratedAt = DateTime.UtcNow,
                Summary = new ExecutiveMonthlySummaryResponse(),
                Comparison = new MonthlyComparisonResponse
                {
                    PreviousMonth = ToMonthKey(monthStart.AddMonths(-1))
                },
                Alerts = [],
                DaysElapsed = DateTime.UtcNow.Day,
                DaysTotal = DateTime.DaysInMonth(monthStart.Year, monthStart.Month),
                AverageDailySpend = 0,
                ComparisonHistory = [],
                LastYearMonth = null
            });
        }

        if (!projectId.HasValue)
            return BadRequest(LocalizedResponse.Create("VALIDATION_ERROR", _localizer["ProjectIdRequired"]));

        var userId = User.GetRequiredUserId();
        var response = await _dashboardService.GetMonthlySummaryAsync(userId, monthStart, projectId.Value, comparisonMonths, ct);
        return Ok(response);
    }

    /// <summary>
    /// Returns the daily spending/income trend for the selected month.
    /// </summary>
    [HttpGet("monthly-daily-trend")]
    [ProducesResponseType(typeof(MonthlyDailyTrendResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMonthlyDailyTrend(
        [FromQuery] string? month,
        [FromQuery] Guid? projectId,
        CancellationToken ct = default)
    {
        if (!TryParseMonth(month, out var monthStart))
            return InvalidMonth();

        if (IsAdminUser())
        {
            return Ok(new MonthlyDailyTrendResponse
            {
                Month = ToMonthKey(monthStart),
                CurrencyCode = "USD",
                ProjectId = projectId,
                TrendByDay = []
            });
        }

        if (!projectId.HasValue)
            return BadRequest(LocalizedResponse.Create("VALIDATION_ERROR", _localizer["ProjectIdRequired"]));

        var userId = User.GetRequiredUserId();
        var response = await _dashboardService.GetMonthlyDailyTrendAsync(userId, monthStart, projectId.Value, ct);
        return Ok(response);
    }

    /// <summary>
    /// Returns the top spending categories of the month (all visible projects or a specific project).
    /// </summary>
    [HttpGet("monthly-top-categories")]
    [ProducesResponseType(typeof(MonthlyTopCategoriesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMonthlyTopCategories(
        [FromQuery] string? month,
        [FromQuery] Guid? projectId,
        CancellationToken ct = default)
    {
        if (!TryParseMonth(month, out var monthStart))
            return InvalidMonth();

        if (IsAdminUser())
        {
            return Ok(new MonthlyTopCategoriesResponse
            {
                Month = ToMonthKey(monthStart),
                CurrencyCode = "USD",
                ProjectId = projectId,
                TopCategories = []
            });
        }

        if (!projectId.HasValue)
            return BadRequest(LocalizedResponse.Create("VALIDATION_ERROR", _localizer["ProjectIdRequired"]));

        var userId = User.GetRequiredUserId();
        var response = await _dashboardService.GetMonthlyTopCategoriesAsync(userId, monthStart, projectId.Value, ct);
        return Ok(response);
    }

    /// <summary>
    /// Returns the distribution of monthly spending by payment method.
    /// </summary>
    [HttpGet("monthly-payment-methods")]
    [ProducesResponseType(typeof(MonthlyPaymentMethodsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMonthlyPaymentMethods(
        [FromQuery] string? month,
        [FromQuery] Guid? projectId,
        CancellationToken ct = default)
    {
        if (!TryParseMonth(month, out var monthStart))
            return InvalidMonth();

        if (IsAdminUser())
        {
            return Ok(new MonthlyPaymentMethodsResponse
            {
                Month = ToMonthKey(monthStart),
                CurrencyCode = "USD",
                ProjectId = projectId,
                PaymentMethodSplit = []
            });
        }

        if (!projectId.HasValue)
            return BadRequest(LocalizedResponse.Create("VALIDATION_ERROR", _localizer["ProjectIdRequired"]));

        var userId = User.GetRequiredUserId();
        var response = await _dashboardService.GetMonthlyPaymentMethodsAsync(userId, monthStart, projectId.Value, ct);
        return Ok(response);
    }

    /// <summary>
    /// Returns the complete monthly overview (legacy compatibility).
    /// </summary>
    /// <param name="month">Month in YYYY-MM format.</param>
    /// <response code="200">Monthly overview generated.</response>
    /// <response code="400">Invalid month format.</response>
    [HttpGet("monthly-overview")]
    [Obsolete("Deprecated. Use monthly-summary, monthly-daily-trend, monthly-top-categories and monthly-payment-methods.")]
    [ProducesResponseType(typeof(MonthlyOverviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetMonthlyOverview(
        [FromQuery] string? month,
        CancellationToken ct = default)
    {
        if (!TryParseMonth(month, out var monthStart))
            return InvalidMonth();

        if (IsAdminUser())
        {
            return Ok(new MonthlyOverviewResponse
            {
                Month = ToMonthKey(monthStart),
                Navigation = BuildEmptyNavigation(monthStart),
                CurrencyCode = "USD",
                GeneratedAt = DateTime.UtcNow,
                Summary = new MonthlySummaryResponse(),
                Comparison = new MonthlyComparisonResponse
                {
                    PreviousMonth = ToMonthKey(monthStart.AddMonths(-1))
                },
                TrendByDay = [],
                TopCategories = [],
                PaymentMethodSplit = [],
                ProjectHealth = [],
                Alerts = []
            });
        }

        var userId = User.GetRequiredUserId();
        var response = await _dashboardService.GetMonthlyOverviewAsync(userId, monthStart, ct);
        return Ok(response);
    }

    /// <summary>
    /// Lightweight project selector for the dashboard.
    /// Page 1: includes the user's pinned projects + paginated non-pinned projects.
    /// Pages > 1: only paginated non-pinned projects (pinned are NOT repeated).
    /// </summary>
    [HttpGet("projects")]
    [ProducesResponseType(typeof(DashboardProjectsPagedResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetDashboardProjects(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? q = null,
        CancellationToken ct = default)
    {
        if (page < 1)
            return BadRequest(LocalizedResponse.Create("VALIDATION_ERROR", _localizer["InvalidPage"]));

        if (pageSize is < 1 or > 100)
            return BadRequest(LocalizedResponse.Create("VALIDATION_ERROR", _localizer["InvalidPageSize"]));

        if (IsAdminUser())
        {
            return Ok(new DashboardProjectsPagedResponse
            {
                Pinned = [],
                Items = [],
                Page = page,
                PageSize = pageSize,
                TotalCount = 0
            });
        }

        var userId = User.GetRequiredUserId();
        var response = await _dashboardService.GetDashboardProjectsAsync(userId, page, pageSize, q, ct);
        return Ok(response);
    }

    /// <summary>
    /// Returns the top N individual transactions (by absolute amount) for a given month and project.
    /// </summary>
    [HttpGet("monthly-top-transactions")]
    [ProducesResponseType(typeof(MonthlyTopTransactionsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMonthlyTopTransactions(
        [FromQuery] string? month,
        [FromQuery] Guid? projectId,
        [FromQuery] int limit = 5,
        [FromQuery] string type = "all",
        CancellationToken ct = default)
    {
        if (!TryParseMonth(month, out var monthStart))
            return InvalidMonth();

        if (limit is < 1 or > 20)
            return BadRequest(LocalizedResponse.Create("VALIDATION_ERROR", _localizer["InvalidLimit"]));

        if (type != "all" && type != "expense" && type != "income")
            return BadRequest(LocalizedResponse.Create("VALIDATION_ERROR", _localizer["InvalidType"]));

        if (IsAdminUser())
        {
            return Ok(new MonthlyTopTransactionsResponse
            {
                Month = ToMonthKey(monthStart),
                CurrencyCode = "USD",
                ProjectId = projectId,
                Transactions = []
            });
        }

        if (!projectId.HasValue)
            return BadRequest(LocalizedResponse.Create("VALIDATION_ERROR", _localizer["ProjectIdRequired"]));

        var userId = User.GetRequiredUserId();
        var response = await _dashboardService.GetMonthlyTopTransactionsAsync(userId, monthStart, projectId.Value, limit, type, ct);
        return Ok(response);
    }

    // ── Private Helpers ─────────────────────────────────────

    private IActionResult InvalidMonth()
    {
        return BadRequest(LocalizedResponse.Create("VALIDATION_ERROR", _localizer["InvalidMonth"]));
    }

    private bool IsAdminUser()
    {
        var isAdminClaim = User.FindFirst("is_admin")?.Value;
        return bool.TryParse(isAdminClaim, out var isAdmin) && isAdmin;
    }

    private static bool TryParseMonth(string? month, out DateOnly monthStart)
    {
        monthStart = default;

        if (string.IsNullOrWhiteSpace(month) || !MonthPattern.IsMatch(month))
            return false;

        return DateOnly.TryParseExact(
            $"{month}-01",
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out monthStart);
    }

    private static string ToMonthKey(DateOnly date)
        => date.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    private static MonthlyNavigationResponse BuildEmptyNavigation(DateOnly monthStart)
    {
        return new MonthlyNavigationResponse
        {
            PreviousMonth = ToMonthKey(monthStart.AddMonths(-1)),
            CurrentMonth = ToMonthKey(monthStart),
            NextMonth = ToMonthKey(monthStart.AddMonths(1)),
            IsCurrentMonth = monthStart.Year == DateTime.UtcNow.Year && monthStart.Month == DateTime.UtcNow.Month,
            HasPreviousData = false,
            HasNextData = false
        };
    }
}
