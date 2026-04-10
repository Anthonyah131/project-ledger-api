namespace ProjectLedger.API.Repositories;

/// <summary>
/// Generic repository with base CRUD operations.
/// All domain entities in the system implement this contract.
/// </summary>
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<T>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    void Update(T entity);
    void Remove(T entity);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Executes an operation within an explicit transaction, compatible
    /// with NpgsqlRetryingExecutionStrategy. The entire operation is retried
    /// as an atomic unit if a transient error occurs.
    /// </summary>
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken ct = default);
}
