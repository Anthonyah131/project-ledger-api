namespace ProjectLedger.API.Common.Exceptions;

/// <summary>
/// Excepción lanzada cuando un usuario intenta acceder a un recurso
/// para el cual no tiene permisos suficientes.
/// Capturada por el GlobalExceptionHandlerMiddleware → HTTP 403.
/// </summary>
public class ForbiddenAccessException : Exception
{
    public ForbiddenAccessException()
        : base("ForbiddenResource") { }

    public ForbiddenAccessException(string message)
        : base(message) { }

    public ForbiddenAccessException(string message, Exception innerException)
        : base(message, innerException) { }
}
