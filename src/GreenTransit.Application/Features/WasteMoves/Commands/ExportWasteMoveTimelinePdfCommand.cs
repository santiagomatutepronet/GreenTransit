using MediatR;

namespace GreenTransit.Application.Features.WasteMoves.Commands;

/// <summary>
/// Comando de marcador: solicita la generación del expediente PDF de un traslado.
/// El PDF real se genera en la capa Web (QuestPDF).
/// </summary>
public sealed record ExportWasteMoveTimelinePdfCommand(Guid WasteMoveId) : IRequest<Unit>;

public sealed class ExportWasteMoveTimelinePdfCommandHandler
    : IRequestHandler<ExportWasteMoveTimelinePdfCommand, Unit>
{
    public Task<Unit> Handle(
        ExportWasteMoveTimelinePdfCommand request, CancellationToken ct)
        => Task.FromResult(Unit.Value);
}
