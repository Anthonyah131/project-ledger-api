using Microsoft.EntityFrameworkCore;
using ProjectLedger.API.Data;
using ProjectLedger.API.Repositories;
using ProjectLedger.API.Services;

namespace ProjectLedger.API.Extensions;

/// <summary>
/// Extensiones para registrar servicios de la aplicación en el contenedor DI.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registra AppDbContext con Npgsql (compatible con CockroachDB).
    /// La contraseña se resuelve desde la variable de entorno DB_PASSWORD.
    /// </summary>
    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        // Sustituir placeholder ${DB_PASSWORD} con la variable de entorno real
        var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? string.Empty;
        connectionString = connectionString.Replace("${DB_PASSWORD}", dbPassword);

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__ef_migrations_history");
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorCodesToAdd: null);
            }));

        return services;
    }

    /// <summary>
    /// Registra todas las implementaciones concretas de repositorios.
    /// </summary>
    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.AddScoped<IPlanRepository, PlanRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IProjectMemberRepository, ProjectMemberRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IExpenseRepository, ExpenseRepository>();

        services.AddScoped<ICurrencyRepository, CurrencyRepository>();
        services.AddScoped<IPaymentMethodRepository, PaymentMethodRepository>();
        services.AddScoped<IObligationRepository, ObligationRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IProjectBudgetRepository, ProjectBudgetRepository>();
        services.AddScoped<IExternalAuthProviderRepository, ExternalAuthProviderRepository>();

        return services;
    }

    /// <summary>
    /// Registra las implementaciones concretas de servicios de la aplicación.
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Email
        services.AddScoped<IEmailService, EmailService>();

        // Autenticación y tokens
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();

        // Autorización multi-tenant
        services.AddScoped<IProjectAccessService, ProjectAccessService>();

        // Autorización por plan (feature gates + límites)
        services.AddScoped<IPlanAuthorizationService, PlanAuthorizationService>();

        services.AddScoped<ICurrencyService, CurrencyService>();
        services.AddScoped<IPlanService, PlanService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IProjectMemberService, ProjectMemberService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IPaymentMethodService, PaymentMethodService>();
        services.AddScoped<IExpenseService, ExpenseService>();
        services.AddScoped<IObligationService, ObligationService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IProjectBudgetService, ProjectBudgetService>();

        return services;
    }
}
