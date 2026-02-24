using Microsoft.EntityFrameworkCore;
using ProjectLedger.API.Data;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

public class CategoryRepository : Repository<Category>, ICategoryRepository
{
    public CategoryRepository(AppDbContext context) : base(context) { }

    public async Task<IEnumerable<Category>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default)
        => await DbSet
            .Where(c => c.CatProjectId == projectId && !c.CatIsDeleted)
            .OrderBy(c => c.CatName)
            .ToListAsync(ct);

    public async Task<Category?> GetDefaultByProjectIdAsync(Guid projectId, CancellationToken ct = default)
        => await DbSet.FirstOrDefaultAsync(
            c => c.CatProjectId == projectId && c.CatIsDefault && !c.CatIsDeleted, ct);
}
