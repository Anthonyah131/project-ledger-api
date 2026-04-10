namespace ProjectLedger.API.DTOs.Report;

// ── Category Growth ─────────────────────────────────────────

/// <summary>Growth envelope by category between two months.</summary>
public class CategoryGrowthEnvelopeResponse
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = null!;
    public string CurrencyCode { get; set; } = null!;
    public string CurrentMonthLabel { get; set; } = null!;
    public string PreviousMonthLabel { get; set; } = null!;
    public DateTime GeneratedAt { get; set; }
    public List<CategoryGrowthResponse> Categories { get; set; } = [];
}

/// <summary>Growth by category between two months.</summary>
public class CategoryGrowthResponse
{
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = null!;
    public decimal CurrentMonthAmount { get; set; }
    public decimal PreviousMonthAmount { get; set; }
    public decimal GrowthAmount { get; set; }

    /// <summary>Growth percentage. null if previous = 0.</summary>
    public decimal? GrowthPercentage { get; set; }

    public int CurrentMonthCount { get; set; }
    public int PreviousMonthCount { get; set; }
    public decimal AverageAmountCurrent { get; set; }
    public decimal AverageAmountPrevious { get; set; }

    /// <summary>true when previousMonthAmount == 0 and currentMonthAmount > 0.</summary>
    public bool IsNew { get; set; }

    /// <summary>true when currentMonthAmount == 0 and previousMonthAmount > 0.</summary>
    public bool IsDisappeared { get; set; }
}
