using GreenTransit.Application.Features.EcoDataNet.DTOs;

namespace GreenTransit.Application.Common.Interfaces;

/// <summary>
/// Parsea el JSON bruto de un catálogo EDC (DCAT/ODRL) a DTOs tipados.
/// </summary>
public interface IEdcCatalogParser
{
    /// <summary>
    /// Parsea el JSON bruto del catálogo DCAT/ODRL a DTOs tipados.
    /// </summary>
    EdcCatalogDto? ParseCatalog(string rawJson);
}
