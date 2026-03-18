using Microsoft.EntityFrameworkCore;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Data;

/// <summary>
/// Contexto principal de Entity Framework Core para ProjectLedger.
/// Mapea todas las entidades del dominio a las tablas de PostgreSQL/CockroachDB.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // ── DbSets ──────────────────────────────────────────────
    public DbSet<Currency> Currencies => Set<Currency>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<ExternalAuthProvider> ExternalAuthProviders => Set<ExternalAuthProvider>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<PaymentMethod> PaymentMethods => Set<PaymentMethod>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<Obligation> Obligations => Set<Obligation>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ProjectBudget> ProjectBudgets => Set<ProjectBudget>();
    public DbSet<ProjectPaymentMethod> ProjectPaymentMethods => Set<ProjectPaymentMethod>();
    public DbSet<UserSubscription> UserSubscriptions => Set<UserSubscription>();
    public DbSet<StripeWebhookEvent> StripeWebhookEvents => Set<StripeWebhookEvent>();
    public DbSet<Income> Incomes => Set<Income>();
    public DbSet<Partner> Partners => Set<Partner>();
    public DbSet<ProjectAlternativeCurrency> ProjectAlternativeCurrencies => Set<ProjectAlternativeCurrency>();
    public DbSet<TransactionCurrencyExchange> TransactionCurrencyExchanges => Set<TransactionCurrencyExchange>();

    // ── Fase 2b: Workspaces ──────────────────────────────────
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<WorkspaceMember> WorkspaceMembers => Set<WorkspaceMember>();

    // ── Fase 2c: ProjectPartners ─────────────────────────────
    public DbSet<ProjectPartner> ProjectPartners => Set<ProjectPartner>();

    // ── Fase 3a: Splits ──────────────────────────────────────
    public DbSet<ExpenseSplit> ExpenseSplits => Set<ExpenseSplit>();
    public DbSet<IncomeSplit> IncomeSplits => Set<IncomeSplit>();
    public DbSet<SplitCurrencyExchange> SplitCurrencyExchanges => Set<SplitCurrencyExchange>();

    // ── Fase 3c: Partner Settlements ─────────────────────────
    public DbSet<PartnerSettlement> PartnerSettlements => Set<PartnerSettlement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Aplica todas las configuraciones del ensamblado (IEntityTypeConfiguration<T>)
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
