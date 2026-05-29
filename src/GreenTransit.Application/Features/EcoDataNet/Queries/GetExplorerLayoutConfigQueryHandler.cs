using System.Text.Json;
using System.Text.Json.Nodes;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.EcoDataNet.DTOs.DataExplorer;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GreenTransit.Application.Features.EcoDataNet.Queries;

public class GetExplorerLayoutConfigQueryHandler
    : IRequestHandler<GetExplorerLayoutConfigQuery, LayoutConfigDto?>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly IApplicationDbContext  _db;
    private readonly ICurrentUserService    _currentUser;
    private readonly ILogger<GetExplorerLayoutConfigQueryHandler> _logger;

    public GetExplorerLayoutConfigQueryHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ILogger<GetExplorerLayoutConfigQueryHandler> logger)
    {
        _db          = db;
        _currentUser = currentUser;
        _logger      = logger;
    }

    public async Task<LayoutConfigDto?> Handle(
        GetExplorerLayoutConfigQuery request,
        CancellationToken cancellationToken)
    {
        var ownerId = _currentUser.OwnerId;
        var userId  = _currentUser.IdUser;

        // Buscar configuración para este tenant + usuario + asset + proveedor
        var record = await _db.ExplorerLayoutConfigs
            .Where(c => c.OwnerId             == ownerId
                     && c.UserId              == userId
                     && c.AssetId             == request.AssetId
                     && c.ProviderParticipantId == request.ProviderParticipantId)
            .FirstOrDefaultAsync(cancellationToken);

        if (record is null)
            return null;

        // Deserializar overrides + custom widgets desde JSON
        // Retrocompatibilidad: el formato antiguo era directamente un array de WidgetLayoutOverride.
        // El nuevo formato es un objeto PersistedLayoutConfig con "overrides" y "customWidgets".
        List<WidgetLayoutOverride> overrides;
        List<CustomWidgetDefinition> customWidgets;
        try
        {
            using var doc = JsonDocument.Parse(record.LayoutConfigJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                // Formato antiguo
                overrides     = JsonSerializer.Deserialize<List<WidgetLayoutOverride>>(
                                    record.LayoutConfigJson, JsonOptions)
                                ?? new();
                customWidgets = new();
            }
            else
            {
                // Formato nuevo
                var persisted = JsonSerializer.Deserialize<PersistedLayoutConfig>(
                                    record.LayoutConfigJson, JsonOptions)
                                ?? new();
                overrides     = persisted.Overrides;
                customWidgets = persisted.CustomWidgets;
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex,
                "JSON inválido en ExplorerLayoutConfig Id={Id} para AssetId={AssetId}. Se ignora la configuración.",
                record.Id, record.AssetId);
            return null;
        }

        return new LayoutConfigDto
        {
            Id                    = record.Id,
            AssetId               = record.AssetId,
            ProviderParticipantId = record.ProviderParticipantId,
            DatasetName           = record.DatasetName,
            Overrides             = overrides,
            CustomWidgets         = customWidgets,
            SchemaHash            = record.SchemaHash,
            HasSavedConfig        = true,
            LastUpdated           = record.UpdatedAt
        };
    }
}
