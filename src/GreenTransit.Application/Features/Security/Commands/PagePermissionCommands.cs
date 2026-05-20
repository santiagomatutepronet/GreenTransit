using FluentValidation;
using GreenTransit.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;
using GreenTransit.Domain.Entities;

namespace GreenTransit.Application.Features.Security.Commands;

// ── UpdatePagePermissionCommand ───────────────────────────────────────────────

public record UpdatePagePermissionCommand : IRequest
{
    public int IdPageDefinition { get; init; }
    public int IdProfile { get; init; }
    public string? AccessLevel { get; init; }
}

public sealed class UpdatePagePermissionCommandHandler : IRequestHandler<UpdatePagePermissionCommand>
{
    private readonly IApplicationDbContext          _context;
    private readonly ICurrentUserService            _currentUser;
    private readonly ILogger<UpdatePagePermissionCommandHandler> _logger;

    public UpdatePagePermissionCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        ILogger<UpdatePagePermissionCommandHandler> logger)
    {
        _context     = context;
        _currentUser = currentUser;
        _logger      = logger;
    }

    public async Task Handle(UpdatePagePermissionCommand request, CancellationToken ct)
    {
        var page = await _context.PageDefinitions
            .FirstOrDefaultAsync(d => d.ID == request.IdPageDefinition, ct)
            ?? throw new KeyNotFoundException($"PageDefinition {request.IdPageDefinition} no encontrada.");

        var existing = await _context.PagePermissions
            .FirstOrDefaultAsync(p =>
                p.IdPageDefinition == request.IdPageDefinition &&
                p.IdProfile == request.IdProfile, ct);

        if (request.AccessLevel is null)
        {
            if (existing is not null)
            {
                _context.Remove(existing);
                _logger.LogInformation(
                    "Permiso eliminado: página {Route}, perfil {Profile}",
                    page.Route, request.IdProfile);
            }
        }
        else if (existing is not null)
        {
            existing.AccessLevel = request.AccessLevel;
            existing.UpdatedAt   = DateTime.UtcNow;
            existing.IdUser      = _currentUser.IdUser;
            _logger.LogInformation(
                "Permiso actualizado: página {Route}, perfil {Profile}, acceso {Level}",
                page.Route, request.IdProfile, request.AccessLevel);
        }
        else
        {
            _context.Add(new PagePermission
            {
                IdPageDefinition = request.IdPageDefinition,
                IdProfile        = request.IdProfile,
                AccessLevel      = request.AccessLevel,
                CreatedAt        = DateTime.UtcNow,
                IdUser           = _currentUser.IdUser
            });
            _logger.LogInformation(
                "Permiso creado: página {Route}, perfil {Profile}, acceso {Level}",
                page.Route, request.IdProfile, request.AccessLevel);
        }

        await _context.SaveChangesAsync(ct);
    }
}

public sealed class UpdatePagePermissionCommandValidator
    : AbstractValidator<UpdatePagePermissionCommand>
{
    public UpdatePagePermissionCommandValidator()
    {
        RuleFor(x => x.IdPageDefinition).GreaterThan(0);
        RuleFor(x => x.IdProfile).GreaterThan(0);
        RuleFor(x => x.AccessLevel)
            .Must(v => v is null || v == "Read" || v == "Write" || v == "ReadWrite")
            .WithMessage("AccessLevel debe ser 'Read', 'Write', 'ReadWrite' o null.");
    }
}

// ── BulkUpdatePagePermissionsCommand ─────────────────────────────────────────

public record PagePermissionEntry
{
    public int IdPageDefinition { get; init; }
    public int IdProfile { get; init; }
    public string? AccessLevel { get; init; }
}

public record BulkUpdatePagePermissionsCommand : IRequest
{
    public List<PagePermissionEntry> Entries { get; init; } = [];
}

