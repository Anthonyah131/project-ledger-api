using System.Net;
using System.Text.Json;

namespace ProjectLedger.API.Middleware;

/// <summary>
/// Middleware global para capturar excepciones no controladas y retornar
/// una respuesta JSON estandarizada con el código HTTP apropiado.
/// </summary>
public class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;

    public GlobalExceptionHandlerMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlerMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception caught by global error handler.");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        var isDev = context.RequestServices.GetService<IHostEnvironment>()?.IsDevelopment() == true;

        var (statusCode, message) = exception switch
        {
            PlanDeniedException => (StatusCodes.Status403Forbidden,
                exception.Message),
            PlanLimitExceededException => (StatusCodes.Status403Forbidden,
                exception.Message),
            ForbiddenAccessException => (StatusCodes.Status403Forbidden,
                exception.Message),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized,
                exception.Message),
            KeyNotFoundException => (StatusCodes.Status404NotFound,
                exception.Message),
            ArgumentException => (StatusCodes.Status400BadRequest,
                exception.Message),
            _ => (StatusCodes.Status500InternalServerError,
                "An unexpected error occurred. Please try again later.")
        };

        context.Response.StatusCode = statusCode;

        var response = new
        {
            status = statusCode,
            message,
            detail = isDev && statusCode == 500 ? exception.Message : (string?)null
        };

        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        await context.Response.WriteAsJsonAsync(response, jsonOptions);
    }
}

/// <summary>
/// Extension method para registrar el middleware en el pipeline.
/// </summary>
public static class GlobalExceptionHandlerMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
    {
        return app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
    }
}
