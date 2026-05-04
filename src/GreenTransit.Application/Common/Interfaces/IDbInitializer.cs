namespace GreenTransit.Application.Common.Interfaces;

/// <summary>
/// Inicializa y aplica el seed de datos necesarios para el funcionamiento del sistema.
/// Se ejecuta una vez en el arranque de la aplicación.
/// </summary>
public interface IDbInitializer
{
    /// <summary>
    /// Aplica migraciones pendientes y ejecuta el seed idempotente de datos maestros.
    /// Seguro para ejecutar en cada arranque — no inserta duplicados.
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
