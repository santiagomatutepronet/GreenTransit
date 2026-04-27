using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Domain.Entities;
using GreenTransit.Infrastructure.Persistence.Repositories;

namespace GreenTransit.Infrastructure.Persistence;

/// <summary>
/// Implementación de IUnitOfWork que envuelve AppDbContext.
/// Los repositorios se crean de forma diferida (lazy) y se comparte el mismo
/// contexto para que todas las operaciones participen en la misma transacción.
/// </summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    private IRepository<BusinessEntity>? _businessEntities;
    private IRepository<AppUser>?        _appUsers;
    private IRepository<ServiceOrder>?   _serviceOrders;
    private IRepository<WasteMove>?      _wasteMoves;
    private IRepository<Agreement>?      _agreements;
    private IRepository<Settlement>?     _settlements;
    private IRepository<Incident>?       _incidents;
    private IRepository<LerCode>?             _lerCodes;
    private IRepository<Residue>?             _residues;
    private IRepository<TreatmentOperation>?  _treatmentOperations;

    public UnitOfWork(AppDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public IRepository<BusinessEntity> BusinessEntities
        => _businessEntities ??= new EfRepository<BusinessEntity>(_context, _currentUserService);

    public IRepository<AppUser> AppUsers
        => _appUsers ??= new EfRepository<AppUser>(_context, _currentUserService);

    public IRepository<ServiceOrder> ServiceOrders
        => _serviceOrders ??= new EfRepository<ServiceOrder>(_context, _currentUserService);

    public IRepository<WasteMove> WasteMoves
        => _wasteMoves ??= new EfRepository<WasteMove>(_context, _currentUserService);

    public IRepository<Agreement> Agreements
        => _agreements ??= new EfRepository<Agreement>(_context, _currentUserService);

    public IRepository<Settlement> Settlements
        => _settlements ??= new EfRepository<Settlement>(_context, _currentUserService);

    public IRepository<Incident> Incidents
        => _incidents ??= new EfRepository<Incident>(_context, _currentUserService);

    public IRepository<LerCode> LerCodes
        => _lerCodes ??= new EfRepository<LerCode>(_context, _currentUserService);

    public IRepository<Residue> Residues
        => _residues ??= new EfRepository<Residue>(_context, _currentUserService);

    public IRepository<TreatmentOperation> TreatmentOperations
        => _treatmentOperations ??= new EfRepository<TreatmentOperation>(_context, _currentUserService);

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}
