using Microsoft.EntityFrameworkCore;
using ProjectLedger.API.Data;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

public class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(AppDbContext context) : base(context) { }

    public override async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await DbSet
            .Include(u => u.Plan)
            .FirstOrDefaultAsync(u => u.UsrId == id && !u.UsrIsDeleted, ct);

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
        => await DbSet.FirstOrDefaultAsync(
            u => u.UsrEmail == email && !u.UsrIsDeleted, ct);

    public async Task<bool> EmailExistsAsync(string email, CancellationToken ct = default)
        => await DbSet.AnyAsync(
            u => u.UsrEmail == email && !u.UsrIsDeleted, ct);

    public async Task<IReadOnlyList<User>> GetAllUsersAsync(bool includeDeleted = false, CancellationToken ct = default)
        => await DbSet
            .Where(u => includeDeleted || !u.UsrIsDeleted)
            .Include(u => u.Plan)
            .OrderByDescending(u => u.UsrCreatedAt)
            .ToListAsync(ct);

    public async Task<(IReadOnlyList<User> Items, int TotalCount)> GetAllUsersPagedAsync(
        bool includeDeleted, int skip, int take, string? sortBy, bool descending, CancellationToken ct = default)
    {
        var query = DbSet
            .Include(u => u.Plan)
            .Where(u => includeDeleted || !u.UsrIsDeleted);

        var totalCount = await query.CountAsync(ct);

        query = sortBy?.ToLowerInvariant() switch
        {
            "email" => descending ? query.OrderByDescending(u => u.UsrEmail) : query.OrderBy(u => u.UsrEmail),
            "fullname" => descending ? query.OrderByDescending(u => u.UsrFullName) : query.OrderBy(u => u.UsrFullName),
            "lastlogin" => descending ? query.OrderByDescending(u => u.UsrLastLoginAt) : query.OrderBy(u => u.UsrLastLoginAt),
            _ => descending ? query.OrderByDescending(u => u.UsrCreatedAt) : query.OrderBy(u => u.UsrCreatedAt),
        };

        var items = await query.Skip(skip).Take(take).ToListAsync(ct);
        return (items, totalCount);
    }
}
