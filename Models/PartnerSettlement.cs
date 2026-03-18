namespace ProjectLedger.API.Models;

/// <summary>
/// Liquidación directa entre dos partners de un proyecto.
/// Registra pagos que saldan deudas sin pasar por métodos de pago del proyecto.
/// </summary>
public class PartnerSettlement
{
    public Guid PstId { get; set; }
    public Guid PstProjectId { get; set; }
    public Guid PstFromPartnerId { get; set; }
    public Guid PstToPartnerId { get; set; }
    public decimal PstAmount { get; set; }
    public string PstCurrency { get; set; } = null!;
    public decimal PstExchangeRate { get; set; } = 1m;
    public decimal PstConvertedAmount { get; set; }
    public DateOnly PstSettlementDate { get; set; }
    public string? PstDescription { get; set; }
    public string? PstNotes { get; set; }
    public Guid PstCreatedByUserId { get; set; }
    public DateTime PstCreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime PstUpdatedAt { get; set; } = DateTime.UtcNow;

    // ── Soft delete ─────────────────────────────────────────
    public bool PstIsDeleted { get; set; }
    public DateTime? PstDeletedAt { get; set; }
    public Guid? PstDeletedByUserId { get; set; }

    // ── Navigation properties ───────────────────────────────
    public Project Project { get; set; } = null!;
    public Partner FromPartner { get; set; } = null!;
    public Partner ToPartner { get; set; } = null!;
    public Currency Currency { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;
    public User? DeletedByUser { get; set; }
    public ICollection<SplitCurrencyExchange> CurrencyExchanges { get; set; } = [];
}
