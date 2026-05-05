using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.ProductDeclarations.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.ProductDeclarations.Queries;

public sealed record ExportProductDeclarationToPdfQuery(Guid Id)
    : IRequest<(byte[] Content, string FileName)>;

/// <summary>
/// Carga el detalle de la declaración y delega la generación del PDF
/// al generador ubicado en la capa Web. El handler devuelve los datos;
/// la capa Web llama al generador antes de enviar la respuesta.
/// En su lugar, devolvemos el DTO para que el componente Blazor lo use.
/// </summary>
public sealed record GetProductDeclarationForExportQuery(Guid Id)
    : IRequest<ProductDeclarationDetailDto>;

public sealed class GetProductDeclarationForExportQueryHandler
    : IRequestHandler<GetProductDeclarationForExportQuery, ProductDeclarationDetailDto>
{
    private readonly IApplicationDbContext _context;

    public GetProductDeclarationForExportQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<ProductDeclarationDetailDto> Handle(
        GetProductDeclarationForExportQuery request, CancellationToken ct)
    {
        var pd = await _context.ProductDeclarations
            .AsNoTracking()
            .Include(d => d.Producer)
            .Include(d => d.Products)
                .ThenInclude(p => p.Residue)
            .FirstOrDefaultAsync(d => d.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Declaración {request.Id} no encontrada.");

        var products = pd.Products
            .Select(p => new ProductLineDto(
                p.Id, p.IdProductDeclaration, p.IdResidue,
                p.Residue?.Name, p.Residue?.Reference,
                p.Residue?.ProductCategory ?? p.ProductCategory,
                p.Reference, p.Source, p.ProductUse, p.ProductCategory,
                p.Quantity, p.MeasureUnit, p.Units, p.Price))
            .ToList();

        return new ProductDeclarationDetailDto(
            pd.Id, pd.OwnerId, pd.Period, pd.Year, pd.Month,
            pd.Currency, pd.State, pd.DateCreate, pd.DateEmit,
            pd.Reference, pd.IdProducer,
            pd.Producer?.Name, pd.Producer?.NationalId, pd.Producer?.CenterCode,
            pd.Amount, pd.Type, pd.DateCreateSys, pd.DateModifiedSys,
            products);
    }
}
