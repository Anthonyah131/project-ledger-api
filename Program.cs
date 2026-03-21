using Microsoft.AspNetCore.DataProtection;
using Microsoft.OpenApi;
using Microsoft.Extensions.Options;
using ProjectLedger.API.Common.Settings;
using ProjectLedger.API.Extensions;
using ProjectLedger.API.Filters;
using ProjectLedger.API.Middleware;
using ProjectLedger.API.Services;
using QuestPDF.Infrastructure;

// ── QuestPDF License ────────────────────────────────────────
QuestPDF.Settings.License = LicenseType.Community;

// ── Load .env file into environment variables ───────────────
// ASP.NET Core no carga .env automáticamente — lo hacemos aquí.
var envFile = Path.Combine(Directory.GetCurrentDirectory(), ".env");
if (File.Exists(envFile))
{
    foreach (var line in File.ReadAllLines(envFile))
    {
        var trimmed = line.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#')) continue;
        var idx = trimmed.IndexOf('=');
        if (idx < 0) continue;
        var key   = trimmed[..idx].Trim();
        var value = trimmed[(idx + 1)..].Trim();
        Environment.SetEnvironmentVariable(key, value);
    }
}

var builder = WebApplication.CreateBuilder(args);

// ── Data Protection (persistent keys for OAuth state cookie) ─
// En Azure App Service el sistema de archivos bajo /home es persistente.
// Esto evita el error 500 en /signin-google al reiniciar la app.
var keysPath = Environment.GetEnvironmentVariable("HOME") is { } home
    ? Path.Combine(home, "site", "dataprotection-keys")
    : Path.Combine(builder.Environment.ContentRootPath, "dataprotection-keys");

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
    .SetApplicationName("ProjectLedger");

// ── Service Registration ────────────────────────────────────
builder.Services.AddControllers(options =>
{
    // Filtro global: usuarios desactivados solo pueden leer (GET)
    options.Filters.Add<ActiveUserWriteFilter>();
    // Filtro global: admin solo puede acceder a rutas de administración
    options.Filters.Add<AdminIsolationFilter>();
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title       = "Project Ledger API",
        Version     = "v1",
        Description = "API multi-tenant de contabilidad SaaS."
    });

    // Botón Authorize en Swagger UI para JWT
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = SecuritySchemeType.Http,
        Scheme       = "bearer",
        BearerFormat = "JWT",
        In           = ParameterLocation.Header,
        Description  = "Pega el access token JWT aquí."
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

// Database (CockroachDB / PostgreSQL)
builder.Services.AddDatabase(builder.Configuration);

// Email
builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection(EmailSettings.SectionName));
builder.Services.Configure<StripeSettings>(
    builder.Configuration.GetSection(StripeSettings.SectionName));
builder.Services.Configure<ExchangeRateSettings>(
    builder.Configuration.GetSection(ExchangeRateSettings.SectionName));
builder.Services.Configure<AzureDocumentIntelligenceSettings>(
    builder.Configuration.GetSection(AzureDocumentIntelligenceSettings.SectionName));
builder.Services.Configure<GoogleAuthSettings>(
    builder.Configuration.GetSection(GoogleAuthSettings.SectionName));
// Resolver placeholders ${ENV_VAR} en las settings de email
builder.Services.PostConfigure<EmailSettings>(settings =>
{
    settings.SmtpUser     = Resolve(settings.SmtpUser);
    settings.SmtpPassword = Resolve(settings.SmtpPassword);
    settings.FromEmail    = string.IsNullOrEmpty(settings.FromEmail) ? settings.SmtpUser : Resolve(settings.FromEmail);
    static string Resolve(string value) =>
        value.StartsWith("${") && value.EndsWith("}")
            ? Environment.GetEnvironmentVariable(value[2..^1]) ?? string.Empty
            : value;
});

builder.Services.PostConfigure<StripeSettings>(settings =>
{
    if (TryGetBooleanEnv("STRIPE_ENABLED", out var stripeEnabled))
        settings.Enabled = stripeEnabled;

    settings.SecretKey = Resolve(settings.SecretKey);
    settings.WebhookSecret = Resolve(settings.WebhookSecret);
    settings.SuccessUrl = Resolve(settings.SuccessUrl);
    settings.CancelUrl = Resolve(settings.CancelUrl);

    static string Resolve(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.StartsWith("${")
        && value.EndsWith("}")
            ? Environment.GetEnvironmentVariable(value[2..^1]) ?? string.Empty
            : value;

    static bool TryGetBooleanEnv(string key, out bool value)
    {
        var raw = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrWhiteSpace(raw))
        {
            value = false;
            return false;
        }

        if (bool.TryParse(raw, out value))
            return true;

        if (raw == "1")
        {
            value = true;
            return true;
        }

        if (raw == "0")
        {
            value = false;
            return true;
        }

        value = false;
        return false;
    }
});

