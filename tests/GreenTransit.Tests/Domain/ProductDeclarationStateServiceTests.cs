using FluentAssertions;
using GreenTransit.Domain.Entities;
using GreenTransit.Domain.Exceptions;
using GreenTransit.Domain.Services;

namespace GreenTransit.Tests.Domain;

/// <summary>Tests del servicio de dominio ProductDeclarationStateService.</summary>
public sealed class ProductDeclarationStateServiceTests
{
    private readonly ProductDeclarationStateService _svc = new();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ProductDeclaration BuildDraft(bool withProducts = true, bool withProducer = true)
    {
        var pd = new ProductDeclaration
        {
            Id         = Guid.NewGuid(),
            State      = ProductDeclaration.States.Draft,
            IdProducer = withProducer ? Guid.NewGuid() : null,
            Year       = 2025,
            Period     = 1
        };
        if (withProducts)
            pd.Products.Add(new Product { Id = Guid.NewGuid(), Quantity = 100 });
        return pd;
    }

    // ── Emit ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Emit_WhenDraftWithProductsAndProducer_ShouldTransitionToIssued()
    {
        var pd = BuildDraft();

        _svc.Emit(pd);

        pd.State.Should().Be(ProductDeclaration.States.Issued);
        pd.DateEmit.Should().NotBeNull();
    }

    [Fact]
    public void Emit_WhenNoProducts_ShouldThrowDomainException()
    {
        var pd = BuildDraft(withProducts: false);

        var act = () => _svc.Emit(pd);

        act.Should().Throw<DomainException>()
           .WithMessage("*al menos una línea*");
    }

    [Fact]
    public void Emit_WhenNoProducer_ShouldThrowDomainException()
    {
        var pd = BuildDraft(withProducer: false);

        var act = () => _svc.Emit(pd);

        act.Should().Throw<DomainException>()
           .WithMessage("*productor*");
    }

    [Fact]
    public void Emit_WhenNotDraft_ShouldThrowDomainException()
    {
        var pd = BuildDraft();
        pd.State = ProductDeclaration.States.Issued;

        var act = () => _svc.Emit(pd);

        act.Should().Throw<DomainException>()
           .WithMessage($"*{ProductDeclaration.States.Draft}*");
    }

    [Fact]
    public void Emit_WhenNoYear_ShouldThrowDomainException()
    {
        var pd = BuildDraft();
        pd.Year = null;

        var act = () => _svc.Emit(pd);

        act.Should().Throw<DomainException>()
           .WithMessage("*año*");
    }

    [Fact]
    public void Emit_WhenNoPeriod_ShouldThrowDomainException()
    {
        var pd = BuildDraft();
        pd.Period = null;

        var act = () => _svc.Emit(pd);

        act.Should().Throw<DomainException>()
           .WithMessage("*periodo*");
    }

    // ── Validate ──────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_WhenIssued_ShouldTransitionToValidated()
    {
        var pd = BuildDraft();
        pd.State = ProductDeclaration.States.Issued;

        _svc.Validate(pd);

        pd.State.Should().Be(ProductDeclaration.States.Validated);
    }

    [Fact]
    public void Validate_WhenNotIssued_ShouldThrowDomainException()
    {
        var pd = BuildDraft();
        pd.State = ProductDeclaration.States.Draft;

        var act = () => _svc.Validate(pd);

        act.Should().Throw<DomainException>()
           .WithMessage($"*{ProductDeclaration.States.Issued}*");
    }

    // ── Reject ────────────────────────────────────────────────────────────────

    [Fact]
    public void Reject_WhenIssuedWithReason_ShouldTransitionToRejected()
    {
        var pd = BuildDraft();
        pd.State = ProductDeclaration.States.Issued;

        _svc.Reject(pd, "Datos incorrectos en las líneas de producto.");

        pd.State.Should().Be(ProductDeclaration.States.Rejected);
    }

    [Fact]
    public void Reject_WhenIssuedWithEmptyReason_ShouldThrowDomainException()
    {
        var pd = BuildDraft();
        pd.State = ProductDeclaration.States.Issued;

        var act = () => _svc.Reject(pd, "");

        act.Should().Throw<DomainException>()
           .WithMessage("*motivo*");
    }

    [Fact]
    public void Reject_WhenNotIssued_ShouldThrowDomainException()
    {
        var pd = BuildDraft();
        pd.State = ProductDeclaration.States.Validated;

        var act = () => _svc.Reject(pd, "Motivo");

        act.Should().Throw<DomainException>()
           .WithMessage($"*{ProductDeclaration.States.Issued}*");
    }

    // ── ReturnToDraft ─────────────────────────────────────────────────────────

    [Fact]
    public void ReturnToDraft_WhenRejected_ShouldTransitionToDraft()
    {
        var pd = BuildDraft();
        pd.State = ProductDeclaration.States.Rejected;

        _svc.ReturnToDraft(pd);

        pd.State.Should().Be(ProductDeclaration.States.Draft);
    }

    [Fact]
    public void ReturnToDraft_WhenNotRejected_ShouldThrowDomainException()
    {
        var pd = BuildDraft();
        pd.State = ProductDeclaration.States.Validated;

        var act = () => _svc.ReturnToDraft(pd);

        act.Should().Throw<DomainException>()
           .WithMessage($"*{ProductDeclaration.States.Rejected}*");
    }

    // ── Transiciones inválidas ────────────────────────────────────────────────

    [Fact]
    public void Validate_WhenDraft_ShouldThrowDomainException()
    {
        var pd = BuildDraft();
        // Draft → Validated no está permitido: pasa por Emitido primero

        var act = () => _svc.Validate(pd);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void ReturnToDraft_WhenValidated_ShouldThrowDomainException()
    {
        var pd = BuildDraft();
        pd.State = ProductDeclaration.States.Validated;

        var act = () => _svc.ReturnToDraft(pd);

        act.Should().Throw<DomainException>();
    }
}
