using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;
using ProjectLedger.API.Models;
using ProjectLedger.API.Repositories;
using AppPlan = ProjectLedger.API.Models.Plan;

namespace ProjectLedger.API.Services;

public class StripeBillingService : IStripeBillingService
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> CheckoutCustomerLocks = new();

    // ═══════════════════════════════════════════════════════════
    //  Subscription Storage Architecture
    // ═══════════════════════════════════════════════════════════
    //
    //  ┌─────────────────────────────────────────────────────────┐
    //  │  SOURCE OF TRUTH: "user_subscriptions" table           │
    //  │                                                        │
    //  │  Contains the Stripe record with:                      │
    //  │    UssUserId  → FK to the owner user                   │
    //  │    UssPlanId  → FK to the contracted plan              │
    //  │    UssStripeSubscriptionId  → Stripe's sub_xxx         │
    //  │    UssStripeCustomerId      → Stripe's cus_xxx         │
    //  │    UssStatus  → active | trialing | canceled | ...     │
    //  │    UssCurrentPeriodStart / End → cycle dates           │
    //  │                                                        │
    //  │  Created/updated via webhooks:                         │
    //  │    • checkout.session.completed → first creation       │
    //  │    • customer.subscription.updated → changes           │
    //  │    • customer.subscription.deleted → cancellation      │
    //  └─────────────────────────────────────────────────────────┘
    //
    //  ┌─────────────────────────────────────────────────────────┐
    //  │  FAST CACHE: User.UsrPlanId field                      │
    //  │                                                        │
    //  │  Synchronized automatically in UpsertSubscription:     │
    //  │    • Active status → UsrPlanId = contracted plan       │
    //  │    • Canceled status → UsrPlanId = "free" plan         │
    //  │                                                        │
    //  │  PlanAuthorizationService and PlanPermissionHandler    │
    //  │  read UsrPlanId to evaluate plan permissions.          │
    //  └─────────────────────────────────────────────────────────┘
    //
    //  Endpoint /subscription/me reads from user_subscriptions.
    //  Authorization handlers read from User.UsrPlanId.
    //  Both are kept synchronized by UpsertSubscriptionAsync.
    // ═══════════════════════════════════════════════════════════

    private static readonly HashSet<string> HandledEventTypes = new(StringComparer.Ordinal)
    {
        "checkout.session.completed",
        "customer.subscription.updated",
        "customer.subscription.deleted"
    };

    private readonly IPlanRepository _planRepo;
    private readonly IUserRepository _userRepo;
    private readonly IUserSubscriptionRepository _userSubscriptionRepo;
    private readonly IStripeWebhookEventRepository _stripeWebhookEventRepo;
    private readonly StripeSettings _stripeSettings;
    private readonly ILogger<StripeBillingService> _logger;

    public StripeBillingService(
        IPlanRepository planRepo,
        IUserRepository userRepo,
        IUserSubscriptionRepository userSubscriptionRepo,
        IStripeWebhookEventRepository stripeWebhookEventRepo,
        IOptions<StripeSettings> stripeSettings,
        ILogger<StripeBillingService> logger)
    {
        _planRepo = planRepo;
        _userRepo = userRepo;
        _userSubscriptionRepo = userSubscriptionRepo;
        _stripeWebhookEventRepo = stripeWebhookEventRepo;
        _stripeSettings = stripeSettings.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StripePlanSyncResult>> SyncPlansAndPaymentLinksAsync(CancellationToken ct = default)
    {
        EnsureStripeEnabled();
        EnsureStripeSecretConfigured();
        StripeConfiguration.ApiKey = _stripeSettings.SecretKey;

        var plans = (await _planRepo.GetActiveAsync(ct)).ToList();
        if (plans.Count == 0)
            throw new InvalidOperationException("NoActivePlansForSync");

        var productService = new ProductService();
        var priceService = new PriceService();
        var paymentLinkService = new PaymentLinkService();

        var results = new List<StripePlanSyncResult>(plans.Count);

        foreach (var plan in plans)
        {
            if (plan.PlnMonthlyPrice < 0)
                throw new ArgumentException("InvalidPlanPrice");

            var currency = string.IsNullOrWhiteSpace(plan.PlnCurrency)
                ? _stripeSettings.DefaultCurrency.ToLowerInvariant()
                : plan.PlnCurrency.ToLowerInvariant();

            var product = await GetOrCreateProductAsync(productService, plan);
            var price = await GetOrCreatePriceAsync(priceService, plan, product.Id, currency);
            var paymentLink = await GetOrCreatePaymentLinkAsync(paymentLinkService, plan, price.Id);

            plan.PlnCurrency = currency;
            plan.PlnStripeProductId = product.Id;
            plan.PlnStripePriceId = price.Id;
            plan.PlnStripePaymentLinkId = paymentLink.Id;
            plan.PlnStripePaymentLinkUrl = paymentLink.Url;
            plan.PlnUpdatedAt = DateTime.UtcNow;

            _planRepo.Update(plan);

            results.Add(new StripePlanSyncResult
            {
                PlanId = plan.PlnId,
                PlanName = plan.PlnName,
                PlanSlug = plan.PlnSlug,
                MonthlyPrice = plan.PlnMonthlyPrice,
                Currency = plan.PlnCurrency,
                StripeProductId = plan.PlnStripeProductId,
                StripePriceId = plan.PlnStripePriceId,
                StripePaymentLinkId = plan.PlnStripePaymentLinkId,
                StripePaymentLinkUrl = plan.PlnStripePaymentLinkUrl
            });
        }

        await _planRepo.SaveChangesAsync(ct);

        return results;
    }

    /// <inheritdoc />
    public async Task ProcessWebhookAsync(string payload, string signatureHeader, CancellationToken ct = default)
    {
        EnsureStripeEnabled();
        EnsureStripeWebhookConfigured();
        EnsureStripeSecretConfigured();
        StripeConfiguration.ApiKey = _stripeSettings.SecretKey;

        var stripeEvent = EventUtility.ConstructEvent(payload, signatureHeader, _stripeSettings.WebhookSecret);

        // Only process event types we care about — ignore the rest with a silent 200
        if (!HandledEventTypes.Contains(stripeEvent.Type))
        {
            _logger.LogDebug("Ignoring unhandled Stripe event type {EventType} ({EventId})", stripeEvent.Type, stripeEvent.Id);
            return;
        }

        var existingEvent = await _stripeWebhookEventRepo.GetByStripeEventIdAsync(stripeEvent.Id, ct);
        if (existingEvent is { SweProcessedSuccessfully: true })
        {
            _logger.LogInformation("Ignoring already processed Stripe event {EventId}", stripeEvent.Id);
            return;
        }

        var trackingEvent = existingEvent ?? new StripeWebhookEvent
        {
            SweId = Guid.NewGuid(),
            SweStripeEventId = stripeEvent.Id,
            SweType = stripeEvent.Type,
            SweCreatedAt = DateTime.UtcNow
        };

        try
        {
            switch (stripeEvent.Type)
            {
                case "checkout.session.completed":
                    await HandleCheckoutSessionCompletedAsync(stripeEvent, ct);
                    break;
                case "customer.subscription.updated":
                case "customer.subscription.deleted":
                    await HandleSubscriptionEventAsync(stripeEvent, ct);
                    break;
            }

            trackingEvent.SweProcessedSuccessfully = true;
            trackingEvent.SweErrorMessage = null;
            trackingEvent.SweProcessedAt = DateTime.UtcNow;

            if (existingEvent is null)
                await _stripeWebhookEventRepo.AddAsync(trackingEvent, ct);
            else
                _stripeWebhookEventRepo.Update(trackingEvent);

            await _stripeWebhookEventRepo.SaveChangesAsync(ct);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException dbEx)
            when (dbEx.InnerException?.Message.Contains("stripe_webhook_events_event_id_uq") == true)
        {
            // Race condition: another request already saved this event — treat as idempotent success
            _logger.LogInformation("Stripe event {EventId} was already saved by a concurrent request, ignoring duplicate.", stripeEvent.Id);
        }
        catch (Exception ex)
        {
            trackingEvent.SweProcessedSuccessfully = false;
            trackingEvent.SweErrorMessage = ex.Message.Length > 1500
                ? ex.Message[..1500]
                : ex.Message;
            trackingEvent.SweProcessedAt = DateTime.UtcNow;

            try
            {
                if (existingEvent is null)
                    await _stripeWebhookEventRepo.AddAsync(trackingEvent, ct);
                else
                    _stripeWebhookEventRepo.Update(trackingEvent);

                await _stripeWebhookEventRepo.SaveChangesAsync(ct);
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException)
            {
                // If error tracking itself fails (e.g. duplicate key), just log and move on
                _logger.LogWarning("Could not persist error tracking for Stripe event {EventId}: {Message}", stripeEvent.Id, ex.Message);
            }

            throw;
        }
    }

    /// <inheritdoc />
    public async Task<UserSubscription?> GetCurrentUserSubscriptionAsync(Guid userId, CancellationToken ct = default)
    {
        EnsureStripeEnabled();

        // 1) Local lookup first (no Stripe calls). Fast path and shared with read-only mode.
        var localSubscription = await GetCurrentUserSubscriptionReadOnlyAsync(userId, ct);
        if (localSubscription is not null)
        {
            if (ShouldReconcileSubscription(localSubscription))
            {
                var knownUser = await _userRepo.GetByIdAsync(userId, ct);
                if (knownUser is not null)
                {
                    var reconciled = await TryReconcileFromStripeAsync(userId, knownUser, ct);
                    if (reconciled is not null)
                        return reconciled;
                }
            }

            return localSubscription;
        }

        var user = await _userRepo.GetByIdAsync(userId, ct);

        // 2) Fallback: search Stripe customers by user email and claim
        if (user is not null && !string.IsNullOrWhiteSpace(user.UsrEmail))
        {
            EnsureStripeSecretConfigured();
            StripeConfiguration.ApiKey = _stripeSettings.SecretKey;

            try
            {
                var customerService = new CustomerService();
                var customers = await customerService.ListAsync(new CustomerListOptions
                {
                    Email = user.UsrEmail.ToLowerInvariant().Trim(),
                    Limit = 5
                }, cancellationToken: ct);

                foreach (var customer in customers)
                {
                    var byCustId = await _userSubscriptionRepo.GetByStripeCustomerIdAsync(customer.Id, ct);
                    if (byCustId is not null)
                    {
                        _logger.LogInformation(
                            "Auto-claiming orphaned subscription {SubId} for user {UserId} via email {Email} → customer {CustomerId}.",
                            byCustId.UssStripeSubscriptionId, userId, user.UsrEmail, customer.Id);

                        byCustId.UssUserId = userId;
                        byCustId.UssUpdatedAt = DateTime.UtcNow;
                        _userSubscriptionRepo.Update(byCustId);

                        // Link StripeCustomerId to user for future lookups
                        user.UsrStripeCustomerId = customer.Id;
                        if (byCustId.UssPlanId is not null && IsActiveSubscriptionStatus(byCustId.UssStatus))
                            user.UsrPlanId = byCustId.UssPlanId.Value;
                        user.UsrUpdatedAt = DateTime.UtcNow;
                        _userRepo.Update(user);

                        await _userSubscriptionRepo.SaveChangesAsync(ct);
                        return byCustId;
                    }
                }
            }
            catch (StripeException ex)
            {
                _logger.LogWarning(ex, "Could not search Stripe customers by email {Email} for orphan claim.", user.UsrEmail);
            }
        }

        // 3) Resilience fallback: reconcile directly from Stripe.
        // This covers cases where the webhook doesn't arrive (e.g., local env without forwarder).
        if (user is not null)
        {
            var reconciled = await TryReconcileFromStripeAsync(userId, user, ct);
            if (reconciled is not null)
                return reconciled;
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<UserSubscription?> GetCurrentUserSubscriptionReadOnlyAsync(Guid userId, CancellationToken ct = default)
    {
        // 1) Direct lookup by userId (happy path)
        var byUserId = await _userSubscriptionRepo.GetCurrentByUserIdAsync(userId, ct);
        if (byUserId is not null)
            return byUserId;

        // 2) Local fallback by StripeCustomerId (without querying Stripe)
        var user = await _userRepo.GetByIdAsync(userId, ct);
        if (user is null || string.IsNullOrWhiteSpace(user.UsrStripeCustomerId))
            return null;

        var byCustomer = await _userSubscriptionRepo.GetByStripeCustomerIdAsync(user.UsrStripeCustomerId, ct);
        if (byCustomer is null)
            return null;

        // Auto-link the orphaned subscription to the user
        _logger.LogInformation(
            "Auto-claiming orphaned subscription {SubId} for user {UserId} via StripeCustomerId {CustomerId} (read-only path).",
            byCustomer.UssStripeSubscriptionId, userId, user.UsrStripeCustomerId);

        byCustomer.UssUserId = userId;
        byCustomer.UssUpdatedAt = DateTime.UtcNow;
        _userSubscriptionRepo.Update(byCustomer);

        // Synchronize UsrPlanId if the subscription is active
        if (byCustomer.UssPlanId is not null && IsActiveSubscriptionStatus(byCustomer.UssStatus))
        {
            user.UsrPlanId = byCustomer.UssPlanId.Value;
            user.UsrUpdatedAt = DateTime.UtcNow;
            _userRepo.Update(user);
        }

        await _userSubscriptionRepo.SaveChangesAsync(ct);
        return byCustomer;
    }

    /// <inheritdoc />
    public async Task<UserSubscription> ChangePlanAsync(
        Guid userId,
        Guid newPlanId,
        bool prorate = true,
        CancellationToken ct = default)
    {
        EnsureStripeEnabled();
        EnsureStripeSecretConfigured();
        StripeConfiguration.ApiKey = _stripeSettings.SecretKey;

        var targetPlan = await _planRepo.GetByIdAsync(newPlanId, ct)
            ?? throw new KeyNotFoundException("PlanNotFound");

        if (!targetPlan.PlnIsActive)
            throw new InvalidOperationException("PlanNotActive");

        // Downgrade to free = cancel paid subscription at the end of the period.
        if (targetPlan.PlnMonthlyPrice <= 0)
            return await CancelSubscriptionAsync(userId, cancelAtPeriodEnd: true, ct);

        if (string.IsNullOrWhiteSpace(targetPlan.PlnStripePriceId))
            throw new InvalidOperationException("PlanMissingStripePriceId");

        var (user, stripeSubscription) = await GetManagedStripeSubscriptionForUserAsync(userId, ct);
        var currentPriceId = stripeSubscription.Items?.Data?.FirstOrDefault()?.Price?.Id;

        if (string.Equals(currentPriceId, targetPlan.PlnStripePriceId, StringComparison.Ordinal)
            && !stripeSubscription.CancelAtPeriodEnd)
        {
            throw new InvalidOperationException("SubscriptionAlreadyOnPlan");
        }

        var itemId = stripeSubscription.Items?.Data?.FirstOrDefault()?.Id;
        if (string.IsNullOrWhiteSpace(itemId))
            throw new InvalidOperationException("SubscriptionMissingBillableItem");

        var updateOptions = new SubscriptionUpdateOptions
        {
            CancelAtPeriodEnd = false,
            ProrationBehavior = prorate ? "create_prorations" : "none",
            Items =
            [
                new SubscriptionItemOptions
                {
                    Id = itemId,
                    Price = targetPlan.PlnStripePriceId
                }
            ],
            Metadata = new Dictionary<string, string>
            {
                ["app_user_id"] = user.UsrId.ToString(),
                ["plan_id"] = targetPlan.PlnId.ToString(),
                ["plan_slug"] = targetPlan.PlnSlug
            }
        };

        var subscriptionService = new SubscriptionService();
        await subscriptionService.UpdateAsync(stripeSubscription.Id, updateOptions, cancellationToken: ct);

        var refreshed = await subscriptionService.GetAsync(
            stripeSubscription.Id,
            new SubscriptionGetOptions
            {
                Expand = ["items.data.price"]
            },
            cancellationToken: ct);

        await UpsertSubscriptionAsync(refreshed, user.UsrEmail, null, user.UsrId, ct);

        return await _userSubscriptionRepo.GetByStripeSubscriptionIdAsync(refreshed.Id, ct)
            ?? throw new InvalidOperationException("SubscriptionUpdatedButNotLoaded");
    }

    /// <inheritdoc />
    public async Task<UserSubscription> CancelSubscriptionAsync(
        Guid userId,
        bool cancelAtPeriodEnd = true,
        CancellationToken ct = default)
    {
        EnsureStripeEnabled();
        EnsureStripeSecretConfigured();
        StripeConfiguration.ApiKey = _stripeSettings.SecretKey;

        var (user, stripeSubscription) = await GetManagedStripeSubscriptionForUserAsync(userId, ct);
        var subscriptionService = new SubscriptionService();

        Subscription updated;
        if (cancelAtPeriodEnd)
        {
            updated = await subscriptionService.UpdateAsync(
                stripeSubscription.Id,
                new SubscriptionUpdateOptions { CancelAtPeriodEnd = true },
                cancellationToken: ct);
        }
        else
        {
            updated = await subscriptionService.CancelAsync(
                stripeSubscription.Id,
                new SubscriptionCancelOptions
                {
                    InvoiceNow = false,
                    Prorate = false
                },
                cancellationToken: ct);
        }

        var refreshed = await subscriptionService.GetAsync(
            updated.Id,
            new SubscriptionGetOptions
            {
                Expand = ["items.data.price"]
            },
            cancellationToken: ct);

        await UpsertSubscriptionAsync(refreshed, user.UsrEmail, null, user.UsrId, ct);

        return await _userSubscriptionRepo.GetByStripeSubscriptionIdAsync(refreshed.Id, ct)
            ?? throw new InvalidOperationException("SubscriptionCanceledButNotLoaded");
    }

    /// <summary>
    /// Attempts to reconcile a user's subscription state directly from the Stripe API.
    /// This acts as a resilience mechanism if webhooks are missed.
    /// </summary>
    private async Task<UserSubscription?> TryReconcileFromStripeAsync(Guid userId, User user, CancellationToken ct)
    {
        EnsureStripeSecretConfigured();
        StripeConfiguration.ApiKey = _stripeSettings.SecretKey;

        var stripeCustomerId = user.UsrStripeCustomerId;

        if (string.IsNullOrWhiteSpace(stripeCustomerId)
            && !string.IsNullOrWhiteSpace(user.UsrEmail))
        {
            stripeCustomerId = await ResolveStripeCustomerIdByEmailAsync(user.UsrEmail, ct);

            if (!string.IsNullOrWhiteSpace(stripeCustomerId))
            {
                user.UsrStripeCustomerId = stripeCustomerId;
                user.UsrUpdatedAt = DateTime.UtcNow;
                _userRepo.Update(user);
                await _userSubscriptionRepo.SaveChangesAsync(ct);
            }
        }

        if (string.IsNullOrWhiteSpace(stripeCustomerId))
        {
            _logger.LogInformation(
                "Stripe reconciliation skipped for user {UserId}: no Stripe customer id found.",
                userId);
            return null;
        }

        try
        {
            var subscriptionService = new SubscriptionService();
            var subscriptionList = await subscriptionService.ListAsync(new SubscriptionListOptions
            {
                Customer = stripeCustomerId,
                Status = "all",
                Limit = 10
            }, cancellationToken: ct);

            var candidate = subscriptionList.Data
                .OrderByDescending(s => GetSubscriptionPriority(s.Status))
                .ThenByDescending(s => s.Created)
                .FirstOrDefault();

            if (candidate is null)
            {
                _logger.LogInformation(
                    "Stripe reconciliation found no subscriptions for user {UserId} / customer {CustomerId}.",
                    userId,
                    stripeCustomerId);
                return null;
            }

            var fullSubscription = await subscriptionService.GetAsync(
                candidate.Id,
                new SubscriptionGetOptions
                {
                    Expand = ["items.data.price"]
                },
                cancellationToken: ct);

            await UpsertSubscriptionAsync(fullSubscription, user.UsrEmail, null, userId, ct);

            _logger.LogInformation(
                "Stripe reconciliation succeeded for user {UserId} with subscription {SubscriptionId}.",
                userId,
                fullSubscription.Id);

            return await _userSubscriptionRepo.GetCurrentByUserIdAsync(userId, ct);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(
                ex,
                "Stripe reconciliation failed for user {UserId} / customer {CustomerId}.",
                userId,
                stripeCustomerId);
            return null;
        }
    }

    private async Task<string?> ResolveStripeCustomerIdByEmailAsync(string email, CancellationToken ct)
    {
        var normalizedEmail = email.ToLowerInvariant().Trim();
        var customerService = new CustomerService();

        var customers = await customerService.ListAsync(new CustomerListOptions
        {
            Email = normalizedEmail,
            Limit = 1
        }, cancellationToken: ct);

        return customers.Data.FirstOrDefault()?.Id;
    }

    private async Task<string> GetOrCreateStripeCustomerIdForUserAsync(User user, string normalizedEmail, CancellationToken ct)
    {
        var customerService = new CustomerService();

        if (!string.IsNullOrWhiteSpace(user.UsrStripeCustomerId))
        {
            try
            {
                var byStoredId = await customerService.GetAsync(user.UsrStripeCustomerId, cancellationToken: ct);
                await EnsureCustomerMetadataAsync(byStoredId, user, customerService, ct);
                return byStoredId.Id;
            }
            catch (StripeException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Stored Stripe customer {CustomerId} for user {UserId} was not found or not accessible. Falling back to lookup.",
                    user.UsrStripeCustomerId,
                    user.UsrId);
            }
        }

        var byAppUserMetadata = await FindCustomerByAppUserIdAsync(user.UsrId, customerService, ct);
        if (byAppUserMetadata is not null)
        {
            await EnsureCustomerMetadataAsync(byAppUserMetadata, user, customerService, ct);
            await PersistUserStripeCustomerIdAsync(user, byAppUserMetadata.Id, ct);
            return byAppUserMetadata.Id;
        }

        var byEmail = await FindCustomerByEmailAsync(normalizedEmail, customerService, ct);
        if (byEmail is not null)
        {
            await EnsureCustomerMetadataAsync(byEmail, user, customerService, ct);
            await PersistUserStripeCustomerIdAsync(user, byEmail.Id, ct);
            return byEmail.Id;
        }

        var created = await customerService.CreateAsync(new CustomerCreateOptions
        {
            Email = normalizedEmail,
            Name = user.UsrFullName,
            Metadata = new Dictionary<string, string>
            {
                ["app_user_id"] = user.UsrId.ToString()
            }
        }, cancellationToken: ct);

        await PersistUserStripeCustomerIdAsync(user, created.Id, ct);
        return created.Id;
    }

    private async Task<Customer?> FindCustomerByAppUserIdAsync(Guid userId, CustomerService customerService, CancellationToken ct)
    {
        try
        {
            var result = await customerService.SearchAsync(new CustomerSearchOptions
            {
                Query = $"metadata['app_user_id']:'{userId}'",
                Limit = 1
            }, cancellationToken: ct);

            return result.Data.FirstOrDefault();
        }
        catch (StripeException ex)
        {
            _logger.LogDebug(
                ex,
                "Could not search Stripe customer by app_user_id {UserId}. Falling back to email lookup.",
                userId);
            return null;
        }
    }

    private async Task<Customer?> FindCustomerByEmailAsync(string normalizedEmail, CustomerService customerService, CancellationToken ct)
    {
        var customers = await customerService.ListAsync(new CustomerListOptions
        {
            Email = normalizedEmail,
            Limit = 100
        }, cancellationToken: ct);

        return customers.Data
            .OrderBy(c => c.Created)
            .FirstOrDefault();
    }

    private async Task EnsureCustomerMetadataAsync(Customer customer, User user, CustomerService customerService, CancellationToken ct)
    {
        var expectedUserId = user.UsrId.ToString();
        var currentMetadata = customer.Metadata is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(customer.Metadata);

        if (currentMetadata.TryGetValue("app_user_id", out var currentUserId)
            && string.Equals(currentUserId, expectedUserId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        currentMetadata["app_user_id"] = expectedUserId;

        await customerService.UpdateAsync(customer.Id, new CustomerUpdateOptions
        {
            Metadata = currentMetadata
        }, cancellationToken: ct);
    }

    private async Task PersistUserStripeCustomerIdAsync(User user, string stripeCustomerId, CancellationToken ct)
    {
        if (string.Equals(user.UsrStripeCustomerId, stripeCustomerId, StringComparison.Ordinal))
            return;

        user.UsrStripeCustomerId = stripeCustomerId;
        user.UsrUpdatedAt = DateTime.UtcNow;
        _userRepo.Update(user);
        await _userSubscriptionRepo.SaveChangesAsync(ct);
    }

    private static int GetSubscriptionPriority(string? status)
        => status switch
        {
            "active" or "trialing" or "past_due" => 3,
            "incomplete" => 2,
            "canceled" or "incomplete_expired" or "unpaid" => 1,
            _ => 0
        };

    /// <inheritdoc />
    public async Task<(string SessionId, string CheckoutUrl)> CreateCheckoutSessionAsync(
        Guid userId, string userEmail, Guid planId, CancellationToken ct = default)
    {
        EnsureStripeEnabled();
        EnsureStripeSecretConfigured();
        StripeConfiguration.ApiKey = _stripeSettings.SecretKey;

        var plan = await _planRepo.GetByIdAsync(planId, ct)
            ?? throw new KeyNotFoundException("PlanNotFound");

        if (!plan.PlnIsActive)
            throw new InvalidOperationException("PlanNotActive");

        if (plan.PlnMonthlyPrice <= 0)
            throw new InvalidOperationException("PlanIsFree");

        if (string.IsNullOrWhiteSpace(plan.PlnStripePriceId))
            throw new InvalidOperationException("PlanMissingStripePriceId");

        var normalizedEmail = userEmail.ToLowerInvariant().Trim();
        var customerLock = CheckoutCustomerLocks.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));

        await customerLock.WaitAsync(ct);
        string stripeCustomerId;

        try
        {
            // Re-fetch under lock to avoid race conditions creating customers for the same user.
            var user = await _userRepo.GetByIdAsync(userId, ct)
                ?? throw new KeyNotFoundException("UserNotFound");

            stripeCustomerId = await GetOrCreateStripeCustomerIdForUserAsync(user, normalizedEmail, ct);
        }
        finally
        {
            customerLock.Release();
        }

        var managedSubscription = await FindManagedStripeSubscriptionForCustomerAsync(stripeCustomerId, ct);
        if (managedSubscription is not null)
        {
            await UpsertSubscriptionAsync(managedSubscription, normalizedEmail, null, userId, ct);

            var currentPriceId = managedSubscription.Items?.Data?.FirstOrDefault()?.Price?.Id;
            if (string.Equals(currentPriceId, plan.PlnStripePriceId, StringComparison.Ordinal)
                && !managedSubscription.CancelAtPeriodEnd)
            {
                throw new InvalidOperationException("SubscriptionAlreadyOnPlan");
            }

            throw new InvalidOperationException("SubscriptionAlreadyActive");
        }

        var sessionService = new SessionService();
        var session = await sessionService.CreateAsync(new SessionCreateOptions
        {
            Mode = "subscription",
            Customer = stripeCustomerId,
            ClientReferenceId = userId.ToString(),
            CustomerEmail = null, // Do not use CustomerEmail if we already use Customer
            LineItems =
            [
                new SessionLineItemOptions
                {
                    Price = plan.PlnStripePriceId,
                    Quantity = 1
                }
            ],
            SuccessUrl = _stripeSettings.SuccessUrl + "?session_id={CHECKOUT_SESSION_ID}",
            CancelUrl = _stripeSettings.CancelUrl,
            AllowPromotionCodes = _stripeSettings.AllowPromotionCodes,
            Metadata = new Dictionary<string, string>
            {
                ["app_user_id"] = userId.ToString(),
                ["plan_id"] = plan.PlnId.ToString(),
                ["plan_slug"] = plan.PlnSlug
            },
            SubscriptionData = new SessionSubscriptionDataOptions
            {
                Metadata = new Dictionary<string, string>
                {
                    ["app_user_id"] = userId.ToString(),
                    ["plan_id"] = plan.PlnId.ToString(),
                    ["plan_slug"] = plan.PlnSlug
                }
            }
        }, cancellationToken: ct);

        _logger.LogInformation(
            "Created Stripe Checkout Session {SessionId} for user {UserId}, plan {PlanSlug}, customer {CustomerId}.",
            session.Id, userId, plan.PlnSlug, stripeCustomerId);

        return (session.Id, session.Url);
    }

    /// <summary>
    /// Processes a successful Checkout Session, auto-linking metadata and updating subscriptions.
    /// </summary>
    private async Task HandleCheckoutSessionCompletedAsync(Event stripeEvent, CancellationToken ct)
    {
        if (stripeEvent.Data.Object is not Session session)
            return;

        if (!string.Equals(session.Mode, "subscription", StringComparison.OrdinalIgnoreCase))
            return;

        if (string.IsNullOrWhiteSpace(session.SubscriptionId))
            return;

        var subscriptionService = new SubscriptionService();
        var subscription = await subscriptionService.GetAsync(
            session.SubscriptionId,
            new SubscriptionGetOptions
            {
                Expand = ["items.data.price"]
            });

        var fallbackEmail = session.CustomerDetails?.Email ?? session.CustomerEmail;
        Guid? fallbackUserId = null;

        if (!string.IsNullOrWhiteSpace(session.ClientReferenceId)
            && Guid.TryParse(session.ClientReferenceId, out var userIdFromClientReference))
        {
            fallbackUserId = userIdFromClientReference;
        }
        else if (session.Metadata is not null
                 && session.Metadata.TryGetValue("app_user_id", out var rawAppUserId)
                 && Guid.TryParse(rawAppUserId, out var userIdFromMetadata))
        {
            fallbackUserId = userIdFromMetadata;
        }

        await UpsertSubscriptionAsync(subscription, fallbackEmail, session.Metadata, fallbackUserId, ct);
    }

    /// <summary>
    /// Handles updates or deletions of a subscription pushed by webhooks.
    /// </summary>
    private async Task HandleSubscriptionEventAsync(Event stripeEvent, CancellationToken ct)
    {
        if (stripeEvent.Data.Object is not Subscription subscription)
            return;

        await UpsertSubscriptionAsync(subscription, null, null, null, ct);
    }

    /// <summary>
    /// Core synchronization method: Updates the local database to mirror a Stripe subscription.
    /// Also propagates plan changes securely to the User's UsrPlanId field.
    /// </summary>
    private async Task UpsertSubscriptionAsync(
        Subscription subscription,
        string? fallbackEmail,
        Dictionary<string, string>? fallbackMetadata,
        Guid? fallbackUserId,
        CancellationToken ct)
    {
        var metadata = subscription.Metadata?.Count > 0
            ? subscription.Metadata
            : fallbackMetadata;

        var user = await ResolveUserAsync(subscription.CustomerId, fallbackEmail, fallbackUserId, ct);
        var plan = await ResolvePlanAsync(subscription, metadata, ct);

        if (user is null)
        {
            _logger.LogWarning(
                "Could not resolve user for Stripe subscription {SubId} (customer={CustomerId}, fallbackEmail={Email}, fallbackUserId={UserId}). " +
                "Subscription will be saved with UssUserId=NULL — will be auto-claimed when the user polls /subscription/me.",
                subscription.Id, subscription.CustomerId, fallbackEmail, fallbackUserId);
        }
        else
        {
            _logger.LogInformation(
                "Resolved user {UserId} ({Email}) for Stripe subscription {SubId}.",
                user.UsrId, user.UsrEmail, subscription.Id);
        }

        var existing = await _userSubscriptionRepo.GetByStripeSubscriptionIdAsync(subscription.Id, ct);
        var entity = existing ?? new UserSubscription
        {
            UssId = Guid.NewGuid(),
            UssStripeSubscriptionId = subscription.Id,
            UssCreatedAt = DateTime.UtcNow
        };

        // In Stripe.net v49+ (API 2025), CurrentPeriodStart/End are in SubscriptionItem, not in Subscription
        var firstItem = subscription.Items?.Data?.FirstOrDefault();

        entity.UssUserId = user?.UsrId ?? existing?.UssUserId;
        entity.UssPlanId = plan?.PlnId ?? existing?.UssPlanId;
        entity.UssStripeCustomerId = subscription.CustomerId ?? existing?.UssStripeCustomerId;
        entity.UssStripePriceId = firstItem?.Price?.Id ?? existing?.UssStripePriceId;
        entity.UssStatus = subscription.Status ?? "unknown";
        entity.UssCancelAtPeriodEnd = subscription.CancelAtPeriodEnd;
        entity.UssCurrentPeriodStart = firstItem?.CurrentPeriodStart ?? existing?.UssCurrentPeriodStart;
        entity.UssCurrentPeriodEnd = firstItem?.CurrentPeriodEnd ?? existing?.UssCurrentPeriodEnd;
        entity.UssCanceledAt = subscription.CanceledAt;
        entity.UssUpdatedAt = DateTime.UtcNow;

        if (existing is null)
            await _userSubscriptionRepo.AddAsync(entity, ct);
        else
            _userSubscriptionRepo.Update(entity);

        if (entity.UssUserId is not null && IsManagedSubscriptionStatus(entity.UssStatus))
        {
            await CollapseLocalActiveLikeSubscriptionsAsync(
                entity.UssUserId.Value,
                entity.UssStripeSubscriptionId,
                ct);
        }

        if (user is not null)
        {
            var userChanged = false;

            if (!string.IsNullOrWhiteSpace(subscription.CustomerId)
                && !string.Equals(user.UsrStripeCustomerId, subscription.CustomerId, StringComparison.Ordinal))
            {
                user.UsrStripeCustomerId = subscription.CustomerId;
                userChanged = true;
            }

            if (plan is not null && IsActiveSubscriptionStatus(subscription.Status))
            {
                if (user.UsrPlanId != plan.PlnId)
                {
                    user.UsrPlanId = plan.PlnId;
                    userChanged = true;
                }
            }
            else if (IsCanceledSubscriptionStatus(subscription.Status))
            {
                var freePlan = await _planRepo.GetBySlugAsync("free", ct);
                if (freePlan is not null && user.UsrPlanId != freePlan.PlnId)
                {
                    user.UsrPlanId = freePlan.PlnId;
                    userChanged = true;
                }
            }

            if (userChanged)
            {
                user.UsrUpdatedAt = DateTime.UtcNow;
                _userRepo.Update(user);
            }
        }

        await _userSubscriptionRepo.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Retrieves a user's current Stripe subscription from the Stripe API, verifying state conditions.
    /// Throws exceptions if the subscription cannot be managed automatically or is already canceled.
    /// </summary>
    private async Task<(User User, Subscription Subscription)> GetManagedStripeSubscriptionForUserAsync(
        Guid userId,
        CancellationToken ct)
    {
        var user = await _userRepo.GetByIdAsync(userId, ct)
            ?? throw new KeyNotFoundException("UserNotFound");

        var local = await GetCurrentUserSubscriptionAsync(userId, ct)
            ?? throw new KeyNotFoundException("SubscriptionNotFound");

        if (IsCanceledSubscriptionStatus(local.UssStatus))
            throw new InvalidOperationException("SubscriptionAlreadyCanceled");

        EnsureStripeSecretConfigured();
        StripeConfiguration.ApiKey = _stripeSettings.SecretKey;

        var subscriptionService = new SubscriptionService();
        var stripeSubscription = await subscriptionService.GetAsync(
            local.UssStripeSubscriptionId,
            new SubscriptionGetOptions
            {
                Expand = ["items.data.price"]
            },
            cancellationToken: ct);

        if (IsCanceledSubscriptionStatus(stripeSubscription.Status))
        {
            await UpsertSubscriptionAsync(stripeSubscription, user.UsrEmail, null, user.UsrId, ct);
            throw new InvalidOperationException("SubscriptionAlreadyCanceled");
        }

        if (!IsManagedSubscriptionStatus(stripeSubscription.Status))
        {
            await UpsertSubscriptionAsync(stripeSubscription, user.UsrEmail, null, user.UsrId, ct);
            throw new InvalidOperationException("SubscriptionCannotBeManagedAutomatically");
        }

        return (user, stripeSubscription);
    }

    private async Task<Subscription?> FindManagedStripeSubscriptionForCustomerAsync(
        string stripeCustomerId,
        CancellationToken ct)
    {
        var subscriptionService = new SubscriptionService();
        var list = await subscriptionService.ListAsync(new SubscriptionListOptions
        {
            Customer = stripeCustomerId,
            Status = "all",
            Limit = 20
        }, cancellationToken: ct);

        var managed = list.Data
            .Where(s => IsManagedSubscriptionStatus(s.Status))
            .OrderByDescending(s => GetSubscriptionPriority(s.Status))
            .ThenByDescending(s => s.Created)
            .FirstOrDefault();

        if (managed is null)
            return null;

        return await subscriptionService.GetAsync(
            managed.Id,
            new SubscriptionGetOptions
            {
                Expand = ["items.data.price"]
            },
            cancellationToken: ct);
    }

    /// <summary>
    /// Mitigates race conditions by collapsing multiple active-like subscriptions locally,
    /// keeping only the valid Stripe Subscription.
    /// </summary>
    private async Task CollapseLocalActiveLikeSubscriptionsAsync(
        Guid userId,
        string keepStripeSubscriptionId,
        CancellationToken ct)
    {
        var activeLike = await _userSubscriptionRepo.GetActiveLikeByUserIdAsync(userId, ct);

        var duplicates = activeLike
            .Where(s => !string.Equals(s.UssStripeSubscriptionId, keepStripeSubscriptionId, StringComparison.Ordinal))
            .ToList();

        if (duplicates.Count == 0)
            return;

        foreach (var duplicate in duplicates)
        {
            duplicate.UssStatus = "canceled";
            duplicate.UssCancelAtPeriodEnd = false;
            duplicate.UssCanceledAt ??= DateTime.UtcNow;
            duplicate.UssUpdatedAt = DateTime.UtcNow;
            _userSubscriptionRepo.Update(duplicate);
        }

        _logger.LogWarning(
            "User {UserId} had {DuplicateCount} active-like subscriptions. Consolidated to Stripe subscription {KeepSubId}.",
            userId,
            duplicates.Count,
            keepStripeSubscriptionId);
    }

    /// <summary>
    /// Determines whether the local subscription state is obsolete and needs reconciliation.
    /// </summary>
    private bool ShouldReconcileSubscription(UserSubscription subscription)
    {
        if (subscription.UssStatus is "past_due" or "incomplete")
            return true;

        if (subscription.UssCurrentPeriodEnd is null)
            return false;

        var periodEnd = subscription.UssCurrentPeriodEnd.Value;
        var now = DateTime.UtcNow;

        if (subscription.UssCancelAtPeriodEnd && periodEnd <= now.AddMinutes(2))
            return true;

        if (IsActiveSubscriptionStatus(subscription.UssStatus) && periodEnd <= now.AddMinutes(-2))
            return true;

        return false;
    }

    private async Task<User?> ResolveUserAsync(string? stripeCustomerId, string? fallbackEmail, Guid? fallbackUserId, CancellationToken ct)
    {
        if (fallbackUserId is not null)
        {
            var byId = await _userRepo.GetByIdAsync(fallbackUserId.Value, ct);
            if (byId is not null)
                return byId;
        }

        if (!string.IsNullOrWhiteSpace(stripeCustomerId))
        {
            var userByCustomer = await _userRepo.GetByStripeCustomerIdAsync(stripeCustomerId, ct);
            if (userByCustomer is not null)
                return userByCustomer;

            try
            {
                var customerService = new CustomerService();
                var customer = await customerService.GetAsync(stripeCustomerId, cancellationToken: ct);

                if (!string.IsNullOrWhiteSpace(customer.Email))
                {
                    var normalizedCustomerEmail = customer.Email.ToLowerInvariant().Trim();
                    var userByCustomerEmail = await _userRepo.GetByEmailAsync(normalizedCustomerEmail, ct);
                    if (userByCustomerEmail is not null)
                        return userByCustomerEmail;
                }
            }
            catch (StripeException ex)
            {
                _logger.LogWarning(ex, "Could not resolve Stripe customer {CustomerId} while matching subscription to local user.", stripeCustomerId);
            }
        }

        if (!string.IsNullOrWhiteSpace(fallbackEmail))
        {
            var normalizedEmail = fallbackEmail.ToLowerInvariant().Trim();
            return await _userRepo.GetByEmailAsync(normalizedEmail, ct);
        }

        return null;
    }

    private async Task<AppPlan?> ResolvePlanAsync(
        Subscription subscription,
        Dictionary<string, string>? metadata,
        CancellationToken ct)
    {
        if (metadata is not null
            && metadata.TryGetValue("plan_slug", out var planSlug)
            && !string.IsNullOrWhiteSpace(planSlug))
        {
            var bySlug = await _planRepo.GetBySlugAsync(planSlug.ToLowerInvariant(), ct);
            if (bySlug is not null)
                return bySlug;
        }

        if (metadata is not null
            && metadata.TryGetValue("plan_id", out var rawPlanId)
            && Guid.TryParse(rawPlanId, out var planId))
        {
            var byId = await _planRepo.GetByIdAsync(planId, ct);
            if (byId is not null)
                return byId;
        }

        var stripePriceId = subscription.Items?.Data?.FirstOrDefault()?.Price?.Id;
        if (string.IsNullOrWhiteSpace(stripePriceId))
            return null;

        return await _planRepo.GetByStripePriceIdAsync(stripePriceId, ct);
    }

    private async Task<Product> GetOrCreateProductAsync(ProductService productService, AppPlan plan)
    {
        if (!string.IsNullOrWhiteSpace(plan.PlnStripeProductId))
        {
            try
            {
                return await productService.GetAsync(plan.PlnStripeProductId);
            }
            catch (StripeException ex)
            {
                _logger.LogWarning(ex, "Stripe product {ProductId} not found for plan {PlanSlug}. Creating a new product.", plan.PlnStripeProductId, plan.PlnSlug);
            }
        }

        return await productService.CreateAsync(new ProductCreateOptions
        {
            Name = plan.PlnName,
            Description = plan.PlnDescription,
            Active = plan.PlnIsActive,
            Metadata = new Dictionary<string, string>
            {
                ["plan_id"] = plan.PlnId.ToString(),
                ["plan_slug"] = plan.PlnSlug
            }
        });
    }

    private async Task<Price> GetOrCreatePriceAsync(PriceService priceService, AppPlan plan, string productId, string currency)
    {
        if (!string.IsNullOrWhiteSpace(plan.PlnStripePriceId))
        {
            try
            {
                var existingPrice = await priceService.GetAsync(plan.PlnStripePriceId);
                var amountInCents = Convert.ToInt64(decimal.Round(plan.PlnMonthlyPrice * 100m, 0, MidpointRounding.AwayFromZero));

                if (existingPrice.Active
                    && existingPrice.UnitAmount == amountInCents
                    && string.Equals(existingPrice.Currency, currency, StringComparison.OrdinalIgnoreCase))
                {
                    return existingPrice;
                }
            }
            catch (StripeException ex)
            {
                _logger.LogWarning(ex, "Stripe price {PriceId} not found for plan {PlanSlug}. Creating a new price.", plan.PlnStripePriceId, plan.PlnSlug);
            }
        }

        var cents = Convert.ToInt64(decimal.Round(plan.PlnMonthlyPrice * 100m, 0, MidpointRounding.AwayFromZero));

        return await priceService.CreateAsync(new PriceCreateOptions
        {
            Currency = currency,
            UnitAmount = cents,
            Product = productId,
            Recurring = new PriceRecurringOptions
            {
                Interval = "month"
            },
            Metadata = new Dictionary<string, string>
            {
                ["plan_id"] = plan.PlnId.ToString(),
                ["plan_slug"] = plan.PlnSlug
            }
        });
    }

    private async Task<PaymentLink> GetOrCreatePaymentLinkAsync(PaymentLinkService paymentLinkService, AppPlan plan, string priceId)
    {
        if (!string.IsNullOrWhiteSpace(plan.PlnStripePaymentLinkId))
        {
            try
            {
                var existingLink = await paymentLinkService.GetAsync(plan.PlnStripePaymentLinkId);
                if (existingLink.Active && IsRedirectConfigured(existingLink))
                    return existingLink;

                _logger.LogInformation(
                    "Stripe payment link {PaymentLinkId} for plan {PlanSlug} has no redirect to success URL. A new link will be created.",
                    plan.PlnStripePaymentLinkId,
                    plan.PlnSlug);
            }
            catch (StripeException ex)
            {
                _logger.LogWarning(ex, "Stripe payment link {PaymentLinkId} not found for plan {PlanSlug}. Creating a new payment link.", plan.PlnStripePaymentLinkId, plan.PlnSlug);
            }
        }

        return await paymentLinkService.CreateAsync(new PaymentLinkCreateOptions
        {
            LineItems =
            [
                new PaymentLinkLineItemOptions
                {
                    Price = priceId,
                    Quantity = 1
                }
            ],
            AllowPromotionCodes = _stripeSettings.AllowPromotionCodes,
            AfterCompletion = new PaymentLinkAfterCompletionOptions
            {
                Type = "redirect",
                Redirect = new PaymentLinkAfterCompletionRedirectOptions
                {
                    Url = _stripeSettings.SuccessUrl
                }
            },
            Metadata = new Dictionary<string, string>
            {
                ["plan_id"] = plan.PlnId.ToString(),
                ["plan_slug"] = plan.PlnSlug
            },
            SubscriptionData = new PaymentLinkSubscriptionDataOptions
            {
                Metadata = new Dictionary<string, string>
                {
                    ["plan_id"] = plan.PlnId.ToString(),
                    ["plan_slug"] = plan.PlnSlug
                }
            }
        });
    }

    private bool IsRedirectConfigured(PaymentLink link)
    {
        var afterCompletionType = link.AfterCompletion?.Type;
        var redirectUrl = link.AfterCompletion?.Redirect?.Url;

        return string.Equals(afterCompletionType, "redirect", StringComparison.OrdinalIgnoreCase)
               && !string.IsNullOrWhiteSpace(redirectUrl)
               && string.Equals(redirectUrl, _stripeSettings.SuccessUrl, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsActiveSubscriptionStatus(string? status)
        => status is "active" or "trialing" or "past_due";

    private bool IsManagedSubscriptionStatus(string? status)
        => status is "active" or "trialing" or "past_due" or "incomplete";

    private bool IsCanceledSubscriptionStatus(string? status)
        => status is "canceled" or "incomplete_expired" or "unpaid";

    private void EnsureStripeEnabled()
    {
        if (!_stripeSettings.Enabled)
            throw new InvalidOperationException("StripeDisabled");
    }

    private void EnsureStripeSecretConfigured()
    {
        if (string.IsNullOrWhiteSpace(_stripeSettings.SecretKey))
            throw new InvalidOperationException("StripeMissingSecretKey");
    }

    private void EnsureStripeWebhookConfigured()
    {
        if (string.IsNullOrWhiteSpace(_stripeSettings.WebhookSecret))
            throw new InvalidOperationException("StripeMissingWebhookSecret");
    }
}
