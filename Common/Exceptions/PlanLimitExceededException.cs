namespace ProjectLedger.API.Common.Exceptions;

/// <summary>
/// Excepción lanzada cuando el usuario ha alcanzado un límite numérico de su plan.
/// El GlobalExceptionHandler la convierte en un 403 con mensaje descriptivo.
/// </summary>
public class PlanLimitExceededException : Exception
{
    public string LimitName { get; }
    public int LimitValue { get; }
    public string PlanName { get; }

    public PlanLimitExceededException(string limitName, int limitValue, string planName)
        : base("PlanLimitExceeded")
    {
        LimitName = limitName;
        LimitValue = limitValue;
        PlanName = planName;
    }
}
