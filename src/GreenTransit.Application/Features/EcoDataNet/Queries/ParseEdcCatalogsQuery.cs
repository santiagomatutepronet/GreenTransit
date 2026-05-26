using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.EcoDataNet.DTOs;
using MediatR;

namespace GreenTransit.Application.Features.EcoDataNet.Queries;

/// <summary>
/// Parsea los JSON brutos de catálogos ya obtenidos en un RequestEdcCatalogResponse
/// y devuelve DTOs tipados agrupados por proveedor.
/// </summary>
public record ParseEdcCatalogsQuery(RequestEdcCatalogResponse CatalogResponse)
    : IRequest<List<EdcProviderParsedCatalogDto>>;

public class ParseEdcCatalogsQueryHandler
    : IRequestHandler<ParseEdcCatalogsQuery, List<EdcProviderParsedCatalogDto>>
{
    private readonly IEdcCatalogParser _parser;

    public ParseEdcCatalogsQueryHandler(IEdcCatalogParser parser)
    {
        _parser = parser;
    }

    public Task<List<EdcProviderParsedCatalogDto>> Handle(
        ParseEdcCatalogsQuery request, CancellationToken ct)
    {
        var results = new List<EdcProviderParsedCatalogDto>();

        foreach (var provider in request.CatalogResponse.Results
            .Where(r => r.Status == EdcProviderStatus.Ok
                     && r.CatalogResult?.Success == true
                     && !string.IsNullOrEmpty(r.CatalogResult.RawJson)))
        {
            var dto = new EdcProviderParsedCatalogDto
            {
                ProviderUserId         = provider.UserId,
                ProviderUserName       = provider.UserName,
                ProviderUserLogin      = provider.UserLogin,
                ProviderServerName     = provider.EDCServerName,
                ProviderConnectorId    = provider.EDCConnectorId,
                ProviderProtocolEndpoint = !string.IsNullOrEmpty(provider.EDCServerName)
                    ? $"https://proto.{provider.EDCServerName}/protocol"
                    : string.Empty
            };

            try
            {
                dto.Catalog          = _parser.ParseCatalog(provider.CatalogResult!.RawJson!);
                dto.ParseSuccess     = dto.Catalog != null;
                dto.ProviderParticipantId = dto.Catalog?.ParticipantId ?? string.Empty;
            }
            catch (Exception ex)
            {
                dto.ParseSuccess = false;
                dto.ParseError   = ex.Message;
            }

            results.Add(dto);
        }

        return Task.FromResult(results);
    }
}
