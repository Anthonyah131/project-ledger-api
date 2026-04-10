using Microsoft.EntityFrameworkCore;
using ProjectLedger.API.Data;

namespace ProjectLedger.API.Repositories;

/// <summary>
/// Generic base implementation of IRepository&lt;T&gt; using EF Core.
/// All entities inherit from here to reuse CRUD operations.
/// </summary>
public class Repository<T> : IRepository<T> where T : class
{
    protected readonly AppDbContext Context;
    protected readonly DbSet<T> DbSet;

    public Repository(AppDbContext context)
    {
        Context = context;
        DbSet = context.Set<T>();
    }

    public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await DbSet.FindAsync([id], ct);

    public virtual async Task<IEnumerable<T>> GetAllAsync(CancellationToken ct = default)
        => await DbSet.ToListAsync(ct);

    public virtual async Task AddAsync(T entity, CancellationToken ct = default)
        => await DbSet.AddAsync(entity, ct);

    public virtual void Update(T entity)
        => DbSet.Update(entity);

    public virtual void Remove(T entity)
        => DbSet.Remove(entity);

    public virtual async Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
        => await DbSet.FindAsync([id], ct) != null;

    public virtual async Task SaveChangesAsync(CancellationToken ct = default)
        => await Context.SaveChangesAsync(ct);

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken ct = default)
    {
        var strategy = Context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async (cancellationToken) =>
        {
            await using var transaction = await Context.Database.BeginTransactionAsync(cancellationToken);
            await operation(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }, ct);
    }
}
