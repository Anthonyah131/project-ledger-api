namespace ProjectLedger.API.Common.Exceptions;

/// <summary>
/// Exception thrown when the user's plan does not allow an action.
/// The GlobalExceptionHandler converts it into a 403 with a descriptive message.
/// </summary>
public class PlanDeniedException : Exception
{
    public PlanPermission? Permission { get; }
    public string PlanName { get; } = string.Empty;

    public PlanDeniedException(PlanPermission permission, string planName)
        : base("PlanDenied")
    {
        Permission = permission;
        PlanName = planName;
    }

    public PlanDeniedException(string message) : base(message) { }
}
