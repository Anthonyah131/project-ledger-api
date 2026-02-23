using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using ProjectLedger.API.Common;

namespace ProjectLedger.API.Extensions;

/// <summary>
/// Extensiones de seguridad: JWT, CORS, Rate Limiting.
/// </summary>
public static class SecurityExtensions
{
    // ── JWT ─────────────────────────────────────────────────

    /// <summary>
    /// Configura autenticación JWT Bearer.
    /// La SecretKey se resuelve desde la variable de entorno JWT_SECRET_KEY,
    /// con fallback al valor del appsettings (útil para staging con secrets manager).
    /// </summary>
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtSettings = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
            ?? throw new InvalidOperationException("JWT settings not found.");

        // Resolver la clave desde variable de entorno
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

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer           = true,
                    ValidateAudience         = true,
                    ValidateLifetime         = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer              = jwtSettings.Issuer,
                    ValidAudience            = jwtSettings.Audience,
                    IssuerSigningKey         = key,
                    ClockSkew                = TimeSpan.Zero   // Sin tolerancia de tiempo en expiración
                };

                // Propagar eventos de auth para logging
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
                        // Respuesta 401 personalizada en JSON
                        context.HandleResponse();
                        context.Response.StatusCode = 401;
                        context.Response.ContentType = "application/json";
                        var result = System.Text.Json.JsonSerializer.Serialize(new
                        {
                            status  = 401,
                            message = "Unauthorized. A valid JWT token is required."
                        });
                        return context.Response.WriteAsync(result);
                    },
                    OnForbidden = context =>
                    {
                        // Respuesta 403 personalizada en JSON
                        context.Response.StatusCode = 403;
                        context.Response.ContentType = "application/json";
                        var result = System.Text.Json.JsonSerializer.Serialize(new
                        {
                            status  = 403,
                            message = "Forbidden. You don't have permission to access this resource."
                        });
                        return context.Response.WriteAsync(result);
                    }
                };
            });

        services.AddAuthorization();

        return services;
    }

    // ── CORS ────────────────────────────────────────────────

    /// <summary>
    /// Configura CORS con la lista de orígenes permitidos desde appsettings.
    /// En producción se deben definir los dominios exactos del frontend.
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
                    .AllowCredentials()                // Necesario para enviar cookies o auth headers
                    .WithExposedHeaders("X-Total-Count", "X-Page", "X-Page-Size"); // Para paginación
            });
        });

        return services;
    }

    // ── Rate Limiting ───────────────────────────────────────

    /// <summary>
    /// Configura rate limiting global con ventana fija.
    /// Política estricta exclusiva para endpoints de autenticación (login/register).
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

            // Política global: 100 requests / 60s
            options.AddFixedWindowLimiter(RateLimitSettings.PolicyName, limiter =>
            {
                limiter.PermitLimit = rlSettings.PermitLimit;
                limiter.Window      = TimeSpan.FromSeconds(rlSettings.WindowSeconds);
                limiter.QueueLimit  = rlSettings.QueueLimit;
            });

            // Política estricta para Auth: 10 intentos / 60s (anti brute-force)
            options.AddFixedWindowLimiter("AuthRateLimit", limiter =>
            {
                limiter.PermitLimit = 10;
                limiter.Window      = TimeSpan.FromSeconds(60);
                limiter.QueueLimit  = 0;
            });

            // Respuesta personalizada al exceder el límite
            options.OnRejected = async (context, ct) =>
            {
                context.HttpContext.Response.StatusCode = 429;
                context.HttpContext.Response.ContentType = "application/json";
                await context.HttpContext.Response.WriteAsJsonAsync(new
                {
                    status  = 429,
                    message = "Too many requests. Please wait and try again."
                }, ct);
            };
        });

        return services;
    }
}
