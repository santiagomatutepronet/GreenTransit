using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentValidation;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Domain.Constants;
using GreenTransit.Domain.Entities;
using MediatR;

namespace GreenTransit.Application.Features.ServiceOrders.Commands;

// ── UpdateServiceOrderCommand ─────────────────────────────────────────────────

public sealed record UpdateServiceOrderCommand(
    Guid      Id,
    string    ServiceOrderNumber,
    DateTime  IssuedAt,
    Guid?     IdIssuedBy,
    string?   IssuedByName,
    string?   IssuedByNationalId,
    string?   IssuedByCenterCode,
    string    Status,
    string    Priority,
    string?   WasteStream,
    string?   SubStream,
    Guid?     IdPickupPoint,
    DateTime? PlannedPickupStart,
    DateTime? PlannedPickupEnd,
    DateTime? PlannedDeliveryStart,
    DateTime? PlannedDeliveryEnd,
    string?   ContainersJson,
    Guid?     IdCarrier,
    Guid?     IdPlannedPlant,
    ServiceOrderResidueInput[] Residues
) : IRequest;

public sealed class UpdateServiceOrderCommandHandler : IRequestHandler<UpdateServiceOrderCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public UpdateServiceOrderCommandHandler(
        IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task Handle(UpdateServiceOrderCommand request, CancellationToken ct)
    {
        var so = await _context.ServiceOrders
            .FirstOrDefaultAsync(s => s.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"ServiceOrder {request.Id} no encontrada.");

        if (!ServiceOrderStatuses.Editable.Contains(so.Status))
            throw new InvalidOperationException(
                $"No se puede editar una SO en estado '{so.Status}'.");

        // ── Cabecera sincronizada desde la primera línea de residuo ───────────
        var firstLine = request.Residues.Length > 0 ? request.Residues[0] : null;

        so.ServiceOrderNumber   = request.ServiceOrderNumber;
        so.IssuedAt             = request.IssuedAt;
        so.IdIssuedBy           = request.IdIssuedBy;
        so.IssuedByName         = request.IssuedByName;
        so.IssuedByNationalId   = request.IssuedByNationalId;
        so.IssuedByCenterCode   = request.IssuedByCenterCode;
        so.Status               = request.Status;
        so.Priority             = request.Priority;
        so.WasteStream          = request.WasteStream;
        so.SubStream            = request.SubStream;
        so.IdLERCode            = firstLine?.IdLERCode;
        so.ProductUse           = firstLine?.ProductUse;
        so.ProductCategory      = firstLine?.ProductCategory;
        so.EstimatedWeight      = firstLine?.EstimatedWeight;
        so.MeasureUnit          = firstLine?.MeasureUnit;
        so.Units                = firstLine?.Units;
        so.IdPickupPoint        = request.IdPickupPoint;
        so.PlannedPickupStart   = request.PlannedPickupStart;
        so.PlannedPickupEnd     = request.PlannedPickupEnd;
        so.PlannedDeliveryStart = request.PlannedDeliveryStart;
        so.PlannedDeliveryEnd   = request.PlannedDeliveryEnd;
        so.ContainersJson       = request.ContainersJson;
        so.IdCarrier            = request.IdCarrier;
        so.IdPlannedPlant       = request.IdPlannedPlant;
        so.Version             += 1;
        so.UpdatedAt            = DateTime.UtcNow;
        so.IdUser               = _currentUser.IdUser;
        so.Hash                 = ComputeHash(so);

        // ── Reemplazar líneas de residuo ─────────────────────────────────────
        // ExecuteDeleteAsync elimina directamente en BD sin pasar por el change tracker,
        // evitando DbUpdateConcurrencyException en contextos de larga vida (Blazor Server).
        await _context.ServiceOrderResidues
            .Where(r => r.IdServiceOrder == so.Id)
            .ExecuteDeleteAsync(ct);

        foreach (var line in request.Residues)
        {
            _context.Add(new ServiceOrderResidue
            {
                Id              = Guid.NewGuid(),
                IdServiceOrder  = so.Id,
                SortOrder       = line.SortOrder,
                IdLERCode       = line.IdLERCode,
                ProductUse      = line.ProductUse,
                ProductCategory = line.ProductCategory,
                EstimatedWeight = line.EstimatedWeight,
                MeasureUnit     = line.MeasureUnit,
                Units           = line.Units
            });
        }

        await _context.SaveChangesAsync(ct);
    }

    private static string ComputeHash(ServiceOrder so)
    {
        var payload = JsonSerializer.Serialize(new
        {
            so.ServiceOrderNumber, so.IssuedAt, so.IdIssuedBy,
            so.IdPickupPoint, so.IdLERCode, so.Status, so.Priority,
            so.EstimatedWeight, so.PlannedPickupStart, so.Version
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)))
                      .ToLowerInvariant();
    }
}

