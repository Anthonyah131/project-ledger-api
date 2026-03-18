namespace ProjectLedger.API.Repositories;

/// <summary>
/// Repositorio genérico con operaciones CRUD base.
/// Todas las entidades del sistema implementan este contrato.
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
    /// Ejecuta una operación dentro de una transacción explícita, compatible
    /// con NpgsqlRetryingExecutionStrategy. Toda la operación se reintenta
    /// como unidad atómica si ocurre un error transitorio.
    /// </summary>
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken ct = default);
}
