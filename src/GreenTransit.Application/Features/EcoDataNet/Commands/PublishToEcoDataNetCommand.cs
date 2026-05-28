using GreenTransit.Application.Common.Interfaces;
using MediatR;

namespace GreenTransit.Application.Features.EcoDataNet.Commands;

public record PublishToEcoDataNetCommand(
    Action<string, int, int>? OnProgress = null
) : IRequest<PublishSummary>;

public class PublishToEcoDataNetCommandHandler
    : IRequestHandler<PublishToEcoDataNetCommand, PublishSummary>
{
    private readonly IEcoDataNetPublisher _publisher;

    public PublishToEcoDataNetCommandHandler(IEcoDataNetPublisher publisher)
        => _publisher = publisher;

    public Task<PublishSummary> Handle(
        PublishToEcoDataNetCommand request, CancellationToken cancellationToken)
        => _publisher.PublishAllAsync(request.OnProgress, cancellationToken);
}