public sealed class BulkUpdatePagePermissionsCommandHandler
    : IRequestHandler<BulkUpdatePagePermissionsCommand>
{
    private readonly IApplicationDbContext               _context;
    private readonly ICurrentUserService                 _currentUser;
    private readonly IPagePermissionService              _permissionService;
    private readonly ILogger<BulkUpdatePagePermissionsCommandHandler> _logger;

    public BulkUpdatePagePermissionsCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IPagePermissionService permissionService,
        ILogger<BulkUpdatePagePermissionsCommandHandler> logger)
    {
        _context           = context;
        _currentUser       = currentUser;
        _permissionService = permissionService;
        _logger            = logger;
    }

    public async Task Handle(BulkUpdatePagePermissionsCommand request, CancellationToken ct)
    {
        if (request.Entries.Count == 0) return;

        var pageIds = request.Entries.Select(e => e.IdPageDefinition).Distinct().ToList();

        var existing = await _context.PagePermissions
            .Where(p => pageIds.Contains(p.IdPageDefinition))
            .ToListAsync(ct);

        int changes = 0;
        foreach (var entry in request.Entries)
        {
            var perm = existing.FirstOrDefault(p =>
                p.IdPageDefinition == entry.IdPageDefinition &&
                p.IdProfile == entry.IdProfile);

            if (entry.AccessLevel is null)
            {
                if (perm is not null)
                {
                    _context.Remove(perm);
                    changes++;
                }
            }
            else if (perm is not null)
            {
                if (perm.AccessLevel != entry.AccessLevel)
                {
                    perm.AccessLevel = entry.AccessLevel;
                    perm.UpdatedAt   = DateTime.UtcNow;
                    perm.IdUser      = _currentUser.IdUser;
                    changes++;
                }
            }
            else
            {
                _context.Add(new PagePermission
                {
                    IdPageDefinition = entry.IdPageDefinition,
                    IdProfile        = entry.IdProfile,
                    AccessLevel      = entry.AccessLevel,
                    CreatedAt        = DateTime.UtcNow,
                    IdUser           = _currentUser.IdUser
                });
                changes++;
            }
        }

        await _context.SaveChangesAsync(ct);

        // Invalidar caché de permisos para todos los perfiles afectados
        var affectedProfiles = request.Entries.Select(e => e.IdProfile).Distinct();
        foreach (var profileId in affectedProfiles)
            await _permissionService.InvalidateCacheForProfileAsync(profileId);

        _logger.LogInformation(
            "Actualización masiva de permisos: {Count} cambios por usuario {IdUser}",
            changes, _currentUser.IdUser);
    }
}

// ── UpdatePageDefinitionCommand ───────────────────────────────────────────────

public record UpdatePageDefinitionCommand : IRequest
{
    public int Id { get; init; }
    public string PageName { get; init; } = string.Empty;
    public string ModuleName { get; init; } = string.Empty;
    public int SortOrder { get; init; }
    public bool IsActive { get; init; }
}

public sealed class UpdatePageDefinitionCommandHandler
    : IRequestHandler<UpdatePageDefinitionCommand>
{
    private readonly IApplicationDbContext _context;

    public UpdatePageDefinitionCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(UpdatePageDefinitionCommand request, CancellationToken ct)
    {
        var page = await _context.PageDefinitions
            .FirstOrDefaultAsync(d => d.ID == request.Id, ct)
            ?? throw new KeyNotFoundException($"PageDefinition {request.Id} no encontrada.");

        page.PageName   = request.PageName;
        page.ModuleName = request.ModuleName;
        page.SortOrder  = request.SortOrder;
        page.IsActive   = request.IsActive;
        page.UpdatedAt  = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);
    }
}

// ── SyncPageDefinitionsCommand ────────────────────────────────────────────────

public record SyncPageDefinitionsCommand : IRequest<int>;

public sealed class SyncPageDefinitionsCommandHandler
    : IRequestHandler<SyncPageDefinitionsCommand, int>
{
    private readonly IPageDiscoveryService _discovery;

    public SyncPageDefinitionsCommandHandler(IPageDiscoveryService discovery)
    {
        _discovery = discovery;
    }

    public async Task<int> Handle(SyncPageDefinitionsCommand request, CancellationToken ct)
        => await _discovery.SyncPageDefinitionsAsync(ct);
}
