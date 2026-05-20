using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.ProductDeclarations.DTOs;
using GreenTransit.Domain.Authorization;
using MediatR;

namespace GreenTransit.Application.Features.ProductDeclarations.Queries;

/// <summary>Devuelve el detalle completo de una declaración de producción con sus líneas.</summary>
public sealed record GetProductDeclarationDetailQuery(Guid Id)
    : IRequest<ProductDeclarationDetailDto>;

public sealed class GetProductDeclarationDetailQueryHandler
    : IRequestHandler<GetProductDeclarationDetailQuery, ProductDeclarationDetailDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public GetProductDeclarationDetailQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<ProductDeclarationDetailDto> Handle(
        GetProductDeclarationDetailQuery request, CancellationToken ct)
    {
        var pd = await _context.ProductDeclarations
            .AsNoTracking()
            .Include(d => d.Producer)
            .Include(d => d.Products)
                .ThenInclude(p => p.Residue)
            .FirstOrDefaultAsync(d => d.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Declaración {request.Id} no encontrada.");

        // ── Verificación de visibilidad por perfil ────────────────────────────
        if (_currentUser.IsInProfile(ProfileConstants.Producer))
        {
            if (pd.IdProducer != _currentUser.LinkedEntityId)
                throw new UnauthorizedAccessException(
                    "No tienes permiso para ver esta declaración.");
        }
        else if (_currentUser.IsInProfile(ProfileConstants.Scrap))
        {
            var linkedId = _currentUser.LinkedEntityId;
            if (linkedId.HasValue && pd.IdProducer.HasValue)
            {
                var isAdherent = await _context.Agreements
                    .AnyAsync(a => a.IdScrap == linkedId.Value
                               && a.IdPublicEntity == pd.IdProducer, ct);
                if (!isAdherent)
                    throw new UnauthorizedAccessException(
                        "No tienes permiso para ver esta declaración.");
            }
        }

        // Cargar diccionario de fuentes para resolver descripciones
        var sourceRefs = pd.Products
            .Where(p => !string.IsNullOrEmpty(p.Source))
            .Select(p => p.Source!)
            .Distinct()
            .ToList();

        Dictionary<string, string> sourceLookup = [];
        if (sourceRefs.Count > 0)
        {
            sourceLookup = await _context.DicProductDeclarationSources
                .AsNoTracking()
                .Where(s => sourceRefs.Contains(s.Ref))
                .ToDictionaryAsync(s => s.Ref, s => s.Description, ct);
        }

        var products = pd.Products
            .Select(p => new ProductLineDto(
                p.Id,
                p.IdProductDeclaration,
                p.IdResidue,
                p.Residue?.Name ?? p.ProductName,
                p.Residue?.Reference,
                p.Residue?.ProductCategory ?? p.ProductCategory,
                p.Reference,
                p.Source,
                p.Source != null && sourceLookup.TryGetValue(p.Source, out var srcDesc) ? srcDesc : p.Source,
                p.ProductUse,
                p.ProductCategory,
                p.Quantity,
                p.MeasureUnit,
                p.Units,
                p.Price))
            .ToList();

        return new ProductDeclarationDetailDto(
            pd.Id,
            pd.OwnerId,
            pd.Period,
            pd.Year,
            pd.Month,
            pd.Currency,
            pd.State,
            pd.DateCreate,
            pd.DateEmit,
            pd.Reference,
            pd.IdProducer,
            pd.Producer?.Name,
            pd.Producer?.NationalId,
            pd.Producer?.CenterCode,
            pd.Amount,
            pd.Type,
            pd.DateCreateSys,
            pd.DateModifiedSys,
            products);
    }
}
