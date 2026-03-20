using ProjectLedger.API.DTOs.Common;
using ProjectLedger.API.DTOs.Partner;

namespace ProjectLedger.API.DTOs.Report;

// ── Partner General Report ───────────────────────────────────

/// <summary>
/// Reporte general de un partner: actividad consolidada por proyecto y método de pago.
/// Los montos por proyecto están en la moneda base de cada proyecto.
/// Los montos por método de pago están en la moneda del método de pago.
/// </summary>
public class PartnerGeneralReportResponse
{
    public Guid PartnerId { get; set; }
    public string PartnerName { get; set; } = null!;
    public string? PartnerEmail { get; set; }
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
    public DateTime GeneratedAt { get; set; }

    public List<PartnerProjectSummary> Projects { get; set; } = [];
    public List<PartnerPaymentMethodSummary> PaymentMethods { get; set; } = [];
}

/// <summary>
/// Actividad del partner en un proyecto específico.
/// Todos los montos están en la moneda base del proyecto.
/// Las monedas alternativas se muestran por transacción solo para referencia.
/// </summary>
public class PartnerProjectSummary
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = null!;
    public string CurrencyCode { get; set; } = null!;

    // Balances en moneda base del proyecto (misma lógica que /partners/balance)
    public decimal PaidPhysically { get; set; }
    public decimal OthersOweHim { get; set; }
    public decimal HeOwesOthers { get; set; }
    public decimal SettlementsReceived { get; set; }
    public decimal SettlementsPaid { get; set; }
    public decimal NetBalance { get; set; }

    /// <summary>Mismos valores expresados en monedas alternativas del proyecto.</summary>
    public List<PartnerCurrencyTotal> CurrencyTotals { get; set; } = [];

    public List<PartnerProjectTransaction> Transactions { get; set; } = [];
    public List<PartnerProjectSettlement> Settlements { get; set; } = [];
}

/// <summary>
/// Una transacción (gasto o ingreso) donde el partner tiene un split.
/// SplitAmount está en la moneda base del proyecto.
/// CurrencyExchanges son los montos alternativos del split — solo para lectura.
/// </summary>
public class PartnerProjectTransaction
{
    public Guid TransactionId { get; set; }
    public string Type { get; set; } = null!;           // "expense" | "income"
    public string Title { get; set; } = null!;
    public DateOnly Date { get; set; }
    public string? Category { get; set; }
    public string? PaymentMethodName { get; set; }
    public string? PayingPartnerName { get; set; }
    public decimal SplitAmount { get; set; }             // en moneda base del proyecto
    public string SplitType { get; set; } = null!;
    public decimal SplitValue { get; set; }
    public List<SplitCurrencyExchangeItem> CurrencyExchanges { get; set; } = [];
}

public class PartnerProjectSettlement
{
    public Guid SettlementId { get; set; }
    public DateOnly Date { get; set; }
    public string Direction { get; set; } = null!;       // "paid_to" | "received_from"
    public string OtherPartnerName { get; set; } = null!;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = null!;
    public decimal ConvertedAmount { get; set; }         // en moneda base del proyecto
    public List<CurrencyExchangeResponse> CurrencyExchanges { get; set; } = [];
}

/// <summary>
/// Actividad del partner a través de un método de pago.
/// Todos los montos están en la moneda del método de pago.
/// </summary>
public class PartnerPaymentMethodSummary
{
    public Guid PaymentMethodId { get; set; }
    public string PaymentMethodName { get; set; } = null!;
    public string Currency { get; set; } = null!;
    public string? BankName { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal TotalIncomes { get; set; }
    public decimal NetFlow { get; set; }                 // TotalIncomes - TotalExpenses
    public int TransactionCount { get; set; }
}
