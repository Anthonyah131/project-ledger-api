using Microsoft.AspNetCore.Mvc.Filters;
using ProjectLedger.API.Common.Exceptions;

namespace ProjectLedger.API.Filters;

/// <summary>
/// Global filter that restricts write operations for deactivated users.
/// 
/// Rules:
/// - Anonymous requests (without authentication) → pass (e.g. register, login).
/// - GET/HEAD/OPTIONS → always allowed (read).
/// - POST/PUT/PATCH/DELETE → only if the user has is_active = "true" in the JWT.
/// 
/// Auth endpoints (register, login, refresh) are marked with [AllowAnonymous]
/// and have no claims, so they are not affected.
/// </summary>
public class ActiveUserWriteFilter : IAsyncActionFilter
{
    // HTTP methods considered "read" — always allowed
    private static readonly HashSet<string> ReadMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "GET", "HEAD", "OPTIONS"
    };

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var user = context.HttpContext.User;

        // If there is no authenticated user, let it pass (handled by [Authorize] / [AllowAnonymous])
        if (user.Identity?.IsAuthenticated != true)
        {
            await next();
            return;
        }

        // Reading always allowed
        var method = context.HttpContext.Request.Method;
        if (ReadMethods.Contains(method))
        {
            await next();
            return;
        }

        // For writing, verify is_active claim
        var isActiveClaim = user.FindFirst("is_active")?.Value;
        if (isActiveClaim != "true")
            throw new ForbiddenAccessException("AccountDeactivated");

        await next();
    }
}
