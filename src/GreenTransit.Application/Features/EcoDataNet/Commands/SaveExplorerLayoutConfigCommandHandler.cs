using System.Text.Json;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.EcoDataNet.DTOs.DataExplorer;
using GreenTransit.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.EcoDataNet.Commands;

public class SaveExplorerLayoutConfigCommandHandler
    : IRequestHandler<SaveExplorerLayoutConfigCommand, int>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented        = false
    };

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService   _currentUser;

    public SaveExplorerLayoutConfigCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser)
    {
        _db          = db;
        _currentUser = currentUser;
    }

    public async Task<int> Handle(
        SaveExplorerLayoutConfigCommand request,
        CancellationToken cancellationToken)
    {
        var ownerId = _currentUser.OwnerId;
        var userId  = _currentUser.IdUser;

        var persisted = new PersistedLayoutConfig
        {
            Overrides     = request.Overrides,
            CustomWidgets = request.CustomWidgets
        };
        var json = JsonSerializer.Serialize(persisted, JsonOptions);

        // Patrón Upsert: buscar registro existente
        var existing = await _db.ExplorerLayoutConfigs
            .Where(c => c.OwnerId              == ownerId
                     && c.UserId               == userId
                     && c.AssetId              == request.AssetId
                     && c.ProviderParticipantId == request.ProviderParticipantId)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            // Actualizar registro existente
            existing.LayoutConfigJson = json;
            existing.SchemaHash       = request.SchemaHash;
            existing.DatasetName      = request.DatasetName;
            existing.UpdatedAt        = DateTime.UtcNow;
        }
        else
        {
            // Crear nuevo registro
            existing = new ExplorerLayoutConfig
            {
                OwnerId               = ownerId,
                UserId                = userId,
                AssetId               = request.AssetId,
                ProviderParticipantId = request.ProviderParticipantId,
                DatasetName           = request.DatasetName,
                LayoutConfigJson      = json,
                SchemaHash            = request.SchemaHash,
                CreatedAt             = DateTime.UtcNow,
                UpdatedAt             = DateTime.UtcNow
            };
            _db.Add(existing);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return existing.Id;
    }
}
