namespace ProjectLedger.API.Common.Exceptions;

/// <summary>
/// Excepción lanzada cuando el plan del usuario no permite ejecutar una acción.
/// El GlobalExceptionHandler la convierte en un 403 con mensaje descriptivo.
/// </summary>
public class PlanDeniedException : Exception
{
    public PlanPermission? Permission { get; }
    public string PlanName { get; } = string.Empty;

    public PlanDeniedException(PlanPermission permission, string planName)
        : base($"Your current plan '{planName}' does not include the '{permission}' feature. Please upgrade your plan.")
    {
        Permission = permission;
        PlanName = planName;
    }

    public PlanDeniedException(string message) : base(message) { }
}
