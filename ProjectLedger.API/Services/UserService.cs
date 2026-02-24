using ProjectLedger.API.Models;
using ProjectLedger.API.Repositories;

namespace ProjectLedger.API.Services;

/// <summary>
/// Servicio de usuarios. Gestión de perfil, soft delete.
/// La autenticación (login/register) se maneja en AuthService.
/// </summary>
public class UserService : IUserService
{
    private readonly IUserRepository _userRepo;

    public UserService(IUserRepository userRepo)
    {
        _userRepo = userRepo;
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _userRepo.GetByIdAsync(id, ct);
        return user is { UsrIsDeleted: false } ? user : null;
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
        => await _userRepo.GetByEmailAsync(email.ToLowerInvariant(), ct);

    public async Task<User> CreateAsync(User user, CancellationToken ct = default)
    {
        user.UsrCreatedAt = DateTime.UtcNow;
        user.UsrUpdatedAt = DateTime.UtcNow;

        await _userRepo.AddAsync(user, ct);
        await _userRepo.SaveChangesAsync(ct);
        return user;
    }

    public async Task UpdateAsync(User user, CancellationToken ct = default)
    {
        user.UsrUpdatedAt = DateTime.UtcNow;
        _userRepo.Update(user);
        await _userRepo.SaveChangesAsync(ct);
    }

    public async Task SoftDeleteAsync(Guid id, Guid deletedByUserId, CancellationToken ct = default)
    {
        var user = await _userRepo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"User '{id}' not found.");

        if (user.UsrIsDeleted)
            throw new InvalidOperationException("User is already deleted.");

        user.UsrIsDeleted = true;
        user.UsrDeletedAt = DateTime.UtcNow;
        user.UsrDeletedByUserId = deletedByUserId;
        user.UsrIsActive = false;
        user.UsrUpdatedAt = DateTime.UtcNow;

        _userRepo.Update(user);
        await _userRepo.SaveChangesAsync(ct);
    }
}
