using FluentValidation;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.EcoDataNet.DTOs;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GreenTransit.Application.Features.EcoDataNet.Commands;

// ── Command ───────────────────────────────────────────────────────────────────

public sealed record DownloadTransferDataCommand : IRequest<EdcDataDownloadResponse>
{
    public string DataPlaneEndpoint { get; init; } = string.Empty;
    public string AuthType { get; init; } = "Bearer";
    public string AuthCode { get; init; } = string.Empty;
}

// ── Validator ─────────────────────────────────────────────────────────────────

public sealed class DownloadTransferDataCommandValidator
    : AbstractValidator<DownloadTransferDataCommand>
{
    public DownloadTransferDataCommandValidator()
    {
        RuleFor(x => x.DataPlaneEndpoint).NotEmpty()
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out _))
            .WithMessage("DataPlaneEndpoint debe ser una URL válida.");
        RuleFor(x => x.AuthCode).NotEmpty();
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class DownloadTransferDataCommandHandler
    : IRequestHandler<DownloadTransferDataCommand, EdcDataDownloadResponse>
{
    private readonly IEdcManagementClient _edcClient;
    private readonly ILogger<DownloadTransferDataCommandHandler> _logger;

    public DownloadTransferDataCommandHandler(
        IEdcManagementClient edcClient,
        ILogger<DownloadTransferDataCommandHandler> logger)
    {
        _edcClient = edcClient;
        _logger    = logger;
    }

    public async Task<EdcDataDownloadResponse> Handle(
        DownloadTransferDataCommand request, CancellationToken ct)
    {
        _logger.LogInformation("Descargando datos desde data plane: {Endpoint}", request.DataPlaneEndpoint);

        // El token EDR es temporal y autoriza la descarga directamente; no requiere verificación multi-tenant adicional
        return await _edcClient.DownloadDataAsync(
            request.DataPlaneEndpoint, request.AuthType, request.AuthCode, ct);
    }
}
