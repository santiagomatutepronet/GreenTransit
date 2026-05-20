using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Domain.Constants;
using GreenTransit.Domain.Entities;
using MediatR;

namespace GreenTransit.Application.Features.ServiceOrders.Commands;

// ── Input de línea de residuo ─────────────────────────────────────────────────

public sealed record ServiceOrderResidueInput(
    int      SortOrder,
    Guid?    IdLERCode,
    int?     ProductUse,
    int?     ProductCategory,
    decimal? EstimatedWeight,
    int?     MeasureUnit,
    int?     Units
);

// ── Comando ───────────────────────────────────────────────────────────────────

public sealed record CreateServiceOrderCommand(
    string    ServiceOrderNumber,   // vacío = generar automáticamente
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
) : IRequest<Guid>;

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class CreateServiceOrderCommandHandler
    : IRequestHandler<CreateServiceOrderCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public CreateServiceOrderCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<Guid> Handle(
        CreateServiceOrderCommand request, CancellationToken cancellationToken)
    {
        var ownerId = _currentUser.OwnerId;

        // ── Número automático ─────────────────────────────────────────────────
        var number = string.IsNullOrWhiteSpace(request.ServiceOrderNumber)
            ? await GenerateNumberAsync(ownerId, cancellationToken)
            : request.ServiceOrderNumber;

        // ── Cabecera sincronizada desde la primera línea de residuo ───────────
        var firstLine = request.Residues.Length > 0 ? request.Residues[0] : null;

        // ── Entidad ───────────────────────────────────────────────────────────
        var so = new ServiceOrder
        {
            Id                   = Guid.NewGuid(),
            OwnerId              = ownerId,
            ServiceOrderNumber   = number,
            IssuedAt             = request.IssuedAt,
            IdIssuedBy           = request.IdIssuedBy,
            IssuedByName         = request.IssuedByName,
            IssuedByNationalId   = request.IssuedByNationalId,
            IssuedByCenterCode   = request.IssuedByCenterCode,
            Status               = request.Status,
            Priority             = request.Priority,
            WasteStream          = request.WasteStream,
            SubStream            = request.SubStream,
            IdLERCode            = firstLine?.IdLERCode,
            ProductUse           = firstLine?.ProductUse,
            ProductCategory      = firstLine?.ProductCategory,
            EstimatedWeight      = firstLine?.EstimatedWeight,
            MeasureUnit          = firstLine?.MeasureUnit,
            Units                = firstLine?.Units,
            IdPickupPoint        = request.IdPickupPoint,
            PlannedPickupStart   = request.PlannedPickupStart,
            PlannedPickupEnd     = request.PlannedPickupEnd,
            PlannedDeliveryStart = request.PlannedDeliveryStart,
            PlannedDeliveryEnd   = request.PlannedDeliveryEnd,
            ContainersJson       = request.ContainersJson,
            IdCarrier            = request.IdCarrier,
            IdPlannedPlant       = request.IdPlannedPlant,
            Version              = 1,
            IdUser               = _currentUser.IdUser,
            CreatedAt            = DateTime.UtcNow,
            UpdatedAt            = DateTime.UtcNow
        };

        so.Hash = ComputeHash(so);

        // ── Líneas de residuo ────────────────────────────────────────────────
        foreach (var line in request.Residues)
        {
            so.Residues.Add(new ServiceOrderResidue
            {
                Id             = Guid.NewGuid(),
                IdServiceOrder = so.Id,
                SortOrder      = line.SortOrder,
                IdLERCode      = line.IdLERCode,
                ProductUse     = line.ProductUse,
                ProductCategory = line.ProductCategory,
                EstimatedWeight = line.EstimatedWeight,
                MeasureUnit    = line.MeasureUnit,
                Units          = line.Units
            });
        }

        _context.Add(so);
        await _context.SaveChangesAsync(cancellationToken);

        return so.Id;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<string> GenerateNumberAsync(Guid ownerId, CancellationToken ct)
    {
        var count = await _context.ServiceOrders
            .CountAsync(s => s.OwnerId == ownerId, ct);

        return $"SO-{DateTime.UtcNow.Year}-{(count + 1):00000}";
    }

    private static string ComputeHash(ServiceOrder so)
    {
        var payload = JsonSerializer.Serialize(new
        {
            so.ServiceOrderNumber, so.IssuedAt, so.IdIssuedBy,
            so.IdPickupPoint, so.IdLERCode, so.Status, so.Priority,
            so.EstimatedWeight, so.PlannedPickupStart
        });

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

