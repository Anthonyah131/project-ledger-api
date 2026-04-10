using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Localization;
using ProjectLedger.API.DTOs.Common;
using ProjectLedger.API.Resources;

namespace ProjectLedger.API.Middleware;

/// <summary>
/// Global middleware to catch unhandled exceptions and return
/// a standardized JSON response with the appropriate HTTP code.
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
        var localizer = context.RequestServices.GetRequiredService<IStringLocalizer<Messages>>();
        var isDev = context.RequestServices.GetService<IHostEnvironment>()?.IsDevelopment() == true;

        var (statusCode, code, message) = exception switch
        {
            PlanDeniedException => (StatusCodes.Status403Forbidden,
                "PLAN_DENIED", localizer[exception.Message].Value),
            PlanLimitExceededException => (StatusCodes.Status403Forbidden,
                "PLAN_LIMIT_EXCEEDED", localizer[exception.Message].Value),
            ForbiddenAccessException => (StatusCodes.Status403Forbidden,
                "FORBIDDEN", localizer[exception.Message].Value),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized,
                "UNAUTHORIZED", localizer[exception.Message].Value),
            KeyNotFoundException => (StatusCodes.Status404NotFound,
                "NOT_FOUND", localizer[exception.Message].Value),
            InvalidOperationException => (StatusCodes.Status400BadRequest,
                "BAD_REQUEST", localizer[exception.Message].Value),
            ArgumentException => (StatusCodes.Status400BadRequest,
                "BAD_REQUEST", localizer[exception.Message].Value),
            _ => (StatusCodes.Status500InternalServerError,
                "INTERNAL_ERROR", localizer["UnexpectedError"].Value)
        };

        context.Response.StatusCode = statusCode;

        object response;
        if (exception is PlanDeniedException planDenied)
        {
            response = new
            {
                code,
                message,
                feature = planDenied.Permission?.ToString()
            };
        }
        else if (exception is PlanLimitExceededException planLimit)
        {
            response = new
            {
                code,
                message,
                feature = planLimit.LimitName
            };
        }
        else
        {
            response = isDev && statusCode == 500
                ? new { code, message, detail = exception.Message }
                : LocalizedResponse.Create(code, message);
        }

        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        await context.Response.WriteAsJsonAsync(response, jsonOptions);
    }
}

/// <summary>
/// Extension method to register the middleware in the pipeline.
/// </summary>
public static class GlobalExceptionHandlerMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
    {
        return app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
    }
}

