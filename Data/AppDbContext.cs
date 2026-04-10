using Microsoft.EntityFrameworkCore;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Data;

/// <summary>
/// Main Entity Framework Core context for ProjectLedger.
/// Maps all domain entities to PostgreSQL/CockroachDB tables.
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

    // ── Phase 2b: Workspaces ──────────────────────────────────
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<WorkspaceMember> WorkspaceMembers => Set<WorkspaceMember>();

    // ── Phase 2c: ProjectPartners ─────────────────────────────
    public DbSet<ProjectPartner> ProjectPartners => Set<ProjectPartner>();

    // ── Phase 3a: Splits ──────────────────────────────────────
    public DbSet<ExpenseSplit> ExpenseSplits => Set<ExpenseSplit>();
    public DbSet<IncomeSplit> IncomeSplits => Set<IncomeSplit>();
    public DbSet<SplitCurrencyExchange> SplitCurrencyExchanges => Set<SplitCurrencyExchange>();

    // ── Phase 3c: Partner Settlements ─────────────────────────
    public DbSet<PartnerSettlement> PartnerSettlements => Set<PartnerSettlement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Applies all configurations from the assembly (IEntityTypeConfiguration<T>)
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
