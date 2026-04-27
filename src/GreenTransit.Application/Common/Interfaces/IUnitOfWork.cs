using GreenTransit.Domain.Entities;

namespace GreenTransit.Application.Common.Interfaces;

/// <summary>
/// Unidad de trabajo: agrupa repositorios tipados y persiste todos los cambios
/// en una única transacción de base de datos.
/// </summary>
public interface IUnitOfWork
{
    IRepository<BusinessEntity> BusinessEntities { get; }
    IRepository<AppUser>        AppUsers         { get; }
    IRepository<ServiceOrder>   ServiceOrders    { get; }
    IRepository<WasteMove>      WasteMoves       { get; }
    IRepository<Agreement>      Agreements       { get; }
    IRepository<Settlement>     Settlements      { get; }
    IRepository<Incident>       Incidents        { get; }
    IRepository<LerCode>             LerCodes             { get; }
    IRepository<Residue>             Residues             { get; }
    IRepository<TreatmentOperation>  TreatmentOperations  { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
