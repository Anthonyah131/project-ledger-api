using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using ProjectLedger.API.Data;

namespace ProjectLedger.API.Middleware;

/// <summary>
/// Dedicated middleware for MCP endpoints.
/// Validates a fixed service token and builds a ClaimsPrincipal equivalent
/// to the JWT flow so that filters, policies, and services work without changes.
/// </summary>
public class McpAuthMiddleware
{
    private const string AuthorizationHeaderName = "Authorization";
    private const string UserIdHeaderName = "X-User-Id";
    private const string BearerPrefix = "Bearer ";
    private const string AuthenticationScheme = "McpServiceToken";

    private readonly RequestDelegate _next;
    private readonly ILogger<McpAuthMiddleware> _logger;

    public McpAuthMiddleware(
        RequestDelegate next,
        ILogger<McpAuthMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        AppDbContext dbContext,
        IOptions<McpSettings> mcpOptions)
    {
        var configuredToken = Environment.GetEnvironmentVariable("MCP_SERVICE_TOKEN");
        if (string.IsNullOrWhiteSpace(configuredToken))
            configuredToken = mcpOptions.Value.ServiceToken;

        if (string.IsNullOrWhiteSpace(configuredToken))
        {
            _logger.LogError("MCP service token is not configured.");
            await WriteUnauthorizedAsync(context, "Unauthorized. A valid MCP service token is required.");
            return;
        }

        if (!TryGetBearerToken(context.Request.Headers, out var providedToken)
            || !TokensMatch(providedToken, configuredToken))
        {
            _logger.LogWarning("MCP authentication failed due to missing or invalid service token.");
            await WriteUnauthorizedAsync(context, "Unauthorized. A valid MCP service token is required.");
            return;
        }

        if (!TryGetUserId(context.Request.Headers, out var userId))
        {
            _logger.LogWarning("MCP authentication failed due to missing or invalid X-User-Id header.");
            await WriteForbiddenAsync(context, "Forbidden. X-User-Id header is missing or invalid.");
            return;
        }

        var user = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                u => u.UsrId == userId && !u.UsrIsDeleted && u.UsrIsActive,
                context.RequestAborted);

        if (user is null)
        {
            _logger.LogWarning("MCP authentication failed because user {UserId} was not found.", userId);
            await WriteForbiddenAsync(context, "Forbidden. X-User-Id does not reference a valid user.");
            return;
        }

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.UsrId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.UsrEmail),
            new Claim(JwtRegisteredClaimNames.Name, user.UsrFullName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("plan_id", user.UsrPlanId.ToString()),
            new Claim("is_admin", user.UsrIsAdmin.ToString().ToLowerInvariant()),
            new Claim("is_active", user.UsrIsActive.ToString().ToLowerInvariant())
        };

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, AuthenticationScheme));

        context.User = principal;
        context.Features.Set<IAuthenticateResultFeature>(new McpAuthenticateResultFeature
        {
            AuthenticateResult = AuthenticateResult.Success(new AuthenticationTicket(principal, AuthenticationScheme))
        });

        // Prevents the JWT handler from trying to interpret the service token if it is executed later.
        context.Request.Headers.Remove(AuthorizationHeaderName);

        await _next(context);
    }

    /// <summary>
    /// Extracts the raw token value from an <c>Authorization: Bearer &lt;token&gt;</c> header.
    /// Returns false if the header is absent, malformed, or empty.
    /// </summary>
    private static bool TryGetBearerToken(IHeaderDictionary headers, out string token)
    {
        token = string.Empty;

        if (!headers.TryGetValue(AuthorizationHeaderName, out StringValues authValues))
            return false;

        var headerValue = authValues.ToString();
        if (string.IsNullOrWhiteSpace(headerValue)
            || !headerValue.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
            return false;

        token = headerValue[BearerPrefix.Length..].Trim();
        return !string.IsNullOrWhiteSpace(token);
    }

    /// <summary>
    /// Parses the user GUID from the <c>X-User-Id</c> request header.
    /// Returns false if the header is absent or not a valid GUID.
    /// </summary>
    private static bool TryGetUserId(IHeaderDictionary headers, out Guid userId)
    {
        userId = Guid.Empty;

        if (!headers.TryGetValue(UserIdHeaderName, out StringValues userIdValues))
            return false;

        return Guid.TryParse(userIdValues.ToString(), out userId);
    }

    /// <summary>
    /// Compares two tokens using a constant-time equality check to prevent timing-based side-channel attacks.
    /// Uses <see cref="CryptographicOperations.FixedTimeEquals"/> so that the comparison time does not
    /// vary with the number of matching bytes.
    /// </summary>
    private static bool TokensMatch(string providedToken, string configuredToken)
    {
        var providedBytes = Encoding.UTF8.GetBytes(providedToken);
        var configuredBytes = Encoding.UTF8.GetBytes(configuredToken);

        return providedBytes.Length == configuredBytes.Length
            && CryptographicOperations.FixedTimeEquals(providedBytes, configuredBytes);
    }

    /// <summary>Writes a 401 Unauthorized JSON response and short-circuits the pipeline.</summary>
    private static Task WriteUnauthorizedAsync(HttpContext context, string message)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsJsonAsync(new
        {
            status = StatusCodes.Status401Unauthorized,
            message
        });
    }

    /// <summary>Writes a 403 Forbidden JSON response and short-circuits the pipeline.</summary>
    private static Task WriteForbiddenAsync(HttpContext context, string message)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsJsonAsync(new
        {
            status = StatusCodes.Status403Forbidden,
            message
        });
    }

    private sealed class McpAuthenticateResultFeature : IAuthenticateResultFeature
    {
        public AuthenticateResult? AuthenticateResult { get; set; }
    }
}