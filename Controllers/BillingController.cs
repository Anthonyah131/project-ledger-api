using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Stripe;
using ProjectLedger.API.DTOs.Billing;
using ProjectLedger.API.DTOs.Common;
using ProjectLedger.API.Models;
using ProjectLedger.API.Resources;
using ProjectLedger.API.Services;

namespace ProjectLedger.API.Controllers;

/// <summary>
/// Controlador de facturación / suscripciones Stripe.
///
/// ═══════════════════════════════════════════════════════════════
///  ARQUITECTURA DE SUSCRIPCIONES
/// ═══════════════════════════════════════════════════════════════
///
///  Flujo de pago:
///    1. Frontend llama POST /api/billing/stripe/create-checkout-session
///       con el PlanId deseado.
///    2. Backend crea una Stripe Checkout Session con:
///       - ClientReferenceId = userId (para vincular en el webhook)
///       - Customer = Stripe customer (creado o reutilizado)
///       - Metadata con app_user_id y plan_slug
///    3. Frontend redirige al usuario al CheckoutUrl devuelto.
///    4. Stripe procesa el pago → envía webhook checkout.session.completed.
///    5. Webhook crea/actualiza registro en user_subscriptions.
///    6. Frontend llega a /billing/success y hace polling a
///       GET /api/billing/subscription/me hasta obtener 200.
///
///  Almacenamiento:
///    - user_subscriptions: fuente de verdad para datos Stripe
///      (status, período, stripe IDs, etc.)
///    - User.UsrPlanId: caché rápido del plan actual, usado por
///      PlanAuthorizationService y PlanPermissionHandler para
///      evaluar permisos en cada request.
///    - Ambos se mantienen sincronizados por UpsertSubscriptionAsync.
///
/// ═══════════════════════════════════════════════════════════════
/// </summary>
[ApiController]
[Route("api/billing")]
[Tags("Billing")]
[Produces("application/json")]
public class BillingController : ControllerBase
{
    private const string StripeDisabledCode = "STRIPE_DISABLED";
    private const string StripeDisabledMessage = "Stripe billing is disabled by configuration.";

    private readonly IStripeBillingService _stripeBillingService;
    private readonly IUserService _userService;
    private readonly StripeSettings _stripeSettings;
    private readonly IStringLocalizer<Messages> _localizer;

    public BillingController(
        IStripeBillingService stripeBillingService,
        IUserService userService,
        IOptions<StripeSettings> stripeSettings,
        IStringLocalizer<Messages> localizer)
    {
        _stripeBillingService = stripeBillingService;
        _userService = userService;
        _stripeSettings = stripeSettings.Value;
        _localizer = localizer;
    }

