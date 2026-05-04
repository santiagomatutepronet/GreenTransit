using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Domain.Authorization;
using GreenTransit.Domain.Entities;

namespace GreenTransit.Infrastructure.Services;

/// <summary>
/// Implementación de IDataScopeService. Aplica el filtrado de datos por perfil del usuario
/// siguiendo las reglas del §3.2 del Mapa de Autorización.
///
/// Scoped: una instancia por request HTTP. Los valores del usuario se leen una sola vez
/// desde ICurrentUserService (que ya está cacheado en claims).
/// </summary>
public sealed class DataScopeService : IDataScopeService
{
    private readonly ICurrentUserService _currentUser;

    public DataScopeService(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    // ─── HasFullAccess ────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public bool HasFullAccess(string functionalArea) =>
        GetEntityFilter(functionalArea) is null;

    // ─── GetEntityFilter ──────────────────────────────────────────────────────

    /// <inheritdoc/>
    public Guid? GetEntityFilter(string functionalArea)
    {
        // ADMIN y DISPATCH_OFFICE siempre tienen acceso completo.
        if (_currentUser.IsInAnyProfile(ProfileConstants.Admin, ProfileConstants.DispatchOffice))
            return null;

        return functionalArea switch
        {
            DataScopeAreas.ServiceOrders =>
                // PRODUCER y PUBLIC_ENT ven solo las SOs que emitieron.
                _currentUser.IsInAnyProfile(ProfileConstants.Producer, ProfileConstants.PublicEnt)
                    ? _currentUser.LinkedEntityId
                    : null,

            DataScopeAreas.WasteMoves =>
                // CARRIER ve solo los traslados donde figura como transportista.
                _currentUser.IsInProfile(ProfileConstants.Carrier)
                    ? _currentUser.LinkedEntityId
                    : null,

            DataScopeAreas.Residues =>
                // PRODUCER ve solo sus Product/ProductSpec.
                _currentUser.IsInProfile(ProfileConstants.Producer)
                    ? _currentUser.LinkedEntityId
                    : null,

            // EntryPlants, EntryCACs, TreatmentPlants: el filtro por OwnerId ya garantiza
            // que PLANT_OP/CAC_OP solo ven los registros de su entidad (mismo tenant).
            DataScopeAreas.EntryPlants or
            DataScopeAreas.EntryCACs or
            DataScopeAreas.TreatmentPlants or
            DataScopeAreas.Incidents => null,

            _ => null
        };
    }

    // ─── ApplyScope ───────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public IQueryable<ServiceOrder> ApplyScope(IQueryable<ServiceOrder> query)
    {
        var entityId = GetEntityFilter(DataScopeAreas.ServiceOrders);
        // Filtrar por la entidad que emitió la orden de servicio.
        return entityId.HasValue
            ? query.Where(so => so.IdIssuedBy == entityId)
            : query;
    }

    /// <inheritdoc/>
    public IQueryable<WasteMove> ApplyScope(IQueryable<WasteMove> query)
    {
        var entityId = GetEntityFilter(DataScopeAreas.WasteMoves);
        // CARRIER: solo traslados donde figura como transportista en al menos una línea.
        return entityId.HasValue
            ? query.Where(wm => wm.WasteMoveResidues.Any(r => r.IdCarrier == entityId))
            : query;
    }

    /// <inheritdoc/>
    public IQueryable<EntryPlant> ApplyScope(IQueryable<EntryPlant> query)
    {
        // El filtro por OwnerId ya restringe las entradas a las de la planta del tenant.
        // No se aplica filtro adicional por LinkedEntityId.
        return query;
    }

    /// <inheritdoc/>
    public IQueryable<EntryCAC> ApplyScope(IQueryable<EntryCAC> query)
    {
        // Igual que EntryPlants: OwnerId ya actúa de filtro suficiente para CAC_OP.
        return query;
    }

    /// <inheritdoc/>
    public IQueryable<TreatmentPlant> ApplyScope(IQueryable<TreatmentPlant> query)
    {
        // Igual que EntryPlants.
        return query;
    }

    /// <inheritdoc/>
    public IQueryable<Incident> ApplyScope(IQueryable<Incident> query)
    {
        // Sin filtro adicional por perfil: todos los perfiles ven todas las incidencias del tenant.
        return query;
    }

    /// <inheritdoc/>
    public IQueryable<Residue> ApplyScope(IQueryable<Residue> query)
    {
        var entityId = GetEntityFilter(DataScopeAreas.Residues);
        // PRODUCER: solo sus Product y ProductSpec (no los Waste operativos del tenant).
        return entityId.HasValue
            ? query.Where(r => r.IdProducer == entityId
                            && (r.ResidueType == "Product" || r.ResidueType == "ProductSpec"))
            : query;
    }
}
