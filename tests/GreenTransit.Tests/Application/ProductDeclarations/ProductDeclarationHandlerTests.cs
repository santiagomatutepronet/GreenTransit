using FluentAssertions;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.ProductDeclarations.Commands;
using GreenTransit.Application.Features.ProductDeclarations.Queries;
using GreenTransit.Domain.Authorization;
using GreenTransit.Domain.Entities;
using GreenTransit.Domain.Exceptions;
using GreenTransit.Domain.Services;
using GreenTransit.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Tests.Application.ProductDeclarations;

/// <summary>Tests de integración de commands y queries del módulo de Declaraciones de Producción.</summary>
public sealed class ProductDeclarationHandlerTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static readonly Guid ProducerEntityId =
        Guid.Parse("cccccccc-0000-0000-0000-000000000003");

    private static readonly Guid ScrapEntityId =
        Guid.Parse("dddddddd-0000-0000-0000-000000000004");

    private static readonly Guid ProducerEntityId2 =
        Guid.Parse("eeeeeeee-0000-0000-0000-000000000005");

    private static readonly IProductDeclarationNotificationService NullNotifier =
        new NullProductDeclarationNotificationService();

    private static FakeCurrentUserService AdminUser()    => new(FakeCurrentUserService.TenantA, ProfileConstants.Admin);
    private static GreenTransit.Application.Common.Interfaces.ICurrentUserService ProducerUser() => new FakeProducerService(FakeCurrentUserService.TenantA, ProducerEntityId);
    private static GreenTransit.Application.Common.Interfaces.ICurrentUserService ScrapUser()    => new FakeScrapService(FakeCurrentUserService.TenantA, ScrapEntityId);

    private static CreateProductDeclarationCommand BuildCreateCmd(
        Guid? idProducer = null,
        int   year       = 2025,
        int   period     = 1,
        string type      = "Declaración Anual") =>
        new(idProducer ?? ProducerEntityId, year, period, null, type, "EUR", "REF-TEST");

    private async Task<Guid> CreateDeclarationAsync(
        GreenTransit.Infrastructure.Persistence.AppDbContext ctx,
        FakeCurrentUserService user,
        Guid? idProducer = null)
    {
        var handler = new CreateProductDeclarationCommandHandler(ctx, user);
        return await handler.Handle(BuildCreateCmd(idProducer), CancellationToken.None);
    }

    // ── 1. Crear declaración ──────────────────────────────────────────────────

    [Fact]
    public async Task Create_WithValidData_ShouldCreateWithDraftState()
    {
        var user = AdminUser();
        await using var ctx = TestDbContextFactory.Create(user);

        var id = await CreateDeclarationAsync(ctx, user);

        var pd = await ctx.ProductDeclarations.FindAsync(id);
        pd.Should().NotBeNull();
        pd!.State.Should().Be(ProductDeclaration.States.Draft);
        pd.OwnerId.Should().Be(FakeCurrentUserService.TenantA);
        pd.IdProducer.Should().Be(ProducerEntityId);
    }

    // ── 2. Duplicado activo bloqueado ─────────────────────────────────────────

    [Fact]
    public async Task Create_WhenActiveDuplicateExists_ShouldThrow()
    {
        var user = AdminUser();
        await using var ctx = TestDbContextFactory.Create(user);

        await CreateDeclarationAsync(ctx, user); // primera declaración

        var handler = new CreateProductDeclarationCommandHandler(ctx, user);
        var act     = () => handler.Handle(BuildCreateCmd(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
                 .WithMessage("*ya existe*");
    }

    // ── 3. Añadir línea recalcula Amount ──────────────────────────────────────

    [Fact]
    public async Task AddProduct_ShouldAddLineAndRecalculateAmount()
    {
        var user = AdminUser();
        await using var ctx = TestDbContextFactory.Create(user);

        // Precondición: residuo de tipo Product
        var residue = new Residue { Id = Guid.NewGuid(), ResidueType = "Product", Name = "Envase PET" };
        ctx.Residues.Add(residue);
        await ctx.SaveChangesAsync();

        var id      = await CreateDeclarationAsync(ctx, user);
        var handler = new AddProductToDeclarationCommandHandler(ctx, user);

        await handler.Handle(new AddProductToDeclarationCommand(
            id, residue.Id, "REF-01", "Comercialización",
            null, null,
            1000m, "Kg", 50, 2.50m), CancellationToken.None);

        var pd = await ctx.ProductDeclarations.Include(d => d.Products).FirstAsync(d => d.Id == id);
        pd.Products.Should().HaveCount(1);
        pd.Amount.Should().Be(2500m); // 1000 × 2.50
    }

    // ── 4. Emitir asigna DateEmit ─────────────────────────────────────────────

    [Fact]
    public async Task Emit_WhenHasProducts_ShouldSetDateEmit()
    {
        var user = AdminUser();
        await using var ctx = TestDbContextFactory.Create(user);

        var residue = new Residue { Id = Guid.NewGuid(), ResidueType = "Product", Name = "Envase vidrio" };
        ctx.Residues.Add(residue);
        await ctx.SaveChangesAsync();

        var id = await CreateDeclarationAsync(ctx, user);

        // Añadir línea (requerido para emitir)
        var addHandler = new AddProductToDeclarationCommandHandler(ctx, user);
        await addHandler.Handle(new AddProductToDeclarationCommand(
            id, residue.Id, null, null, null, null, 500m, "Kg", null, null), CancellationToken.None);

        // Completar campos obligatorios
        var pd = await ctx.ProductDeclarations.FindAsync(id);
        pd!.IdProducer = ProducerEntityId;
        pd.Year   = 2025;
        pd.Period = 1;
        await ctx.SaveChangesAsync();

        var svc = new ProductDeclarationStateService();
        var emitHandler = new EmitProductDeclarationCommandHandler(ctx, user, svc, NullNotifier);
        await emitHandler.Handle(new EmitProductDeclarationCommand(id), CancellationToken.None);

        pd = await ctx.ProductDeclarations.FindAsync(id);
        pd!.State.Should().Be(ProductDeclaration.States.Issued);
        pd.DateEmit.Should().NotBeNull();
    }

    // ── 5. Emitir sin líneas lanza DomainException ────────────────────────────

    [Fact]
    public async Task Emit_WhenNoProducts_ShouldThrowDomainException()
    {
        var user = AdminUser();
        await using var ctx = TestDbContextFactory.Create(user);

        var id  = await CreateDeclarationAsync(ctx, user);
        var svc = new ProductDeclarationStateService();
        var handler = new EmitProductDeclarationCommandHandler(ctx, user, svc, NullNotifier);

        var act = () => handler.Handle(new EmitProductDeclarationCommand(id), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>()
                 .WithMessage("*al menos una línea*");
    }

    // ── 6. Query PRODUCER solo ve sus declaraciones ───────────────────────────

    [Fact]
    public async Task GetDeclarations_AsProducer_ShouldOnlySeeOwnDeclarations()
    {
        var admin = AdminUser();
        await using var ctx = TestDbContextFactory.Create(admin);

        // Crear 2 declaraciones: una del ProducerEntityId y otra de ProducerEntityId2
        await CreateDeclarationAsync(ctx, admin, ProducerEntityId);
        await CreateDeclarationAsync(ctx, admin, ProducerEntityId2);

        var producerUser = ProducerUser();
        var handler = new GetProductDeclarationsQueryHandler(ctx, producerUser);
        var result  = await handler.Handle(
            new GetProductDeclarationsQuery(), CancellationToken.None);

        result.Items.Should().OnlyContain(pd => pd.IdProducer == ProducerEntityId);
    }

    // ── 7. Query SCRAP solo ve declaraciones de sus productores ──────────────

    [Fact]
    public async Task GetDeclarations_AsScrap_ShouldOnlySeeAdherentProducers()
    {
        var admin = AdminUser();
        await using var ctx = TestDbContextFactory.Create(admin);

        // Crear acuerdo SCRAP ↔ Producer1 (no Producer2)
        var agreement = new Agreement
        {
            Id             = Guid.NewGuid(),
            IdScrap        = ScrapEntityId,
            IdPublicEntity = ProducerEntityId,
            OwnerId        = FakeCurrentUserService.TenantA,
            AgreementNumber    = "AGR-TEST",
            Status             = "Activo",
            EffectiveFrom      = DateTime.UtcNow.AddYears(-1),
            IdUser         = 1
        };
        ctx.Agreements.Add(agreement);
        await ctx.SaveChangesAsync();

        // Crear declaraciones de ambos productores
        await CreateDeclarationAsync(ctx, admin, ProducerEntityId);
        await CreateDeclarationAsync(ctx, admin, ProducerEntityId2);

        var scrapUser = ScrapUser();
        var handler   = new GetProductDeclarationsQueryHandler(ctx, scrapUser);
        var result    = await handler.Handle(
            new GetProductDeclarationsQuery(), CancellationToken.None);

        result.Items.Should().OnlyContain(pd => pd.IdProducer == ProducerEntityId);
        result.Items.Should().NotContain(pd => pd.IdProducer == ProducerEntityId2);
    }

    // ── 8. Validar ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Validate_WhenIssued_ShouldTransitionToValidated()
    {
        var admin = AdminUser();
        await using var ctx = TestDbContextFactory.Create(admin);

        var residue = new Residue { Id = Guid.NewGuid(), ResidueType = "Product", Name = "Aluminio" };
        ctx.Residues.Add(residue);
        await ctx.SaveChangesAsync();

        var id = await CreateDeclarationAsync(ctx, admin);
        var addH = new AddProductToDeclarationCommandHandler(ctx, admin);
        await addH.Handle(new AddProductToDeclarationCommand(
            id, residue.Id, null, null, null, null, 200m, "Kg", null, null), CancellationToken.None);

        var pd = await ctx.ProductDeclarations.FindAsync(id);
        pd!.IdProducer = ProducerEntityId; pd.Year = 2025; pd.Period = 1;
        await ctx.SaveChangesAsync();

        var svc = new ProductDeclarationStateService();
        var emitH     = new EmitProductDeclarationCommandHandler(ctx, admin, svc, NullNotifier);
        var validateH = new ValidateProductDeclarationCommandHandler(ctx, admin, svc, NullNotifier);

        await emitH.Handle(new EmitProductDeclarationCommand(id), CancellationToken.None);
        await validateH.Handle(new ValidateProductDeclarationCommand(id), CancellationToken.None);

        pd = await ctx.ProductDeclarations.FindAsync(id);
        pd!.State.Should().Be(ProductDeclaration.States.Validated);
    }

    // ── 9. Rechazar ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Reject_WhenIssued_ShouldTransitionToRejected()
    {
        var admin = AdminUser();
        await using var ctx = TestDbContextFactory.Create(admin);

        var residue = new Residue { Id = Guid.NewGuid(), ResidueType = "Product", Name = "Papel" };
        ctx.Residues.Add(residue);
        await ctx.SaveChangesAsync();

        var id = await CreateDeclarationAsync(ctx, admin);
        var addH = new AddProductToDeclarationCommandHandler(ctx, admin);
        await addH.Handle(new AddProductToDeclarationCommand(
            id, residue.Id, null, null, null, null, 300m, "Kg", null, null), CancellationToken.None);

        var pd = await ctx.ProductDeclarations.FindAsync(id);
        pd!.IdProducer = ProducerEntityId; pd.Year = 2025; pd.Period = 1;
        await ctx.SaveChangesAsync();

        var svc    = new ProductDeclarationStateService();
        var emitH  = new EmitProductDeclarationCommandHandler(ctx, admin, svc, NullNotifier);
        var rejectH = new RejectProductDeclarationCommandHandler(ctx, admin, svc, NullNotifier);

        await emitH.Handle(new EmitProductDeclarationCommand(id), CancellationToken.None);
        await rejectH.Handle(new RejectProductDeclarationCommand(id, "Datos incompletos en líneas"), CancellationToken.None);

        pd = await ctx.ProductDeclarations.FindAsync(id);
        pd!.State.Should().Be(ProductDeclaration.States.Rejected);
    }
}

// ── Fakes de usuario con LinkedEntityId ──────────────────────────────────────

file sealed class FakeProducerService : GreenTransit.Application.Common.Interfaces.ICurrentUserService
{
    private readonly Guid _linkedId;
    public FakeProducerService(Guid ownerId, Guid linkedId)
    {
        OwnerId    = ownerId;
        _linkedId  = linkedId;
    }
    public bool   IsAuthenticated => true;
    public int    IdUser          => 1;
    public string Login           => "producer@test.dev";
    public string Email           => "producer@test.dev";
    public string UserName        => "Producer User";
    public Guid   OwnerId         { get; }
    public int    ProfileId       => 2;
    public string UserProfile     => ProfileConstants.Producer;
    public Guid?  LinkedEntityId  => _linkedId;
    public bool IsInProfile(string profileRef) =>
        string.Equals(UserProfile, profileRef, StringComparison.OrdinalIgnoreCase);
    public bool IsInAnyProfile(params string[] profileRefs) =>
        profileRefs.Any(p => string.Equals(UserProfile, p, StringComparison.OrdinalIgnoreCase));
}

file sealed class FakeScrapService : GreenTransit.Application.Common.Interfaces.ICurrentUserService
{
    private readonly Guid _linkedId;
    public FakeScrapService(Guid ownerId, Guid linkedId)
    {
        OwnerId   = ownerId;
        _linkedId = linkedId;
    }
    public bool   IsAuthenticated => true;
    public int    IdUser          => 1;
    public string Login           => "scrap@test.dev";
    public string Email           => "scrap@test.dev";
    public string UserName        => "Scrap User";
    public Guid   OwnerId         { get; }
    public int    ProfileId       => 3;
    public string UserProfile     => ProfileConstants.Scrap;
    public Guid?  LinkedEntityId  => _linkedId;
    public bool IsInProfile(string profileRef) =>
        string.Equals(UserProfile, profileRef, StringComparison.OrdinalIgnoreCase);
    public bool IsInAnyProfile(params string[] profileRefs) =>
        profileRefs.Any(p => string.Equals(UserProfile, p, StringComparison.OrdinalIgnoreCase));
}

file sealed class NullProductDeclarationNotificationService
    : GreenTransit.Application.Common.Interfaces.IProductDeclarationNotificationService
{
    public Task NotifyIssuedAsync(Guid id, string? producer, int? year, int? period, CancellationToken ct = default)
        => Task.CompletedTask;
    public Task NotifyValidatedAsync(Guid id, Guid? idProducer, string? reference, CancellationToken ct = default)
        => Task.CompletedTask;
    public Task NotifyRejectedAsync(Guid id, Guid? idProducer, string? reference, string reason, CancellationToken ct = default)
        => Task.CompletedTask;
}
