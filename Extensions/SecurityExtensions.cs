using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Localization;
using Microsoft.IdentityModel.Tokens;
using ProjectLedger.API.Resources;
using ProjectLedger.API.Services;

namespace ProjectLedger.API.Extensions;

/// <summary>
/// Security extensions: JWT, CORS, Rate Limiting.
/// </summary>
public static class SecurityExtensions
{
    // ── JWT ─────────────────────────────────────────────────

    /// <summary>
    /// Configures JWT Bearer authentication.
    /// The SecretKey is resolved from the JWT_SECRET_KEY environment variable,
    /// with fallback to the appsettings value (useful for staging with secrets manager).
    /// </summary>
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtSettings = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
            ?? throw new InvalidOperationException("JWT settings not found.");

        // Resolve the key from environment variable
        var secretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
                        ?? jwtSettings.SecretKey;

        if (string.IsNullOrWhiteSpace(secretKey) || secretKey.Length < 32)
            throw new InvalidOperationException(
                "JWT SecretKey must be at least 32 characters. Set the JWT_SECRET_KEY environment variable.");

        jwtSettings.SecretKey = secretKey;

        services.Configure<JwtSettings>(opts =>
        {
            configuration.GetSection(JwtSettings.SectionName).Bind(opts);
            opts.SecretKey = secretKey;
        });

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
        {
            KeyId = "ProjectLedger-HS256"
        };

        var googleSettings = configuration.GetSection(GoogleAuthSettings.SectionName).Get<GoogleAuthSettings>()
            ?? new GoogleAuthSettings();

        var googleClientId = ResolvePlaceholder(googleSettings.ClientId);
        var googleClientSecret = ResolvePlaceholder(googleSettings.ClientSecret);

