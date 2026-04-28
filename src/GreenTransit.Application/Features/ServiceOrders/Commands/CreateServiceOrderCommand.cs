using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Domain.Constants;
using GreenTransit.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.ServiceOrders.Commands;

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
    int?      ProductUse,
    int?      ProductCategory,
    Guid?     IdLERCode,
    Guid?     IdPickupPoint,
    DateTime? PlannedPickupStart,
    DateTime? PlannedPickupEnd,
    DateTime? PlannedDeliveryStart,
    DateTime? PlannedDeliveryEnd,
    decimal?  EstimatedWeight,
    int?      MeasureUnit,
    int?      Units,
    string?   ContainersJson,
    Guid?     IdCarrier,
    Guid?     IdPlannedPlant
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
            ProductUse           = request.ProductUse,
            ProductCategory      = request.ProductCategory,
            IdLERCode            = request.IdLERCode,
            IdPickupPoint        = request.IdPickupPoint,
            PlannedPickupStart   = request.PlannedPickupStart,
            PlannedPickupEnd     = request.PlannedPickupEnd,
            PlannedDeliveryStart = request.PlannedDeliveryStart,
            PlannedDeliveryEnd   = request.PlannedDeliveryEnd,
            EstimatedWeight      = request.EstimatedWeight,
            MeasureUnit          = request.MeasureUnit,
            Units                = request.Units,
            ContainersJson       = request.ContainersJson,
            IdCarrier            = request.IdCarrier,
            IdPlannedPlant       = request.IdPlannedPlant,
            Version              = 1,
            IdUser               = _currentUser.IdUser,
            CreatedAt            = DateTime.UtcNow,
            UpdatedAt            = DateTime.UtcNow
        };

        so.Hash = ComputeHash(so);

        _context.ServiceOrders.Add(so);
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

