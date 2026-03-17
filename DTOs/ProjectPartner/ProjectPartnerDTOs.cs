using System.ComponentModel.DataAnnotations;

namespace ProjectLedger.API.DTOs.ProjectPartner;

// ── Requests ────────────────────────────────────────────────

/// <summary>Request para asignar un partner a un proyecto.</summary>
public class AddProjectPartnerRequest
{
    [Required]
    public Guid PartnerId { get; set; }
}

// ── Responses ───────────────────────────────────────────────

/// <summary>Partner asignado a un proyecto.</summary>
public class ProjectPartnerResponse
{
    public Guid Id { get; set; }
    public Guid PartnerId { get; set; }
    public string PartnerName { get; set; } = null!;
    public string? PartnerEmail { get; set; }
    public DateTime AddedAt { get; set; }
}

/// <summary>Métodos de pago disponibles en un proyecto, agrupados por partner.</summary>
public class AvailablePaymentMethodsResponse
{
    public Guid ProjectId { get; set; }
    /// <summary>Métodos de pago del usuario que no tienen partner asignado — disponibles en cualquier proyecto.</summary>
    public IReadOnlyList<ProjectPaymentMethodItem> UnpartneredPaymentMethods { get; set; } = [];
    /// <summary>Partners asignados al proyecto con sus métodos de pago.</summary>
    public IReadOnlyList<PartnerWithPaymentMethods> Partners { get; set; } = [];
}

/// <summary>Partner con sus métodos de pago disponibles en el contexto del proyecto.</summary>
public class PartnerWithPaymentMethods
{
    public Guid PartnerId { get; set; }
    public string PartnerName { get; set; } = null!;
    public IReadOnlyList<ProjectPaymentMethodItem> PaymentMethods { get; set; } = [];
}

/// <summary>Método de pago disponible en el proyecto.</summary>
public class ProjectPaymentMethodItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Type { get; set; } = null!;
    public string Currency { get; set; } = null!;
    public string? BankName { get; set; }
}
