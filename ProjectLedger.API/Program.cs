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
builder.Services.AddOpenApi();

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
    app.MapOpenApi();
}

// 3. HTTPS redirect
app.UseHttpsRedirection();

// 4. Rate Limiting
app.UseRateLimiter();

// 5. CORS (antes de auth)
app.UseCors(ProjectLedger.API.Common.CorsSettings.PolicyName);

// 6. Auth pipeline
app.UseAuthentication();
app.UseAuthorization();

// 7. Controllers
app.MapControllers();

app.Run();


