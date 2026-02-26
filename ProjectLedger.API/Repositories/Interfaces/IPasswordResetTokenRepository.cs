using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

public interface IPasswordResetTokenRepository : IRepository<PasswordResetToken>
{
    /// <summary>Busca un token activo (no usado, no expirado) por hash del código.</summary>
    Task<PasswordResetToken?> GetActiveByCodeHashAsync(string codeHash, CancellationToken ct = default);

    /// <summary>Invalida (marca como usados) todos los tokens activos del usuario.</summary>
    Task InvalidateAllByUserIdAsync(Guid userId, CancellationToken ct = default);
}
