
namespace ProjectLedger.API.Services;

/// <summary>
/// Servicio de validación de acceso a proyectos.
/// Centraliza la lógica de verificación de membresía y rol.
/// Usado por Authorization Handlers y por la capa de servicios (validación imperativa).
/// </summary>
public interface IProjectAccessService
{
    /// <summary>
    /// Verifica si el usuario tiene al menos el rol especificado en el proyecto.
    /// Considera tanto ownership como membership.
    /// </summary>
    Task<bool> HasAccessAsync(
        Guid userId,
        Guid projectId,
        string minimumRole = ProjectRoles.Viewer,
        CancellationToken ct = default);

    /// <summary>
    /// Igual que HasAccessAsync pero lanza ForbiddenAccessException si no tiene acceso.
    /// Usar en la capa Application/Service para validación imperativa.
    /// </summary>
    Task ValidateAccessAsync(
        Guid userId,
        Guid projectId,
        string minimumRole = ProjectRoles.Viewer,
        CancellationToken ct = default);

    /// <summary>
    /// Obtiene el rol efectivo del usuario en el proyecto.
    /// Retorna null si no tiene acceso.
    /// </summary>
    Task<string?> GetUserRoleAsync(
        Guid userId,
        Guid projectId,
        CancellationToken ct = default);
}
