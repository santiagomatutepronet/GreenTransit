using MediatR;
using GreenTransit.Application.Features.EcoDataNet.DTOs.DataExplorer;

namespace GreenTransit.Application.Features.EcoDataNet.Queries;

public class AnalyzeEdcDataQuery : IRequest<EdcDataExplorerResult>
{
    /// <summary>Contenido JSON crudo descargado de la transferencia EDC.</summary>
    public string JsonContent { get; set; } = string.Empty;

    /// <summary>Nombre del proveedor (para metadatos, opcional).</summary>
    public string? ProviderName { get; set; }

    /// <summary>Nombre del dataset/asset (para metadatos, opcional).</summary>
    public string? DatasetName { get; set; }
}