public sealed class UpdateServiceOrderCommandValidator
    : AbstractValidator<UpdateServiceOrderCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public UpdateServiceOrderCommandValidator(
        IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context     = context;
        _currentUser = currentUser;

        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.Status)
            .Must(s => ServiceOrderStatuses.All.Contains(s))
            .WithMessage($"Estado inválido. Permitidos: {string.Join(", ", ServiceOrderStatuses.All)}.");

        RuleFor(x => x.Priority)
            .Must(p => ServiceOrderPriorities.All.Contains(p))
            .WithMessage($"Prioridad inválida. Permitidas: {string.Join(", ", ServiceOrderPriorities.All)}.");

        RuleFor(x => x.ServiceOrderNumber)
            .MaximumLength(64)
            .MustAsync(NumberUniqueForUpdateAsync)
            .WithMessage("Ya existe otra SO con ese número en este tenant.");

        RuleFor(x => x)
            .Must(x => x.PlannedPickupEnd is null || x.PlannedPickupStart is null
                    || x.PlannedPickupEnd > x.PlannedPickupStart)
            .WithMessage("La fecha fin de recogida debe ser posterior al inicio.")
            .OverridePropertyName(nameof(UpdateServiceOrderCommand.PlannedPickupEnd));

        RuleFor(x => x)
            .Must(x => x.PlannedDeliveryStart is null || x.PlannedPickupEnd is null
                    || x.PlannedDeliveryStart >= x.PlannedPickupEnd)
            .WithMessage("La fecha inicio de entrega debe ser igual o posterior al fin de recogida.")
            .OverridePropertyName(nameof(UpdateServiceOrderCommand.PlannedDeliveryStart));

        // Al menos una línea de residuo
        RuleFor(x => x.Residues)
            .NotEmpty()
            .WithMessage("La orden de servicio debe tener al menos una línea de residuo.");
    }

    private async Task<bool> NumberUniqueForUpdateAsync(
        UpdateServiceOrderCommand cmd, string number, CancellationToken ct)
    {
        var ownerId = _currentUser.OwnerId;
        return !await _context.ServiceOrders
            .AnyAsync(s => s.OwnerId == ownerId
                        && s.ServiceOrderNumber == number
                        && s.Id != cmd.Id, ct);
    }
}

// ── DuplicateServiceOrderCommand ──────────────────────────────────────────────

public sealed record DuplicateServiceOrderCommand(Guid SourceId) : IRequest<Guid>;

