namespace ProjectLedger.API.Common.Exceptions;

/// <summary>
/// Exception thrown when the user has reached a numeric limit of their plan.
/// The GlobalExceptionHandler converts it into a 403 with a descriptive message.
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
