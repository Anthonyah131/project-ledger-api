using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ProjectLedger.API.Filters;

/// <summary>
/// Filtro global que restringe operaciones de escritura para usuarios desactivados.
/// 
/// Reglas:
/// - Requests anónimos (sin autenticación) → pasan (ej: register, login).
/// - GET/HEAD/OPTIONS → siempre permitidos (lectura).
/// - POST/PUT/PATCH/DELETE → solo si el usuario tiene is_active = "true" en el JWT.
/// 
/// Los endpoints de auth (register, login, refresh) están marcados con [AllowAnonymous]
/// y no tienen claims, por lo que no se ven afectados.
/// </summary>
public class ActiveUserWriteFilter : IAsyncActionFilter
{
    // Métodos HTTP considerados "lectura" — siempre permitidos
    private static readonly HashSet<string> ReadMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "GET", "HEAD", "OPTIONS"
    };

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var user = context.HttpContext.User;

        // Si no hay usuario autenticado, dejar pasar (lo maneja [Authorize] / [AllowAnonymous])
        if (user.Identity?.IsAuthenticated != true)
        {
            await next();
            return;
        }

        // Lectura siempre permitida
        var method = context.HttpContext.Request.Method;
        if (ReadMethods.Contains(method))
        {
            await next();
            return;
        }

        // Para escritura, verificar is_active claim
        var isActiveClaim = user.FindFirst("is_active")?.Value;
        if (isActiveClaim != "true")
        {
            context.Result = new ObjectResult(new
            {
                message = "Tu cuenta está desactivada. Solo puedes realizar operaciones de lectura."
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        await next();
    }
}