public sealed class DuplicateServiceOrderCommandHandler
    : IRequestHandler<DuplicateServiceOrderCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public DuplicateServiceOrderCommandHandler(
        IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<Guid> Handle(DuplicateServiceOrderCommand request, CancellationToken ct)
    {
        var source = await _context.ServiceOrders
            .AsNoTracking()
            .Include(s => s.Residues)
            .FirstOrDefaultAsync(s => s.Id == request.SourceId, ct)
            ?? throw new KeyNotFoundException($"ServiceOrder {request.SourceId} no encontrada.");

        var ownerId = _currentUser.OwnerId;
        var count   = await _context.ServiceOrders.CountAsync(s => s.OwnerId == ownerId, ct);
        var number  = $"SO-{DateTime.UtcNow.Year}-{(count + 1):00000}";

        var copy = new ServiceOrder
        {
            Id                   = Guid.NewGuid(),
            OwnerId              = ownerId,
            ServiceOrderNumber   = number,
            IssuedAt             = DateTime.UtcNow,
            IdIssuedBy           = source.IdIssuedBy,
            IssuedByName         = source.IssuedByName,
            IssuedByNationalId   = source.IssuedByNationalId,
            IssuedByCenterCode   = source.IssuedByCenterCode,
            Status               = ServiceOrderStatuses.Pending,
            Priority             = source.Priority,
            WasteStream          = source.WasteStream,
            SubStream            = source.SubStream,
            IdLERCode            = source.IdLERCode,
            ProductUse           = source.ProductUse,
            ProductCategory      = source.ProductCategory,
            IdPickupPoint        = source.IdPickupPoint,
            PlannedPickupStart   = null,
            PlannedPickupEnd     = null,
            PlannedDeliveryStart = null,
            PlannedDeliveryEnd   = null,
            EstimatedWeight      = source.EstimatedWeight,
            MeasureUnit          = source.MeasureUnit,
            Units                = source.Units,
            ContainersJson       = source.ContainersJson,
            IdCarrier            = source.IdCarrier,
            IdPlannedPlant       = source.IdPlannedPlant,
            Version              = 1,
            IdUser               = _currentUser.IdUser,
            CreatedAt            = DateTime.UtcNow,
            UpdatedAt            = DateTime.UtcNow
        };

        var payload = JsonSerializer.Serialize(new
        {
            copy.ServiceOrderNumber, copy.IssuedAt, copy.IdIssuedBy,
            copy.IdPickupPoint, copy.IdLERCode, copy.Status
        });
        copy.Hash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();

        // ── Copiar líneas de residuo ──────────────────────────────────────────
        foreach (var line in source.Residues.OrderBy(r => r.SortOrder))
        {
            copy.Residues.Add(new ServiceOrderResidue
            {
                Id              = Guid.NewGuid(),
                IdServiceOrder  = copy.Id,
                SortOrder       = line.SortOrder,
                IdLERCode       = line.IdLERCode,
                ProductUse      = line.ProductUse,
                ProductCategory = line.ProductCategory,
                EstimatedWeight = line.EstimatedWeight,
                MeasureUnit     = line.MeasureUnit,
                Units           = line.Units
            });
        }

        _context.Add(copy);
        await _context.SaveChangesAsync(ct);
        return copy.Id;
    }
}


// ── CancelServiceOrderCommand ─────────────────────────────────────────────────

public sealed record CancelServiceOrderCommand(Guid Id, string Reason) : IRequest;

public sealed class CancelServiceOrderCommandHandler
    : IRequestHandler<CancelServiceOrderCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public CancelServiceOrderCommandHandler(
        IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task Handle(CancelServiceOrderCommand request, CancellationToken ct)
    {
        var so = await _context.ServiceOrders
            .FirstOrDefaultAsync(s => s.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"ServiceOrder {request.Id} no encontrada.");

        if (so.Status == ServiceOrderStatuses.Cancelled)
            throw new InvalidOperationException("La SO ya está cancelada.");

        // Impedir cancelación si tiene traslado activo vinculado
        var hasActiveMove = await _context.WasteMoves
            .AnyAsync(m => m.ServiceOrderId == so.Id
                        && m.ServiceStatus != "CANCELADO", ct);

        if (hasActiveMove)
            throw new InvalidOperationException(
                "No se puede cancelar: la SO tiene un traslado activo vinculado.");

        so.Status    = ServiceOrderStatuses.Cancelled;
        so.Version  += 1;
        so.UpdatedAt = DateTime.UtcNow;
        so.IdUser    = _currentUser.IdUser;

        await _context.SaveChangesAsync(ct);
    }
}

// ── LinkToWasteMoveCommand ────────────────────────────────────────────────────

public sealed record LinkToWasteMoveCommand(Guid ServiceOrderId, Guid WasteMoveId) : IRequest;

public sealed class LinkToWasteMoveCommandHandler : IRequestHandler<LinkToWasteMoveCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public LinkToWasteMoveCommandHandler(
        IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task Handle(LinkToWasteMoveCommand request, CancellationToken ct)
    {
        var so = await _context.ServiceOrders
            .FirstOrDefaultAsync(s => s.Id == request.ServiceOrderId, ct)
            ?? throw new KeyNotFoundException($"ServiceOrder {request.ServiceOrderId} no encontrada.");

        var wm = await _context.WasteMoves
            .FirstOrDefaultAsync(m => m.Id == request.WasteMoveId, ct)
            ?? throw new KeyNotFoundException($"WasteMove {request.WasteMoveId} no encontrado.");

        so.WasteMoveReference = wm.WasteMoveReference;
        so.Status             = ServiceOrderStatuses.InProgress;
        so.Version           += 1;
        so.UpdatedAt          = DateTime.UtcNow;
        so.IdUser             = _currentUser.IdUser;

        await _context.SaveChangesAsync(ct);
    }
}
