namespace GreenTransit.Domain.Exceptions;

/// <summary>
/// Excepción de dominio para reglas de negocio violadas.
/// Se captura en el pipeline de MediatR / UI para mostrar mensajes claros al usuario.
/// </summary>
public sealed class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
    public DomainException(string message, Exception inner) : base(message, inner) { }
}