    [HttpPost("stripe/sync-plans")]
    [Authorize]
    [ProducesResponseType(typeof(StripePlanSyncResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(BillingUnavailableResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SyncStripePlans(CancellationToken ct)
    {
        var stripeDisabled = EnsureStripeEnabledForClientCalls();
        if (stripeDisabled is not null)
            return stripeDisabled;

        if (!IsAdmin())
            return Forbid();

        var items = await _stripeBillingService.SyncPlansAndPaymentLinksAsync(ct);
        var response = new StripePlanSyncResponse
        {
            Items = items.Select(i => new StripePlanSyncItemResponse
            {
                PlanId = i.PlanId,
                PlanName = i.PlanName,
                PlanSlug = i.PlanSlug,
                MonthlyPrice = i.MonthlyPrice,
                Currency = i.Currency,
                StripeProductId = i.StripeProductId,
                StripePriceId = i.StripePriceId,
                StripePaymentLinkId = i.StripePaymentLinkId,
                StripePaymentLinkUrl = i.StripePaymentLinkUrl
            }).ToList()
        };

        return Ok(response);
    }

    [HttpGet("status")]
    [Authorize]
    [ProducesResponseType(typeof(BillingStatusResponse), StatusCodes.Status200OK)]
    public IActionResult GetBillingStatus()
    {
        return Ok(new BillingStatusResponse
        {
            StripeEnabled = _stripeSettings.Enabled,
            Reason = _stripeSettings.Enabled ? null : _localizer["StripeDisabled"].Value
        });
    }

    /// <summary>
    /// Crea una Stripe Checkout Session vinculada al usuario autenticado.
    /// El frontend debe redirigir al CheckoutUrl devuelto.
    /// 
    /// Ventaja sobre Payment Links: garantiza client_reference_id = userId,
    /// lo que permite al webhook vincular la suscripción de forma determinista.
    /// </summary>
    [HttpPost("stripe/create-checkout-session")]
    [Authorize]
    [ProducesResponseType(typeof(CreateCheckoutSessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(BillingUnavailableResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CreateCheckoutSession(
        [FromBody] CreateCheckoutSessionRequest request,
        CancellationToken ct)
    {
        var stripeDisabled = EnsureStripeEnabledForClientCalls();
        if (stripeDisabled is not null)
            return stripeDisabled;

        var userId = User.GetRequiredUserId();
        var email = User.GetEmail()
                    ?? throw new InvalidOperationException(_localizer["EmailClaimNotFound"]);

        try
        {
            var (sessionId, checkoutUrl) = await _stripeBillingService
                .CreateCheckoutSessionAsync(userId, email, request.PlanId, ct);

            return Ok(new CreateCheckoutSessionResponse
            {
                SessionId = sessionId,
                CheckoutUrl = checkoutUrl
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer[ex.Message]));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(LocalizedResponse.Create("VALIDATION_ERROR", _localizer[ex.Message]));
        }
    }

    [HttpGet("subscription/me")]
    [Authorize]
    [ProducesResponseType(typeof(MySubscriptionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMySubscription(CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        var subscription = _stripeSettings.Enabled
            ? await _stripeBillingService.GetCurrentUserSubscriptionAsync(userId, ct)
            : await _stripeBillingService.GetCurrentUserSubscriptionReadOnlyAsync(userId, ct);

        if (subscription is not null)
            return Ok(ToMySubscriptionResponse(subscription));

        // No Stripe subscription record — build a synthetic response from the user's assigned plan.
        // This covers manual plan assignments and deployments with Stripe disabled.
        var user = await _userService.GetByIdAsync(userId, ct);
        if (user is null)
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer["UserNotFound"]));

        return Ok(new MySubscriptionResponse
        {
            UserId = userId,
            PlanId = user.UsrPlanId,
            PlanName = user.Plan?.PlnName,
            PlanSlug = user.Plan?.PlnSlug,
            StripeSubscriptionId = string.Empty,
            StripeCustomerId = null,
            StripePriceId = null,
            Status = "manual",
            CancelAtPeriodEnd = false,
            AutoRenews = false,
            WillDowngradeToFree = false,
            UpdatedAt = user.UsrUpdatedAt
        });
    }

    [HttpPost("subscription/change-plan")]
    [Authorize]
    [ProducesResponseType(typeof(MySubscriptionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(BillingUnavailableResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ChangePlan(
        [FromBody] ChangeSubscriptionPlanRequest request,
        CancellationToken ct)
    {
        var stripeDisabled = EnsureStripeEnabledForClientCalls();
        if (stripeDisabled is not null)
            return stripeDisabled;

        var userId = User.GetRequiredUserId();

        try
        {
            var updated = await _stripeBillingService.ChangePlanAsync(userId, request.PlanId, request.Prorate, ct);
            return Ok(ToMySubscriptionResponse(updated));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer[ex.Message]));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(LocalizedResponse.Create("VALIDATION_ERROR", _localizer[ex.Message]));
        }
    }

    [HttpPost("subscription/cancel")]
    [Authorize]
    [ProducesResponseType(typeof(MySubscriptionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(BillingUnavailableResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CancelSubscription(
        [FromBody] CancelSubscriptionRequest request,
        CancellationToken ct)
    {
        var stripeDisabled = EnsureStripeEnabledForClientCalls();
        if (stripeDisabled is not null)
            return stripeDisabled;

        var userId = User.GetRequiredUserId();

        try
        {
            var updated = await _stripeBillingService.CancelSubscriptionAsync(userId, request.CancelAtPeriodEnd, ct);
            return Ok(ToMySubscriptionResponse(updated));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer[ex.Message]));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(LocalizedResponse.Create("VALIDATION_ERROR", _localizer[ex.Message]));
        }
    }

    [HttpPost("stripe/webhook")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> StripeWebhook(CancellationToken ct)
    {
        if (!_stripeSettings.Enabled)
        {
            // Respond 200 to avoid Stripe retry loops when billing is intentionally disabled.
            return Ok(new { received = true, skipped = true, reason = "Stripe billing is disabled." });
        }

        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(ct);

        var signature = Request.Headers["Stripe-Signature"].ToString();
        if (string.IsNullOrWhiteSpace(signature))
            return BadRequest(LocalizedResponse.Create("VALIDATION_ERROR", "Missing Stripe-Signature header."));

        try
        {
            await _stripeBillingService.ProcessWebhookAsync(payload, signature, ct);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(LocalizedResponse.Create("VALIDATION_ERROR", _localizer[ex.Message]));
        }
        catch (StripeException ex)
        {
            return BadRequest(LocalizedResponse.Create("VALIDATION_ERROR", _localizer[ex.Message]));
        }

        return Ok(new { received = true });
    }

    private static MySubscriptionResponse ToMySubscriptionResponse(UserSubscription subscription)
    {
        var isCanceledNow = IsCanceledSubscriptionStatus(subscription.UssStatus);
        var willDowngrade = !isCanceledNow && subscription.UssCancelAtPeriodEnd;

        return new MySubscriptionResponse
        {
            UserId = subscription.UssUserId,
            PlanId = subscription.UssPlanId,
            PlanName = subscription.Plan?.PlnName,
            PlanSlug = subscription.Plan?.PlnSlug,
            StripeSubscriptionId = subscription.UssStripeSubscriptionId,
            StripeCustomerId = subscription.UssStripeCustomerId,
            StripePriceId = subscription.UssStripePriceId,
            Status = subscription.UssStatus,
            CancelAtPeriodEnd = subscription.UssCancelAtPeriodEnd,
            AutoRenews = !subscription.UssCancelAtPeriodEnd && !isCanceledNow,
            WillDowngradeToFree = willDowngrade,
            DowngradeToFreeAt = willDowngrade ? subscription.UssCurrentPeriodEnd : null,
            CurrentPeriodStart = subscription.UssCurrentPeriodStart,
            CurrentPeriodEnd = subscription.UssCurrentPeriodEnd,
            CanceledAt = subscription.UssCanceledAt,
            UpdatedAt = subscription.UssUpdatedAt
        };
    }

    private static bool IsCanceledSubscriptionStatus(string? status)
        => status is "canceled" or "incomplete_expired" or "unpaid";

    private bool IsAdmin()
    {
        var claim = User.FindFirst("is_admin")?.Value;
        return claim == "true";
    }

    private IActionResult? EnsureStripeEnabledForClientCalls()
    {
        if (_stripeSettings.Enabled)
            return null;

        return StatusCode(StatusCodes.Status503ServiceUnavailable,
            new BillingUnavailableResponse
            {
                Code = StripeDisabledCode,
                Message = _localizer["StripeDisabled"]
            });
    }
}
