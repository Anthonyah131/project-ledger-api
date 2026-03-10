namespace ProjectLedger.API.Models;

/// <summary>
/// Vinculación de un método de pago a un proyecto.
/// Permite que miembros de un proyecto compartido usen los métodos de pago vinculados.
/// Sin soft delete: se elimina físicamente al desvincular.
/// </summary>
public class ProjectPaymentMethod
{
    public Guid PpmId { get; set; }
    public Guid PpmProjectId { get; set; }
    public Guid PpmPaymentMethodId { get; set; }
    public Guid PpmAddedByUserId { get; set; }
    public DateTime PpmCreatedAt { get; set; } = DateTime.UtcNow;

    // ── Navigation properties ───────────────────────────────
    public Project Project { get; set; } = null!;
    public PaymentMethod PaymentMethod { get; set; } = null!;
    public User AddedByUser { get; set; } = null!;
}