        var authBuilder = services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                // Preserve original JWT claim names (sub, email, etc.)
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer           = true,
                    ValidateAudience         = true,
                    ValidateLifetime         = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer              = jwtSettings.Issuer,
                    ValidAudience            = jwtSettings.Audience,
                    IssuerSigningKey         = key,
                    ClockSkew                = TimeSpan.Zero   // No clock skew tolerance for expiration
                };

                // Propagate auth events for logging
                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILogger<JwtBearerEvents>>();
                        logger.LogWarning("JWT authentication failed: {Message}", context.Exception.Message);
                        return Task.CompletedTask;
                    },
                    OnChallenge = context =>
                    {
                        // Custom 401 JSON response
                        context.HandleResponse();
                        context.Response.StatusCode = 401;
                        context.Response.ContentType = "application/json";
                        var localizer = context.HttpContext.RequestServices
                            .GetRequiredService<IStringLocalizer<Messages>>();
                        var result = System.Text.Json.JsonSerializer.Serialize(new
                        {
                            status  = 401,
                            code = "UNAUTHORIZED", message = localizer["UnauthorizedJwt"].Value
                        });
                        return context.Response.WriteAsync(result);
                    },
                    OnForbidden = context =>
                    {
                        // Custom 403 JSON response
                        context.Response.StatusCode = 403;
                        context.Response.ContentType = "application/json";
                        var localizer = context.HttpContext.RequestServices
                            .GetRequiredService<IStringLocalizer<Messages>>();
                        var result = System.Text.Json.JsonSerializer.Serialize(new
                        {
                            status  = 403,
                            code = "FORBIDDEN", message = localizer["ForbiddenResource"].Value
                        });
                        return context.Response.WriteAsync(result);
                    }
                };
            });

        if (!string.IsNullOrWhiteSpace(googleClientId)
            && !string.IsNullOrWhiteSpace(googleClientSecret))
        {
            authBuilder
                .AddCookie(AuthSchemes.ExternalCookieScheme, options =>
                {
                    options.Cookie.Name = "projectledger.external-auth";
                    options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
                    options.SlidingExpiration = false;
                })
                .AddGoogle(AuthSchemes.GoogleScheme, options =>
                {
                    options.ClientId = googleClientId;
                    options.ClientSecret = googleClientSecret;
                    options.SignInScheme = AuthSchemes.ExternalCookieScheme;
                    options.CallbackPath = "/signin-google";
                    options.SaveTokens = false;
                });
        }

        // ── Authorization Handlers ─────────────────────────────
        services.AddScoped<IAuthorizationHandler, ProjectMemberHandler>();
        services.AddScoped<IAuthorizationHandler, PlanPermissionHandler>();

        services.AddAuthorization(options =>
        {
            // ── Project-level policies (multi-tenant) ────────────

            // Policy: at least project viewer
            options.AddPolicy("ProjectViewer", policy =>
                policy.Requirements.Add(new ProjectMemberRequirement(ProjectRoles.Viewer)));

            // Policy: at least project editor
            options.AddPolicy("ProjectEditor", policy =>
                policy.Requirements.Add(new ProjectMemberRequirement(ProjectRoles.Editor)));

            // Policy: project owner only
            options.AddPolicy("ProjectOwner", policy =>
                policy.Requirements.Add(new ProjectMemberRequirement(ProjectRoles.Owner)));

            // ── Plan-level policies (feature gates) ──────────────
            // Format: "Plan:{PlanPermission}" — one for each plan permission.
            // Usage: [Authorize(Policy = "Plan:CanExportData")]

            foreach (var permission in Enum.GetValues<PlanPermission>())
            {
                options.AddPolicy($"Plan:{permission}", policy =>
                    policy.Requirements.Add(new PlanPermissionRequirement(permission)));
            }
        });

        return services;
    }

    private static string ResolvePlaceholder(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        if (value.StartsWith("${") && value.EndsWith("}"))
            return Environment.GetEnvironmentVariable(value[2..^1]) ?? string.Empty;

        return value;
    }

    // ── CORS ────────────────────────────────────────────────

    /// <summary>
    /// Configures CORS with the list of allowed origins from appsettings.
    /// In production, the exact frontend domains must be defined.
    /// </summary>
    public static IServiceCollection AddCorsPolicy(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var corsSettings = configuration.GetSection(CorsSettings.SectionName).Get<CorsSettings>()
            ?? new CorsSettings();

        services.AddCors(options =>
        {
            options.AddPolicy(CorsSettings.PolicyName, policy =>
            {
                policy
                    .WithOrigins(corsSettings.AllowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials()                // Necessary for sending cookies or auth headers
                    .WithExposedHeaders("X-Total-Count", "X-Page", "X-Page-Size"); // For pagination
            });
        });

        return services;
    }

    // ── Rate Limiting ───────────────────────────────────────

    /// <summary>
    /// Configures global rate limiting with fixed window.
    /// Strict policy exclusive for authentication endpoints (login/register).
    /// </summary>
    public static IServiceCollection AddRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var rlSettings = configuration.GetSection(RateLimitSettings.SectionName).Get<RateLimitSettings>()
            ?? new RateLimitSettings();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = 429;

            // Global policy: 100 requests / 60s
            options.AddFixedWindowLimiter(RateLimitSettings.PolicyName, limiter =>
            {
                limiter.PermitLimit = rlSettings.PermitLimit;
                limiter.Window      = TimeSpan.FromSeconds(rlSettings.WindowSeconds);
                limiter.QueueLimit  = rlSettings.QueueLimit;
            });

            // Strict policy for Auth: 30 attempts / 60s (anti brute-force)
            options.AddFixedWindowLimiter("AuthRateLimit", limiter =>
            {
                limiter.PermitLimit = 30;
                limiter.Window      = TimeSpan.FromSeconds(60);
                limiter.QueueLimit  = 0;
            });

            // Custom response when exceeding limit
            options.OnRejected = async (context, ct) =>
            {
                context.HttpContext.Response.StatusCode = 429;
                context.HttpContext.Response.ContentType = "application/json";
                var localizer = context.HttpContext.RequestServices
                    .GetRequiredService<IStringLocalizer<Messages>>();
                await context.HttpContext.Response.WriteAsJsonAsync(new
                {
                    status  = 429,
                    code = "TOO_MANY_REQUESTS", message = localizer["TooManyRequests"].Value
                }, ct);
            };
        });

        return services;
    }
}