builder.Services.PostConfigure<ExchangeRateSettings>(settings =>
{
    if (TryGetBooleanEnv("EXCHANGE_RATE_API_ENABLED", out var exchangeEnabled))
        settings.Enabled = exchangeEnabled;

    settings.ApiKey = Resolve(settings.ApiKey);

    if (string.IsNullOrWhiteSpace(settings.BaseUrl))
        settings.BaseUrl = "https://v6.exchangerate-api.com/";

    if (settings.TimeoutSeconds <= 0)
        settings.TimeoutSeconds = 10;

    static string Resolve(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.StartsWith("${")
        && value.EndsWith("}")
            ? Environment.GetEnvironmentVariable(value[2..^1]) ?? string.Empty
            : value;

    static bool TryGetBooleanEnv(string key, out bool value)
    {
        var raw = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrWhiteSpace(raw))
        {
            value = false;
            return false;
        }

        if (bool.TryParse(raw, out value))
            return true;

        if (raw == "1")
        {
            value = true;
            return true;
        }

        if (raw == "0")
        {
            value = false;
            return true;
        }

        value = false;
        return false;
    }
});

builder.Services.PostConfigure<AzureDocumentIntelligenceSettings>(settings =>
{
    if (TryGetBooleanEnv("AZURE_DOC_INTELLIGENCE_ENABLED", out var enabled))
        settings.Enabled = enabled;

    settings.Endpoint = Resolve(settings.Endpoint);
    settings.ApiKey = Resolve(settings.ApiKey);

    if (string.IsNullOrWhiteSpace(settings.DefaultModelId))
        settings.DefaultModelId = "prebuilt-receipt";

    if (settings.TimeoutSeconds <= 0)
        settings.TimeoutSeconds = 30;

    if (settings.PollingIntervalMilliseconds <= 0)
        settings.PollingIntervalMilliseconds = 1000;

    if (settings.MaxPollingAttempts <= 0)
        settings.MaxPollingAttempts = 30;

    if (settings.MaxFileSizeMb <= 0)
        settings.MaxFileSizeMb = 10;

    static string Resolve(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.StartsWith("${")
        && value.EndsWith("}")
            ? Environment.GetEnvironmentVariable(value[2..^1]) ?? string.Empty
            : value;

    static bool TryGetBooleanEnv(string key, out bool value)
    {
        var raw = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrWhiteSpace(raw))
        {
            value = false;
            return false;
        }

        if (bool.TryParse(raw, out value))
            return true;

        if (raw == "1")
        {
            value = true;
            return true;
        }

        if (raw == "0")
        {
            value = false;
            return true;
        }

        value = false;
        return false;
    }
});

