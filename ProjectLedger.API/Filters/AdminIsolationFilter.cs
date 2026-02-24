using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ProjectLedger.API.Filters;

/// <summary>
/// Filtro global que restringe al Administrador Global a rutas de administración
/// (/api/admin/*) y rutas públicas (/api/auth/*, /api/health*, /api/plans*, /api/currencies*).
/// Garantiza aislamiento multi-tenant estricto: el admin NO puede ver proyectos,
/// categorías, gastos, obligaciones ni reportes financieros.
/// </summary>
public class AdminIsolationFilter : IAsyncActionFilter
{
    /// <summary>
    /// Prefijos de ruta que un administrador tiene permitido acceder.
    /// </summary>
    private static readonly string[] AllowedPrefixes =
    [
        "/api/admin",       // Gestión de usuarios
        "/api/auth",        // Login / register / refresh
        "/api/health",      // Health check
        "/api/plans",       // Consulta de planes (público)
        "/api/currencies",  // Consulta de monedas (público)
        "/api/users",       // Perfil propio (self-service)
        "/swagger"          // Documentación
    ];

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var user = context.HttpContext.User;

        // Solo aplica a usuarios autenticados que son admin
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

        // Admin autenticado → verificar si la ruta está permitida
        var path = context.HttpContext.Request.Path.Value ?? string.Empty;

        foreach (var prefix in AllowedPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                await next();
                return;
            }
        }

        // Ruta NO permitida para admin → 403
        context.Result = new ObjectResult(new
        {
            message = "Los administradores globales solo pueden gestionar usuarios. No tienen acceso a datos de proyectos, categorías, gastos ni obligaciones."
        })
        { StatusCode = StatusCodes.Status403Forbidden };
    }
}
