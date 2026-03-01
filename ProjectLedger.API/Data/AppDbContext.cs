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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Aplica todas las configuraciones del ensamblado (IEntityTypeConfiguration<T>)
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
