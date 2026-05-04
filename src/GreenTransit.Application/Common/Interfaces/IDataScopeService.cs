using GreenTransit.Domain.Entities;

namespace GreenTransit.Application.Common.Interfaces;

/// <summary>
/// Servicio que aplica el filtrado de datos por perfil del usuario sobre IQueryable.
/// Responde a la pregunta: "¿Este usuario ve TODOS los datos del tenant o solo los SUYOS?"
///
/// El filtro de OwnerId (multi-tenant) se aplica por separado; este servicio añade
/// únicamente el filtrado por entidad vinculada según las reglas del §3.2 del
/// Mapa de Autorización.
///
/// Todos los métodos ApplyScope son composables y NO materializan la query.
/// </summary>
public interface IDataScopeService
{
    /// <summary>
    /// Indica si el usuario tiene acceso completo sobre el área funcional indicada
    /// (ve todos los datos del tenant) o solo los datos vinculados a su entidad.
    /// </summary>
    /// <param name="functionalArea">
    /// Identificador del área funcional. Usar las constantes de <see cref="DataScopeAreas"/>.
    /// </param>
    bool HasFullAccess(string functionalArea);

    /// <summary>
    /// Devuelve el <see cref="Guid"/> de la entidad vinculada al usuario para filtrar,
    /// o <c>null</c> si tiene acceso completo en el área indicada.
    /// </summary>
    Guid? GetEntityFilter(string functionalArea);

    /// <summary>
    /// Aplica el filtrado de ámbito sobre <see cref="ServiceOrder"/>.
    /// PRODUCER y PUBLIC_ENT → WHERE IdIssuedBy = LinkedEntityId.
    /// Resto → sin filtro adicional.
    /// </summary>
    IQueryable<ServiceOrder> ApplyScope(IQueryable<ServiceOrder> query);

    /// <summary>
    /// Aplica el filtrado de ámbito sobre <see cref="WasteMove"/>.
    /// CARRIER → WHERE EXISTS (WasteMoveResidues WHERE IdCarrier = LinkedEntityId).
    /// Resto → sin filtro adicional.
    /// </summary>
    IQueryable<WasteMove> ApplyScope(IQueryable<WasteMove> query);

    /// <summary>
    /// Aplica el filtrado de ámbito sobre <see cref="EntryPlant"/>.
    /// PLANT_OP → WHERE OwnerId = currentOwnerId (el OwnerId ya actúa de identificador de planta).
    /// Resto → sin filtro adicional.
    /// </summary>
    IQueryable<EntryPlant> ApplyScope(IQueryable<EntryPlant> query);

    /// <summary>
    /// Aplica el filtrado de ámbito sobre <see cref="EntryCAC"/>.
    /// CAC_OP → WHERE OwnerId = currentOwnerId.
    /// Resto → sin filtro adicional.
    /// </summary>
    IQueryable<EntryCAC> ApplyScope(IQueryable<EntryCAC> query);

    /// <summary>
    /// Aplica el filtrado de ámbito sobre <see cref="TreatmentPlant"/>.
    /// PLANT_OP → WHERE OwnerId = currentOwnerId.
    /// Resto → sin filtro adicional.
    /// </summary>
    IQueryable<TreatmentPlant> ApplyScope(IQueryable<TreatmentPlant> query);

    /// <summary>
    /// Aplica el filtrado de ámbito sobre <see cref="Incident"/>.
    /// Sin filtro adicional por perfil; todos los perfiles ven todas las incidencias del tenant.
    /// </summary>
    IQueryable<Incident> ApplyScope(IQueryable<Incident> query);

    /// <summary>
    /// Aplica el filtrado de ámbito sobre <see cref="Residue"/>.
    /// PRODUCER → WHERE IdProducer = LinkedEntityId AND ResidueType IN ('Product','ProductSpec').
    /// Resto → sin filtro adicional.
    /// </summary>
    IQueryable<Residue> ApplyScope(IQueryable<Residue> query);
}

/// <summary>
/// Constantes que identifican las áreas funcionales para <see cref="IDataScopeService"/>.
/// </summary>
public static class DataScopeAreas
{
    public const string ServiceOrders   = "ServiceOrders";
    public const string WasteMoves      = "WasteMoves";
    public const string EntryPlants     = "EntryPlants";
    public const string EntryCACs       = "EntryCACs";
    public const string TreatmentPlants = "TreatmentPlants";
    public const string Incidents       = "Incidents";
    public const string Residues        = "Residues";
}
