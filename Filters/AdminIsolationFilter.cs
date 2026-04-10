using Microsoft.AspNetCore.Mvc.Filters;
using ProjectLedger.API.Common.Exceptions;

namespace ProjectLedger.API.Filters;

/// <summary>
/// Global filter that restricts the Global Administrator to administration routes
/// (/api/admin/*) and public routes (/api/auth/*, /api/health*, /api/plans*, /api/currencies*).
/// Guarantees strict multi-tenant isolation: the admin CANNOT see projects,
/// categories, expenses, obligations or financial reports.
/// </summary>
public class AdminIsolationFilter : IAsyncActionFilter
{
    /// <summary>
    /// Route prefixes that an administrator is allowed to access.
    /// </summary>
    private static readonly string[] AllowedPrefixes =
    [
        "/api/admin",       // User management
        "/api/auth",        // Login / register / refresh
        "/api/health",      // Health check
        "/api/plans",       // Plan queries (public)
        "/api/currencies",  // Currency queries (public)
        "/api/billing",     // Billing (Stripe sync/webhooks/subscriptions)
        "/api/users",       // Own profile (self-service)
        "/api/dashboard",   // Dashboard (empty responses for admin)
        "/swagger"          // Documentation
    ];

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var user = context.HttpContext.User;

        // Only applies to authenticated users who are admin
        if (user.Identity?.IsAuthenticated != true)
        {
            await next();
            return;
        }

        var isAdmin = user.FindFirst("is_admin")?.Value == "true";
        if (!isAdmin)
        {
            await next();
            return;
        }

        // Authenticated admin → verify if the route is allowed
        var path = context.HttpContext.Request.Path.Value ?? string.Empty;

        foreach (var prefix in AllowedPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                await next();
                return;
            }
        }

        // Route NOT allowed for admin → 403
        throw new ForbiddenAccessException("AdminIsolation");
    }
}
