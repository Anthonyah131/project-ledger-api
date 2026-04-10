using ProjectLedger.API.Models;
using ProjectLedger.API.Repositories;

namespace ProjectLedger.API.Services;

/// <summary>
/// User service. Manages profile, soft delete, activation/deactivation.
/// Authentication (login/register) is handled in AuthService.
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
            ?? throw new KeyNotFoundException("UserNotFound");

        if (user.UsrIsDeleted)
            throw new InvalidOperationException("UserAlreadyDeleted");

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

    public async Task<(IReadOnlyList<User> Items, int TotalCount)> GetAllPagedAsync(
        bool includeDeleted, int skip, int take, string? sortBy, bool descending, CancellationToken ct = default)
        => await _userRepo.GetAllUsersPagedAsync(includeDeleted, skip, take, sortBy, descending, ct);

    public async Task<bool> ActivateAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _userRepo.GetByIdAsync(userId, ct);
        if (user is null || user.UsrIsDeleted) return false;
        if (user.UsrIsActive) return true; // Already active

        user.UsrIsActive = true;
        user.UsrUpdatedAt = DateTime.UtcNow;
        _userRepo.Update(user);
        await _userRepo.SaveChangesAsync(ct);

        // Notify the user via email
        _ = _emailService.SendAccountActivatedEmailAsync(user.UsrEmail, user.UsrFullName, ct);

        return true;
    }

    public async Task<bool> DeactivateAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _userRepo.GetByIdAsync(userId, ct);
        if (user is null || user.UsrIsDeleted) return false;
        if (!user.UsrIsActive) return true; // Already deactivated

        user.UsrIsActive = false;
        user.UsrUpdatedAt = DateTime.UtcNow;
        _userRepo.Update(user);
        await _userRepo.SaveChangesAsync(ct);

        // Notify the user via email
        _ = _emailService.SendAccountDeactivatedEmailAsync(user.UsrEmail, user.UsrFullName, ct);

        return true;
    }
}
