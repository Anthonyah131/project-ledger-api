using Microsoft.OpenApi;
using ProjectLedger.API.Extensions;
using ProjectLedger.API.Middleware;

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

// ── Service Registration ────────────────────────────────────
builder.Services.AddControllers();
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

// Security
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddCorsPolicy(builder.Configuration);
builder.Services.AddRateLimiting(builder.Configuration);

// Application services & repositories
builder.Services.AddRepositories();
builder.Services.AddApplicationServices();

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

// 6. Auth pipeline
app.UseAuthentication();
app.UseAuthorization();

// 7. Controllers
app.MapControllers();

app.Run();


