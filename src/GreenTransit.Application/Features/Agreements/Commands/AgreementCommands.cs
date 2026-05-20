using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentValidation;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Domain.Authorization;
using GreenTransit.Domain.Constants;
using GreenTransit.Domain.Entities;
using MediatR;

namespace GreenTransit.Application.Features.Agreements.Commands;

// ── CreateAgreementCommand ────────────────────────────────────────────────────

/// <summary>Crea un acuerdo marco en estado Draft. Solo ADMIN y SCRAP.</summary>
public sealed record CreateAgreementCommand(
    // Partes
    Guid  IdScrap,
    Guid  IdPublicEntity,
    Guid? IdCoordinator,
    // Ámbito
    string? WasteStream,
    string? SubStream,
    string? AutonomousCommunity,
    string? ProvinceCode,
    string? MunicipalityCode,
    string? CoveredMethodsJson,
    // Economía
    string? TariffModelType,
    string? TariffRulesJson,
    string? MinimumsJson,
    string? ObligationsJson,
    string  Currency,
    // Vigencia
    DateTime  EffectiveFrom,
    DateTime? EffectiveTo
) : IRequest<Guid>;

public sealed class CreateAgreementCommandHandler : IRequestHandler<CreateAgreementCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public CreateAgreementCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<Guid> Handle(CreateAgreementCommand request, CancellationToken ct)
    {
        if (!_currentUser.IsInAnyProfile(ProfileConstants.Admin, ProfileConstants.Scrap))
            throw new UnauthorizedAccessException("Solo ADMIN y SCRAP pueden crear acuerdos.");

        var ownerId = _currentUser.OwnerId;
        var number  = await GenerateAgreementNumberAsync(ownerId, ct);
        var now     = DateTime.UtcNow;

        var agreement = new Agreement
        {
            Id                 = Guid.NewGuid(),
            OwnerId            = ownerId,
            AgreementNumber    = number,
            Status             = Agreement.Statuses.Draft,
            IdScrap            = request.IdScrap,
            IdPublicEntity     = request.IdPublicEntity,
            IdCoordinator      = request.IdCoordinator,
            WasteStream        = request.WasteStream,
            SubStream          = request.SubStream,
            AutonomousCommunity = request.AutonomousCommunity,
            ProvinceCode       = request.ProvinceCode,
            MunicipalityCode   = request.MunicipalityCode,
            CoveredMethodsJson = request.CoveredMethodsJson,
            TariffModelType    = request.TariffModelType,
            TariffRulesJson    = request.TariffRulesJson,
            MinimumsJson       = request.MinimumsJson,
            ObligationsJson    = request.ObligationsJson,
            Currency           = request.Currency,
            EffectiveFrom      = request.EffectiveFrom,
            EffectiveTo        = request.EffectiveTo,
            Version            = 1,
            CreatedAt          = now,
            UpdatedAt          = now,
            IdUser             = _currentUser.IdUser
        };

        agreement.Hash = ComputeHash(agreement);

        _context.Add(agreement);
        await _context.SaveChangesAsync(ct);
        return agreement.Id;
    }

    private async Task<string> GenerateAgreementNumberAsync(Guid? ownerId, CancellationToken ct)
    {
        var year  = DateTime.UtcNow.Year;
        var count = await _context.Agreements
            .CountAsync(a => a.OwnerId == ownerId && a.CreatedAt.Year == year, ct);
        return $"AGR-{year}-{(count + 1):D4}";
    }

    internal static string ComputeHash(Agreement a)
    {
        var payload = JsonSerializer.Serialize(new
        {
            a.AgreementNumber, a.IdScrap, a.IdPublicEntity, a.IdCoordinator,
            a.WasteStream, a.AutonomousCommunity, a.ProvinceCode,
            a.TariffModelType, a.TariffRulesJson, a.MinimumsJson,
            a.EffectiveFrom, a.EffectiveTo, a.Version
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }
}

public sealed class CreateAgreementCommandValidator : AbstractValidator<CreateAgreementCommand>
{
    private readonly IApplicationDbContext _context;

    public CreateAgreementCommandValidator(IApplicationDbContext context)
    {
        _context = context;

        RuleFor(x => x.IdScrap)
            .NotEmpty().WithMessage("El SCRAP es obligatorio.")
            .MustAsync(BeScrapEntityAsync).WithMessage("La entidad indicada no tiene rol SCRAP.");

        RuleFor(x => x.IdPublicEntity)
            .NotEmpty().WithMessage("La entidad pública es obligatoria.")
            .MustAsync(BePublicEntityAsync).WithMessage("La entidad indicada no tiene rol PublicEntity.");

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("La moneda es obligatoria.");

        RuleFor(x => x.EffectiveFrom)
            .NotEmpty().WithMessage("La fecha de inicio es obligatoria.");

        RuleFor(x => x.EffectiveTo)
            .GreaterThan(x => x.EffectiveFrom)
            .When(x => x.EffectiveTo.HasValue)
            .WithMessage("La fecha de fin debe ser posterior a la de inicio.");
    }

    private async Task<bool> BeScrapEntityAsync(Guid id, CancellationToken ct)
        => await _context.BusinessEntities
            .AnyAsync(e => e.Id == id && e.EntityRole == EntityRoles.SCRAP, ct);

    private async Task<bool> BePublicEntityAsync(Guid id, CancellationToken ct)
        => await _context.BusinessEntities
            .AnyAsync(e => e.Id == id && e.EntityRole == EntityRoles.PublicEntity, ct);
}

// ── UpdateAgreementCommand ────────────────────────────────────────────────────

/// <summary>Actualiza un acuerdo en estado Draft. Incrementa versión y recalcula hash.</summary>
public sealed record UpdateAgreementCommand(
    Guid      Id,
    string?   WasteStream,
    string?   SubStream,
    string?   AutonomousCommunity,
    string?   ProvinceCode,
    string?   MunicipalityCode,
    string?   CoveredMethodsJson,
    string?   TariffModelType,
    string?   TariffRulesJson,
    string?   MinimumsJson,
    string?   ObligationsJson,
    string    Currency,
    DateTime  EffectiveFrom,
    DateTime? EffectiveTo
) : IRequest;

public sealed class UpdateAgreementCommandHandler : IRequestHandler<UpdateAgreementCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public UpdateAgreementCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task Handle(UpdateAgreementCommand request, CancellationToken ct)
    {
        if (!_currentUser.IsInAnyProfile(ProfileConstants.Admin, ProfileConstants.Scrap))
            throw new UnauthorizedAccessException("Solo ADMIN y SCRAP pueden editar acuerdos.");

        var agreement = await _context.Agreements
            .FirstOrDefaultAsync(a => a.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Acuerdo {request.Id} no encontrado.");

        if (agreement.Status != Agreement.Statuses.Draft)
            throw new InvalidOperationException("Solo se pueden editar acuerdos en estado Draft.");

        agreement.WasteStream         = request.WasteStream;
        agreement.SubStream           = request.SubStream;
        agreement.AutonomousCommunity = request.AutonomousCommunity;
        agreement.ProvinceCode        = request.ProvinceCode;
        agreement.MunicipalityCode    = request.MunicipalityCode;
        agreement.CoveredMethodsJson  = request.CoveredMethodsJson;
        agreement.TariffModelType     = request.TariffModelType;
        agreement.TariffRulesJson     = request.TariffRulesJson;
        agreement.MinimumsJson        = request.MinimumsJson;
        agreement.ObligationsJson     = request.ObligationsJson;
        agreement.Currency            = request.Currency;
        agreement.EffectiveFrom       = request.EffectiveFrom;
        agreement.EffectiveTo         = request.EffectiveTo;
        agreement.Version            += 1;
        agreement.UpdatedAt           = DateTime.UtcNow;
        agreement.IdUser              = _currentUser.IdUser;
        agreement.Hash                = CreateAgreementCommandHandler.ComputeHash(agreement);

        await _context.SaveChangesAsync(ct);
    }
}

// ── ActivateAgreementCommand ──────────────────────────────────────────────────

/// <summary>Transición Draft → Active. Valida que la vigencia esté en curso.</summary>
public sealed record ActivateAgreementCommand(Guid Id) : IRequest;

public sealed class ActivateAgreementCommandHandler : IRequestHandler<ActivateAgreementCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public ActivateAgreementCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task Handle(ActivateAgreementCommand request, CancellationToken ct)
    {
        if (!_currentUser.IsInAnyProfile(ProfileConstants.Admin, ProfileConstants.Scrap))
            throw new UnauthorizedAccessException("Solo ADMIN y SCRAP pueden activar acuerdos.");

        var agreement = await _context.Agreements
            .FirstOrDefaultAsync(a => a.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Acuerdo {request.Id} no encontrado.");

        if (agreement.Status != Agreement.Statuses.Draft)
            throw new InvalidOperationException("Solo se pueden activar acuerdos en estado Draft.");

        var today = DateTime.UtcNow.Date;
        if (agreement.EffectiveFrom.Date > today)
            throw new InvalidOperationException(
                $"El acuerdo no puede activarse hasta {agreement.EffectiveFrom:dd/MM/yyyy}.");

        if (agreement.EffectiveTo.HasValue && agreement.EffectiveTo.Value.Date < today)
            throw new InvalidOperationException("El acuerdo ya ha vencido; no puede activarse.");

        agreement.Status    = Agreement.Statuses.Active;
        agreement.UpdatedAt = DateTime.UtcNow;
        agreement.IdUser    = _currentUser.IdUser;

        await _context.SaveChangesAsync(ct);
    }
}

// ── CancelAgreementCommand ────────────────────────────────────────────────────

/// <summary>Transición Active → Cancelled. Motivo obligatorio.</summary>
public sealed record CancelAgreementCommand(Guid Id, string Reason) : IRequest;

public sealed class CancelAgreementCommandHandler : IRequestHandler<CancelAgreementCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public CancelAgreementCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task Handle(CancelAgreementCommand request, CancellationToken ct)
    {
        if (!_currentUser.IsInAnyProfile(ProfileConstants.Admin, ProfileConstants.Scrap))
            throw new UnauthorizedAccessException("Solo ADMIN y SCRAP pueden cancelar acuerdos.");

        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new ArgumentException("El motivo de cancelación es obligatorio.");

        var agreement = await _context.Agreements
            .FirstOrDefaultAsync(a => a.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Acuerdo {request.Id} no encontrado.");

        if (agreement.Status != Agreement.Statuses.Active)
            throw new InvalidOperationException("Solo se pueden cancelar acuerdos en estado Active.");

        agreement.Status    = Agreement.Statuses.Cancelled;
        agreement.UpdatedAt = DateTime.UtcNow;
        agreement.IdUser    = _currentUser.IdUser;

        await _context.SaveChangesAsync(ct);
    }
}

// ── AttachDocumentCommand ─────────────────────────────────────────────────────

/// <summary>Adjunta un documento a un acuerdo existente.</summary>
public sealed record AttachDocumentCommand(
    Guid      AgreementId,
    string    DocumentType,
    string?   DocumentId,
    string?   DocumentHash,
    DateTime? SignedAt,
    string?   SignatureProvider
) : IRequest<Guid>;

public sealed class AttachDocumentCommandHandler : IRequestHandler<AttachDocumentCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public AttachDocumentCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<Guid> Handle(AttachDocumentCommand request, CancellationToken ct)
    {
        if (!_currentUser.IsInAnyProfile(ProfileConstants.Admin, ProfileConstants.Scrap, ProfileConstants.PublicEnt))
            throw new UnauthorizedAccessException("No tienes permiso para adjuntar documentos.");

        var exists = await _context.Agreements
            .AnyAsync(a => a.Id == request.AgreementId, ct);

        if (!exists)
            throw new KeyNotFoundException($"Acuerdo {request.AgreementId} no encontrado.");

        var doc = new AgreementDocument
        {
            Id                = Guid.NewGuid(),
            AgreementId       = request.AgreementId,
            DocumentType      = request.DocumentType,
            DocumentId        = request.DocumentId,
            DocumentHash      = request.DocumentHash,
            SignedAt          = request.SignedAt,
            SignatureProvider = request.SignatureProvider
        };

        _context.Add(doc);
        await _context.SaveChangesAsync(ct);
        return doc.Id;
    }
}

public sealed class AttachDocumentCommandValidator : AbstractValidator<AttachDocumentCommand>
{
    public AttachDocumentCommandValidator()
    {
        RuleFor(x => x.AgreementId).NotEmpty();
        RuleFor(x => x.DocumentType)
            .NotEmpty()
            .Must(t => DocumentTypes.All.Contains(t))
            .WithMessage($"Tipo de documento inválido. Valores permitidos: {string.Join(", ", DocumentTypes.All)}.");
    }
}

/// <summary>Tipos de documento permitidos en AgreementDocument.</summary>
public static class DocumentTypes
{
    public const string Contrato = "Contrato";
    public const string Anexo    = "Anexo";
    public const string Acta     = "Acta";

    public static readonly IReadOnlyList<string> All = [Contrato, Anexo, Acta];
}
