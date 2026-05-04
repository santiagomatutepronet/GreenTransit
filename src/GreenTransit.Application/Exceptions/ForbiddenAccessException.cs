namespace GreenTransit.Application.Exceptions;

/// <summary>
/// Se lanza cuando un usuario autenticado intenta ejecutar una operación
/// para la que no tiene permisos (HTTP 403 Forbidden).
/// Diferente a UnauthorizedAccessException (HTTP 401 — no autenticado).
/// </summary>
public sealed class ForbiddenAccessException : Exception
{
    public ForbiddenAccessException()
        : base("Acceso denegado. No tiene permisos para realizar esta operación.") { }

    public ForbiddenAccessException(string message)
        : base(message) { }

    public ForbiddenAccessException(string message, Exception inner)
        : base(message, inner) { }
}
