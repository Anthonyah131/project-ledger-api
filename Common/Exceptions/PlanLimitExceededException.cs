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
        : base($"You have reached the limit of {limitValue} for '{limitName}' on your '{planName}' plan. Please upgrade your plan.")
    {
        LimitName = limitName;
        LimitValue = limitValue;
        PlanName = planName;
    }
}
