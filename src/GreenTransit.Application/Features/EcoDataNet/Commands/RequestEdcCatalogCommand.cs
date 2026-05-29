using FluentValidation;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Common.Options;
using GreenTransit.Application.Features.EcoDataNet.DTOs;
using GreenTransit.Domain.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GreenTransit.Application.Features.EcoDataNet.Commands;

// ── Command ───────────────────────────────────────────────────────────────────

/// <summary>
/// Solicita el catálogo EDC a todos los proveedores del tenant que tienen el perfil indicado,
/// usando el conector del usuario consumidor como punto de entrada (flujo EDC correcto).
/// </summary>
public sealed record RequestEdcCatalogCommand : IRequest<RequestEdcCatalogResponse>
{
    /// <summary>ID del perfil cuyos datos se quieren consumir (ConsumedProfileId).</summary>
    public int ConsumedProfileId { get; init; }

    /// <summary>
    /// ID del usuario que realiza la solicitud como consumidor.
    /// Su conector EDC se usa como Management API de origen.
    /// </summary>
    public int ConsumerUserId { get; init; }
}

// ── Validator ─────────────────────────────────────────────────────────────────

public sealed class RequestEdcCatalogCommandValidator : AbstractValidator<RequestEdcCatalogCommand>
{
    public RequestEdcCatalogCommandValidator()
    {
        RuleFor(x => x.ConsumedProfileId)
            .GreaterThan(0)
            .WithMessage("Debe seleccionar un perfil a consumir.");

        RuleFor(x => x.ConsumerUserId)
            .GreaterThan(0)
            .WithMessage("Se requiere un usuario consumidor.");
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class RequestEdcCatalogCommandHandler
    : IRequestHandler<RequestEdcCatalogCommand, RequestEdcCatalogResponse>
{
    private readonly IApplicationDbContext   _db;
    private readonly ICurrentUserService     _currentUser;
    private readonly IEdcManagementClient    _edcClient;
    private readonly IOptions<EdcOptions>    _edcOptions;
    private readonly ILogger<RequestEdcCatalogCommandHandler> _logger;

    public RequestEdcCatalogCommandHandler(
        IApplicationDbContext   db,
        ICurrentUserService     currentUser,
        IEdcManagementClient    edcClient,
        IOptions<EdcOptions>    edcOptions,
        ILogger<RequestEdcCatalogCommandHandler> logger)
    {
        _db          = db;
        _currentUser = currentUser;
        _edcClient   = edcClient;
        _edcOptions  = edcOptions;
        _logger      = logger;
    }

    public async Task<RequestEdcCatalogResponse> Handle(
        RequestEdcCatalogCommand request, CancellationToken ct)
    {
        var isAdmin = _currentUser.IsInProfile(ProfileConstants.Admin);

        // 1. SEGURIDAD
        if (!isAdmin)
        {
            // No-ADMIN solo puede consumir con su propio conector
            if (request.ConsumerUserId != _currentUser.IdUser)
                throw new ValidationException(
                    "No tiene permiso para consumir catálogos en nombre de otro usuario.");

            var authorized = await _db.ProfileEDCConsumers
                .AnyAsync(pc => pc.ProfileId == _currentUser.ProfileId
                             && pc.ConsumedProfileId == request.ConsumedProfileId, ct);
            if (!authorized)
                throw new ValidationException(
                    "Su perfil no está autorizado para consumir datos del perfil seleccionado.");
        }

        // 2. OBTENER CONECTOR DEL CONSUMIDOR (quien realiza la petición)
        var consumerConnector = await _db.UserEDCConnectors
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == request.ConsumerUserId, ct);

        if (consumerConnector is null)
            throw new ValidationException(
                "El usuario consumidor no tiene un conector EDC configurado. " +
                "Configure el conector desde 'Configuración conector EDC'.");

        // Management API del consumidor — desde aquí se lanza la petición
        var consumerMgmtUrl = $"https://mgmt.{consumerConnector.EDCServerName}/management";

        _logger.LogInformation(
            "Consumidor EDC: usuario {UserId} server={Server}",
            request.ConsumerUserId, consumerConnector.EDCServerName);

        // 3. LISTAR PROVEEDORES del tenant con el perfil consumido que tengan conector
        var providerUsers = await _db.AppUsers
            .AsNoTracking()
            .Where(u => u.OwnerId == _currentUser.OwnerId
                     && u.IdProfile == request.ConsumedProfileId
                     && u.IsActive)
            .Select(u => new
            {
                u.Id,
                u.CompleteName,
                u.Login,
                Connector = _db.UserEDCConnectors.FirstOrDefault(c => c.UserId == u.Id)
            })
            .ToListAsync(ct);

        _logger.LogInformation(
            "Encontrados {Count} proveedores con perfil {ProfileId}",
            providerUsers.Count, request.ConsumedProfileId);

        // 4. PETICIONES EN PARALELO: Management del consumidor → Protocol del proveedor
        var semaphore = new SemaphoreSlim(_edcOptions.Value.MaxConcurrentRequests);
        var tasks = providerUsers.Select(async provider =>
        {
            var result = new EdcProviderCatalogResult
            {
                UserId    = provider.Id,
                UserName  = provider.CompleteName ?? provider.Login,
                UserLogin = provider.Login
            };

            if (provider.Connector is null)
            {
                result.Status = EdcProviderStatus.NoConnector;
                return result;
            }

            result.EDCServerName  = provider.Connector.EDCServerName;
            result.EDCConnectorId = provider.Connector.EDCConnectorId;

            // counterPartyAddress = Protocol API del proveedor
            var counterPartyUrl = $"https://proto.{provider.Connector.EDCServerName}/protocol";

            await semaphore.WaitAsync(ct);
            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(_edcOptions.Value.RequestTimeoutSeconds));

                // POST desde Management del CONSUMIDOR hacia Protocol del PROVEEDOR
                var catalogResult = await _edcClient.RequestCatalogAsync(
                    consumerMgmtUrl, counterPartyUrl, consumerConnector.ApiKey, timeoutCts.Token);

                result.CatalogResult = catalogResult;
                result.Status        = catalogResult.Success ? EdcProviderStatus.Ok : EdcProviderStatus.Error;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                result.Status = EdcProviderStatus.Timeout;
                result.CatalogResult = new EdcCatalogResult
                {
                    Success      = false,
                    ErrorMessage = $"Timeout tras {_edcOptions.Value.RequestTimeoutSeconds}s"
                };
                _logger.LogWarning("Timeout EDC proveedor {Login} (server: {Server})",
                    provider.Login, provider.Connector.EDCServerName);
            }
            catch (Exception ex)
            {
                result.Status = EdcProviderStatus.Error;
                result.CatalogResult = new EdcCatalogResult { Success = false, ErrorMessage = ex.Message };
                _logger.LogError(ex, "Error EDC proveedor {Login} (server: {Server})",
                    provider.Login, provider.Connector.EDCServerName);
            }
            finally
            {
                semaphore.Release();
            }

            return result;
        });

        var results = await Task.WhenAll(tasks);

        var response = new RequestEdcCatalogResponse
        {
            ConsumerServerName = consumerConnector.EDCServerName,
            TotalProviders     = results.Length,
            SuccessCount       = results.Count(r => r.Status == EdcProviderStatus.Ok),
            ErrorCount         = results.Count(r => r.Status is EdcProviderStatus.Error or EdcProviderStatus.Timeout),
            NoConnectorCount   = results.Count(r => r.Status == EdcProviderStatus.NoConnector),
            Results            = results.ToList()
        };

        _logger.LogInformation(
            "Catálogo EDC completado: {OK} OK, {Err} errores, {NoConn} sin conector",
            response.SuccessCount, response.ErrorCount, response.NoConnectorCount);

        return response;
    }
}
