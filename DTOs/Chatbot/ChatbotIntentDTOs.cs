using System.Text.Json.Serialization;

namespace ProjectLedger.API.DTOs.Chatbot;

/// <summary>
/// Resultado del parsing de intención por el LLM.
/// El parser siempre devuelve JSON con esta estructura.
/// </summary>
public class ParsedIntent
{
    /// <summary>Dominio detectado: expenses, income, projects, payments, obligations, partners, summary, context_only, greeting, off_topic.</summary>
    [JsonPropertyName("domain")]
    public string Domain { get; set; } = string.Empty;

    /// <summary>Acción dentro del dominio (e.g. totals, by_category, portfolio).</summary>
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    /// <summary>Filtros extraídos del lenguaje natural del usuario.</summary>
    [JsonPropertyName("filters")]
    public IntentFilters Filters { get; set; } = new();

    /// <summary>Idioma detectado del usuario: "es" o "en".</summary>
    [JsonPropertyName("lang")]
    public string Lang { get; set; } = "es";
}

/// <summary>
/// Filtros opcionales extraídos del mensaje del usuario.
/// Todos nullable — el LLM solo llena los que detecta.
/// </summary>
public class IntentFilters
{
    [JsonPropertyName("projectName")]
    public string? ProjectName { get; set; }

    [JsonPropertyName("categoryName")]
    public string? CategoryName { get; set; }

    [JsonPropertyName("partnerName")]
    public string? PartnerName { get; set; }

    [JsonPropertyName("paymentMethodName")]
    public string? PaymentMethodName { get; set; }

    /// <summary>"expense", "income", or null for both. Used for movements/recent.</summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("from")]
    public string? From { get; set; }

    [JsonPropertyName("to")]
    public string? To { get; set; }

    [JsonPropertyName("month")]
    public string? Month { get; set; }

    [JsonPropertyName("granularity")]
    public string? Granularity { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("search")]
    public string? Search { get; set; }

    [JsonPropertyName("top")]
    public int? Top { get; set; }

    [JsonPropertyName("comparePreviousPeriod")]
    public bool? ComparePreviousPeriod { get; set; }

    [JsonPropertyName("minPriority")]
    public int? MinPriority { get; set; }

    [JsonPropertyName("dueWithinDays")]
    public int? DueWithinDays { get; set; }

    [JsonPropertyName("overdueDaysMin")]
    public int? OverdueDaysMin { get; set; }

    [JsonPropertyName("minRemainingAmount")]
    public decimal? MinRemainingAmount { get; set; }

    [JsonPropertyName("activityDays")]
    public int? ActivityDays { get; set; }

    [JsonPropertyName("includeBudgetContext")]
    public bool? IncludeBudgetContext { get; set; }

    [JsonPropertyName("includeOthers")]
    public bool? IncludeOthers { get; set; }

    [JsonPropertyName("includeTrend")]
    public bool? IncludeTrend { get; set; }
}
