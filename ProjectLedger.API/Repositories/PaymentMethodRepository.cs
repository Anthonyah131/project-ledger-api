using Microsoft.EntityFrameworkCore;
using ProjectLedger.API.Data;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

public class PaymentMethodRepository : Repository<PaymentMethod>, IPaymentMethodRepository
{
    public PaymentMethodRepository(AppDbContext context) : base(context) { }

    public async Task<IEnumerable<PaymentMethod>> GetByOwnerUserIdAsync(Guid userId, CancellationToken ct = default)
        => await DbSet
            .Where(pm => pm.PmtOwnerUserId == userId && !pm.PmtIsDeleted)
            .OrderBy(pm => pm.PmtName)
            .ToListAsync(ct);
}
