namespace ProjectLedger.API.Common.Exceptions;

/// <summary>
/// Exception thrown when a user attempts to access a resource
/// for which they do not have sufficient permissions.
/// Caught by GlobalExceptionHandlerMiddleware → HTTP 403.
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
