namespace GreenTransit.Application.Common.Interfaces;

/// <summary>
/// Marcador opt-in para commands que requieren una transacción de base de datos explícita.
/// Solo se debe aplicar a commands que realicen múltiples operaciones de escritura
/// que deban ser atómicas (o que mezclen escrituras en varias tablas).
///
/// Uso:
/// public sealed record GenerateSettlementCommand(...) : IRequest&lt;Guid&gt;, ITransactional;
/// </summary>
public interface ITransactional { }
