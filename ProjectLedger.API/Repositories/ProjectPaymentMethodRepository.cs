using Microsoft.EntityFrameworkCore;
using ProjectLedger.API.Data;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

public class ProjectPaymentMethodRepository : Repository<ProjectPaymentMethod>, IProjectPaymentMethodRepository
{
    public ProjectPaymentMethodRepository(AppDbContext context) : base(context) { }

    public async Task<IEnumerable<ProjectPaymentMethod>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default)
        => await DbSet
            .Include(ppm => ppm.PaymentMethod)
                .ThenInclude(pm => pm.OwnerUser)
            .Where(ppm => ppm.PpmProjectId == projectId
                       && !ppm.PaymentMethod.PmtIsDeleted)
            .ToListAsync(ct);

    public async Task<ProjectPaymentMethod?> GetByProjectAndPaymentMethodAsync(
        Guid projectId, Guid paymentMethodId, CancellationToken ct = default)
        => await DbSet.FirstOrDefaultAsync(
            ppm => ppm.PpmProjectId == projectId
                && ppm.PpmPaymentMethodId == paymentMethodId, ct);

    public async Task<bool> IsPaymentMethodLinkedToProjectAsync(
        Guid projectId, Guid paymentMethodId, CancellationToken ct = default)
        => await DbSet.AnyAsync(
            ppm => ppm.PpmProjectId == projectId
                && ppm.PpmPaymentMethodId == paymentMethodId, ct);
}