builder.Services.PostConfigure<GoogleAuthSettings>(settings =>
{
    settings.ClientId = Resolve(settings.ClientId);
    settings.ClientSecret = Resolve(settings.ClientSecret);
    settings.FrontendCallbackUrl = Resolve(settings.FrontendCallbackUrl);

    static string Resolve(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.StartsWith("${")
        && value.EndsWith("}")
            ? Environment.GetEnvironmentVariable(value[2..^1]) ?? string.Empty
            : value;
});

// Resolver placeholder ${JWT_SECRET_KEY} en JwtSettings (igual que EmailSettings)
builder.Services.PostConfigure<JwtSettings>(settings =>
{
    if (!string.IsNullOrEmpty(settings.SecretKey)
        && settings.SecretKey.StartsWith("${")
        && settings.SecretKey.EndsWith("}"))
    {
        settings.SecretKey = Environment.GetEnvironmentVariable(
            settings.SecretKey[2..^1]) ?? settings.SecretKey;
    }
});

builder.Services.Configure<ChatbotSettings>(
    builder.Configuration.GetSection(ChatbotSettings.SectionName));
// Resolver placeholders ${ENV_VAR} en los API keys del chatbot
builder.Services.PostConfigure<ChatbotSettings>(settings =>
{
    settings.OpenRouter = ResolveProviderSettings(settings.OpenRouter);
    settings.Groq       = ResolveProviderSettings(settings.Groq);
    settings.Cerebras   = ResolveProviderSettings(settings.Cerebras);
    settings.BytePlus   = ResolveProviderSettings(settings.BytePlus);

    static ChatbotProviderSettings ResolveProviderSettings(ChatbotProviderSettings p)
    {
        p.ApiKey = Resolve(p.ApiKey);
        return p;
    }

    static string Resolve(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.StartsWith("${") && value.EndsWith("}")
            ? Environment.GetEnvironmentVariable(value[2..^1]) ?? string.Empty
            : value;
});

builder.Services.Configure<McpSettings>(
    builder.Configuration.GetSection(McpSettings.SectionName));
builder.Services.PostConfigure<McpSettings>(settings =>
{
    if (!string.IsNullOrWhiteSpace(settings.ServiceToken)
        && settings.ServiceToken.StartsWith("${")
        && settings.ServiceToken.EndsWith("}"))
    {
        settings.ServiceToken = Environment.GetEnvironmentVariable(
            settings.ServiceToken[2..^1]) ?? string.Empty;
    }
});

// Security
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddCorsPolicy(builder.Configuration);
builder.Services.AddRateLimiting(builder.Configuration);

// Application services & repositories
builder.Services.AddRepositories();
builder.Services.AddApplicationServices();

// Exchange rate service (ExchangeRate-API) + caching
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<IExchangeRateService, ExchangeRateService>((serviceProvider, client) =>
{
    var settings = serviceProvider.GetRequiredService<IOptions<ExchangeRateSettings>>().Value;
    client.BaseAddress = new Uri(settings.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(Math.Clamp(settings.TimeoutSeconds, 1, 60));
});

// ── Chatbot HTTP clients (uno por proveedor, OpenAI-compatible) ──────────────
// Cada cliente tiene su BaseAddress y Authorization pre-configurados.
// IHttpClientFactory reutiliza los handlers internos (socket pooling).
builder.Services.AddHttpClient("Chatbot.OpenRouter", (sp, client) =>
{
    var s = sp.GetRequiredService<IOptions<ChatbotSettings>>().Value.OpenRouter;
    if (!string.IsNullOrWhiteSpace(s.BaseUrl)) client.BaseAddress = new Uri(s.BaseUrl.TrimEnd('/') + "/");
    if (!string.IsNullOrWhiteSpace(s.ApiKey))  client.DefaultRequestHeaders.Authorization = new("Bearer", s.ApiKey);
    client.Timeout = TimeSpan.FromSeconds(Math.Clamp(s.TimeoutSeconds, 5, 120));
});
builder.Services.AddHttpClient("Chatbot.Groq", (sp, client) =>
{
    var s = sp.GetRequiredService<IOptions<ChatbotSettings>>().Value.Groq;
    if (!string.IsNullOrWhiteSpace(s.BaseUrl)) client.BaseAddress = new Uri(s.BaseUrl.TrimEnd('/') + "/");
    if (!string.IsNullOrWhiteSpace(s.ApiKey))  client.DefaultRequestHeaders.Authorization = new("Bearer", s.ApiKey);
    client.Timeout = TimeSpan.FromSeconds(Math.Clamp(s.TimeoutSeconds, 5, 120));
});
builder.Services.AddHttpClient("Chatbot.Cerebras", (sp, client) =>
{
    var s = sp.GetRequiredService<IOptions<ChatbotSettings>>().Value.Cerebras;
    if (!string.IsNullOrWhiteSpace(s.BaseUrl)) client.BaseAddress = new Uri(s.BaseUrl.TrimEnd('/') + "/");
    if (!string.IsNullOrWhiteSpace(s.ApiKey))  client.DefaultRequestHeaders.Authorization = new("Bearer", s.ApiKey);
    client.Timeout = TimeSpan.FromSeconds(Math.Clamp(s.TimeoutSeconds, 5, 120));
});
builder.Services.AddHttpClient("Chatbot.BytePlus", (sp, client) =>
{
    var s = sp.GetRequiredService<IOptions<ChatbotSettings>>().Value.BytePlus;
    if (!string.IsNullOrWhiteSpace(s.BaseUrl)) client.BaseAddress = new Uri(s.BaseUrl.TrimEnd('/') + "/");
    if (!string.IsNullOrWhiteSpace(s.ApiKey))  client.DefaultRequestHeaders.Authorization = new("Bearer", s.ApiKey);
    client.Timeout = TimeSpan.FromSeconds(Math.Clamp(s.TimeoutSeconds, 5, 120));
});

builder.Services.AddHttpClient<IExpenseDocumentIntelligenceService, ExpenseDocumentIntelligenceService>((serviceProvider, client) =>
{
    var settings = serviceProvider.GetRequiredService<IOptions<AzureDocumentIntelligenceSettings>>().Value;

    if (!string.IsNullOrWhiteSpace(settings.Endpoint)
        && Uri.TryCreate(settings.Endpoint, UriKind.Absolute, out var endpointUri))
    {
        client.BaseAddress = endpointUri;
    }

    client.Timeout = TimeSpan.FromSeconds(Math.Clamp(settings.TimeoutSeconds, 5, 180));
});

var app = builder.Build();

// ── Middleware Pipeline ─────────────────────────────────────
// 1. Global error handler (debe ser el primero)
app.UseGlobalExceptionHandler();

// 2. Security headers en todas las respuestas
app.UseSecurityHeaders();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// 3. HTTPS redirect
app.UseHttpsRedirection();

// 4. Rate Limiting
app.UseRateLimiter();

// 5. CORS (antes de auth)
app.UseCors(CorsSettings.PolicyName);

// 6. MCP auth aislado (solo /api/mcp)
app.UseWhen(
    context => context.Request.Path.StartsWithSegments("/api/mcp"),
    branch => branch.UseMiddleware<McpAuthMiddleware>());

// 7. Auth pipeline
app.UseAuthentication();
app.UseAuthorization();

// 8. Controllers
app.MapControllers();

app.Run();


