using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectLedger.API.DTOs.Dashboard;
using ProjectLedger.API.Services;

namespace ProjectLedger.API.Controllers;

/// <summary>
/// Dashboard mensual a nivel usuario (transversal a proyectos visibles).
/// Regla principal: navegacion por mes con formato YYYY-MM.
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

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    /// <summary>
    /// Devuelve el bloque superior del dashboard mensual (navegación, resumen y alertas).
    /// </summary>
    [HttpGet("monthly-summary")]
    [ProducesResponseType(typeof(MonthlySummaryDashboardResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMonthlySummary(
        [FromQuery] string? month,
        [FromQuery] Guid? projectId,
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
                Alerts = []
            });
        }

        if (!projectId.HasValue)
            return BadRequest(new { error = new { code = "PROJECT_ID_REQUIRED", message = "project_id is required." } });

        var userId = User.GetRequiredUserId();
        var response = await _dashboardService.GetMonthlySummaryAsync(userId, monthStart, projectId.Value, ct);
        return Ok(response);
    }

    /// <summary>
    /// Devuelve tendencia diaria de gasto/ingreso para el mes seleccionado.
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
            return BadRequest(new { error = new { code = "PROJECT_ID_REQUIRED", message = "project_id is required." } });

        var userId = User.GetRequiredUserId();
        var response = await _dashboardService.GetMonthlyDailyTrendAsync(userId, monthStart, projectId.Value, ct);
        return Ok(response);
    }

    /// <summary>
    /// Devuelve top categorías de gasto del mes (todos los proyectos visibles o proyecto puntual).
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
            return BadRequest(new { error = new { code = "PROJECT_ID_REQUIRED", message = "project_id is required." } });

        var userId = User.GetRequiredUserId();
        var response = await _dashboardService.GetMonthlyTopCategoriesAsync(userId, monthStart, projectId.Value, ct);
        return Ok(response);
    }

    /// <summary>
    /// Devuelve la distribución de gasto mensual por método de pago.
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
            return BadRequest(new { error = new { code = "PROJECT_ID_REQUIRED", message = "project_id is required." } });

        var userId = User.GetRequiredUserId();
        var response = await _dashboardService.GetMonthlyPaymentMethodsAsync(userId, monthStart, projectId.Value, ct);
        return Ok(response);
    }

    /// <summary>
    /// Devuelve el overview mensual completo (compatibilidad legado).
    /// </summary>
    /// <param name="month">Mes en formato YYYY-MM.</param>
    /// <response code="200">Overview mensual generado.</response>
    /// <response code="400">Formato de month invalido.</response>
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

    // ── Private Helpers ─────────────────────────────────────

    private IActionResult InvalidMonth()
    {
        return BadRequest(new
        {
            error = new
            {
                code = "INVALID_MONTH",
                message = "month must use YYYY-MM format"
            }
        });
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
