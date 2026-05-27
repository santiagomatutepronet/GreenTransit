using System.Text;
using MediatR;
using GreenTransit.Application.Features.EcoDataNet.DTOs.DataExplorer;
using GreenTransit.Application.Features.EcoDataNet.Services;

namespace GreenTransit.Application.Features.EcoDataNet.Queries;

public class AnalyzeEdcDataQueryHandler : IRequestHandler<AnalyzeEdcDataQuery, EdcDataExplorerResult>
{
    private const long MaxJsonSizeBytes = 50 * 1024 * 1024; // 50 MB

    private readonly IJsonSchemaAnalyzer _schemaAnalyzer;
    private readonly IDashboardLayoutBuilder _layoutBuilder;

    public AnalyzeEdcDataQueryHandler(
        IJsonSchemaAnalyzer schemaAnalyzer,
        IDashboardLayoutBuilder layoutBuilder)
    {
        _schemaAnalyzer = schemaAnalyzer;
        _layoutBuilder  = layoutBuilder;
    }

    public Task<EdcDataExplorerResult> Handle(AnalyzeEdcDataQuery request, CancellationToken cancellationToken)
    {
        // Validaciones rápidas en el thread actual (sin coste de Task.Run)
        if (string.IsNullOrWhiteSpace(request.JsonContent))
            return Task.FromResult(new EdcDataExplorerResult
            {
                Success      = false,
                ErrorMessage = "El contenido JSON está vacío. No hay datos para explorar."
            });

        var sizeBytes = Encoding.UTF8.GetByteCount(request.JsonContent);
        if (sizeBytes > MaxJsonSizeBytes)
            return Task.FromResult(new EdcDataExplorerResult
            {
                Success      = false,
                ErrorMessage = $"El JSON supera el límite de 50 MB ({sizeBytes / 1024 / 1024} MB). Descargue un dataset más pequeño."
            });

        // El análisis JSON y la construcción del layout son operaciones CPU-intensivas.
        // Se ejecutan en un thread del ThreadPool para no bloquear el circuit de Blazor Server
        // y permitir que el spinner se renderice mientras se procesa.
        return Task.Run(() => AnalyzeInternal(request, sizeBytes), cancellationToken);
    }

    private EdcDataExplorerResult AnalyzeInternal(AnalyzeEdcDataQuery request, long sizeBytes)
    {
        try
        {
            var schema = _schemaAnalyzer.Analyze(request.JsonContent);
            if (schema is null)
                return new EdcDataExplorerResult
                {
                    Success      = false,
                    ErrorMessage = "El contenido descargado no es un JSON válido. Solo se pueden explorar datos en formato JSON."
                };

            var widgets = _layoutBuilder.Build(schema);

            return new EdcDataExplorerResult
            {
                Success  = true,
                Schema   = schema,
                Widgets  = widgets,
                Metadata = new DataExplorerMetadata
                {
                    ProviderName   = request.ProviderName,
                    DatasetName    = request.DatasetName,
                    DownloadedAt   = DateTime.UtcNow,
                    JsonSizeBytes  = sizeBytes,
                    DetectedFormat = schema.RootIsArray ? "JSON Array" : "JSON Object"
                }
            };
        }
        catch (Exception ex)
        {
            return new EdcDataExplorerResult
            {
                Success      = false,
                ErrorMessage = $"Error inesperado durante el análisis: {ex.Message}"
            };
        }
    }
}
