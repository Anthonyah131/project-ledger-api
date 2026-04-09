using Microsoft.EntityFrameworkCore;
using ProjectLedger.API.Data;
using ProjectLedger.API.Repositories;
using ProjectLedger.API.Services;
using ProjectLedger.API.Services.Chatbot;
using ProjectLedger.API.Services.Chatbot.Interfaces;
using ProjectLedger.API.Services.Chatbot.Providers;
using ProjectLedger.API.Services.Report;

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
        services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
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
        services.AddScoped<IProjectPaymentMethodRepository, ProjectPaymentMethodRepository>();
        services.AddScoped<IUserSubscriptionRepository, UserSubscriptionRepository>();
        services.AddScoped<IStripeWebhookEventRepository, StripeWebhookEventRepository>();

        // Multi-currency & Incomes
        services.AddScoped<IIncomeRepository, IncomeRepository>();
        services.AddScoped<IProjectAlternativeCurrencyRepository, ProjectAlternativeCurrencyRepository>();
        services.AddScoped<ITransactionCurrencyExchangeRepository, TransactionCurrencyExchangeRepository>();

        // Partners
        services.AddScoped<IPartnerRepository, PartnerRepository>();

        // Workspaces (Fase 2b)
        services.AddScoped<IWorkspaceRepository, WorkspaceRepository>();

        // ProjectPartners (Fase 2c)
        services.AddScoped<IProjectPartnerRepository, ProjectPartnerRepository>();

        // Splits (Fase 3a)
        services.AddScoped<IExpenseSplitRepository, ExpenseSplitRepository>();
        services.AddScoped<IIncomeSplitRepository, IncomeSplitRepository>();
        services.AddScoped<ISplitCurrencyExchangeRepository, SplitCurrencyExchangeRepository>();
        services.AddScoped<ISplitCurrencyExchangeService, SplitCurrencyExchangeService>();

        // Partner Settlements & Balances (Fase 3c)
        services.AddScoped<IPartnerSettlementRepository, PartnerSettlementRepository>();
        services.AddScoped<IPartnerBalanceRepository, PartnerBalanceRepository>();

        // Global Search
        services.AddScoped<ISearchRepository, SearchRepository>();

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
        services.AddScoped<IStripeBillingService, StripeBillingService>();

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
        services.AddScoped<IProjectPaymentMethodService, ProjectPaymentMethodService>();
        services.AddScoped<ITransactionReferenceGuardService, TransactionReferenceGuardService>();

        // Reportes
        services.AddScoped<IReportExportService, ReportExportService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IUserReportService, UserReportService>();
        services.AddScoped<IWorkspaceReportService, WorkspaceReportService>();

        // Multi-currency & Incomes
        services.AddScoped<IIncomeService, IncomeService>();
        services.AddScoped<IProjectAlternativeCurrencyService, ProjectAlternativeCurrencyService>();
        services.AddScoped<ITransactionCurrencyExchangeService, TransactionCurrencyExchangeService>();

        // MCP assistant read model
        services.AddScoped<IMcpService, McpService>();

        // Partners
        services.AddScoped<IPartnerService, PartnerService>();

        // Workspaces (Fase 2b)
        services.AddScoped<IWorkspaceService, WorkspaceService>();

        // ProjectPartners (Fase 2c)
        services.AddScoped<IProjectPartnerService, ProjectPartnerService>();

        // Partner Settlements & Balances (Fase 3c)
        services.AddScoped<IPartnerSettlementService, PartnerSettlementService>();
        services.AddScoped<IPartnerBalanceService, PartnerBalanceService>();

        // Partner Reports
        services.AddScoped<IPartnerReportService, PartnerReportService>();

        // Global Search
        services.AddScoped<ISearchService, SearchService>();

        // Chatbot IA (rotación de proveedores gratuitos)
        // Singleton: el rotador mantiene el índice entre peticiones
        services.AddSingleton<ChatbotProviderRotator>();
        // Los proveedores se registran como IEnumerable<IChatProvider> para inyectarlos todos
        services.AddScoped<IChatProvider, OpenRouterChatProvider>();
        services.AddScoped<IChatProvider, GroqChatProvider>();
        services.AddScoped<IChatProvider, CerebrasChatProvider>();
        services.AddScoped<IChatProvider, BytePlusChatProvider>();
        services.AddScoped<IChatbotService, ChatbotService>();
        services.AddScoped<IIntentRouter, IntentRouter>();

        return services;
    }
}
