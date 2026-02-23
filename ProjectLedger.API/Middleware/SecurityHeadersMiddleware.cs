using System.Net;
using System.Text.Json;

namespace ProjectLedger.API.Middleware;

/// <summary>
/// Middleware de seguridad que agrega HTTP Security Headers a todas las respuestas.
/// Protege contra XSS, clickjacking, sniffing de contenido, etc.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        // Evita que el navegador haga MIME-type sniffing
        headers["X-Content-Type-Options"] = "nosniff";

        // Protección contra clickjacking
        headers["X-Frame-Options"] = "DENY";

        // Habilita protección XSS en navegadores legacy
        headers["X-XSS-Protection"] = "1; mode=block";

        // Fuerza HTTPS en el navegador por 1 año
        headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";

        // Política de referrer conservadora
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        // Content Security Policy básica para una REST API pura
        headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";

        // Elimina el header que revela que es ASP.NET
        headers.Remove("Server");
        headers.Remove("X-Powered-By");

        await _next(context);
    }
}

public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
        => app.UseMiddleware<SecurityHeadersMiddleware>();
}
