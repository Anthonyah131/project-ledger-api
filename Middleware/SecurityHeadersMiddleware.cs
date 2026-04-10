namespace ProjectLedger.API.Middleware;

/// <summary>
/// Security middleware that adds HTTP Security Headers to all responses.
/// Protects against XSS, clickjacking, content sniffing, etc.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        var isSwaggerPath = context.Request.Path.StartsWithSegments("/swagger");

        // Prevents the browser from doing MIME-type sniffing
        headers["X-Content-Type-Options"] = "nosniff";

        // Protection against clickjacking (Swagger UI needs to be able to render)
        if (!isSwaggerPath)
            headers["X-Frame-Options"] = "DENY";

        // Enables XSS protection in legacy browsers
        headers["X-XSS-Protection"] = "1; mode=block";

        // Forces HTTPS in the browser for 1 year
        headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";

        // Conservative referrer policy
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        // CSP: more permissive in /swagger so Swagger UI can load its assets
        headers["Content-Security-Policy"] = isSwaggerPath
            ? "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; connect-src 'self'"
            : "default-src 'none'; frame-ancestors 'none'";

        // Removes the header that reveals it is ASP.NET
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
