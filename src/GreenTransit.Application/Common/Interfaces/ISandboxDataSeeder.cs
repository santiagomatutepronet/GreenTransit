namespace GreenTransit.Application.Common.Interfaces;

/// <summary>
/// Servicio de seed de datos sandbox para demostración.
/// Idempotente: usa SourceSystem='SEED' como discriminador.
/// </summary>
public interface ISandboxDataSeeder
{
    /// <summary>
    /// Inserta datos de demostración en orden estricto de dependencias FK.
    /// Si ya existen datos SEED, hace skip de las fases correspondientes.
    /// </summary>
    Task SeedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Elimina todos los datos sandbox (SourceSystem='SEED') en orden inverso de FK.
    /// </summary>
    Task CleanAsync(CancellationToken cancellationToken = default);
}
