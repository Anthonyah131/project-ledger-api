using ProjectLedger.API.Models;
using ProjectLedger.API.Repositories;

namespace ProjectLedger.API.Services;

/// <summary>
/// Servicio de usuarios. Gestión de perfil, soft delete, activación/desactivación.
/// La autenticación (login/register) se maneja en AuthService.
/// </summary>
public class UserService : IUserService
{
    private readonly IUserRepository _userRepo;
    private readonly IEmailService _emailService;

    public UserService(IUserRepository userRepo, IEmailService emailService)
    {
        _userRepo = userRepo;
        _emailService = emailService;
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

    // ── Admin operations ────────────────────────────────────

    public async Task<IReadOnlyList<User>> GetAllAsync(bool includeDeleted = false, CancellationToken ct = default)
        => await _userRepo.GetAllUsersAsync(includeDeleted, ct);

    public async Task<bool> ActivateAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _userRepo.GetByIdAsync(userId, ct);
        if (user is null || user.UsrIsDeleted) return false;
        if (user.UsrIsActive) return true; // Ya está activo

        user.UsrIsActive = true;
        user.UsrUpdatedAt = DateTime.UtcNow;
        _userRepo.Update(user);
        await _userRepo.SaveChangesAsync(ct);

        // Notificar al usuario por correo
        _ = _emailService.SendAccountActivatedEmailAsync(user.UsrEmail, user.UsrFullName, ct);

        return true;
    }

    public async Task<bool> DeactivateAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _userRepo.GetByIdAsync(userId, ct);
        if (user is null || user.UsrIsDeleted) return false;
        if (!user.UsrIsActive) return true; // Ya está desactivado

        user.UsrIsActive = false;
        user.UsrUpdatedAt = DateTime.UtcNow;
        _userRepo.Update(user);
        await _userRepo.SaveChangesAsync(ct);

        // Notificar al usuario por correo
        _ = _emailService.SendAccountDeactivatedEmailAsync(user.UsrEmail, user.UsrFullName, ct);

        return true;
    }
}
