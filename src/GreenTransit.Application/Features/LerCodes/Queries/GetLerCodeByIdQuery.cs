using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.LerCodes.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.LerCodes.Queries;

public sealed record GetLerCodeByIdQuery(Guid Id) : IRequest<LerCodeDetailDto?>;

public sealed class GetLerCodeByIdQueryHandler
    : IRequestHandler<GetLerCodeByIdQuery, LerCodeDetailDto?>
{
    private readonly IApplicationDbContext _context;

    public GetLerCodeByIdQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<LerCodeDetailDto?> Handle(
        GetLerCodeByIdQuery request,
        CancellationToken cancellationToken)
    {
        return await _context.LerCodes
            .AsNoTracking()
            .Where(l => l.Id == request.Id)
            .Select(l => new LerCodeDetailDto(
                l.Id, l.Code, l.CodeExtended, l.Description,
                l.Chapter, l.ChapterDescription,
                l.SubChapter, l.SubChapterDescription,
                l.IsDangerous, l.IsRAEE, l.DefaultProductCategory,
                l.Notes, l.IsActive, l.CreatedAt, l.UpdatedAt))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
