using GreenTransit.Application.Common.Behaviours;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Exceptions;
using GreenTransit.Domain.Authorization;
using MediatR;
using Moq;

namespace GreenTransit.Tests.Authorization;

/// <summary>
/// Tests unitarios para AuthorizationBehavior.
/// Verifica que el pipeline MediatR aplica correctamente los [Authorize] attributes.
/// </summary>
public sealed class AuthorizationBehaviorTests
{
    // ─── Requests de prueba ───────────────────────────────────────────────────

    /// <summary>Request sin restricción — pasa siempre.</summary>
    public record OpenRequest : IRequest<string>;

    /// <summary>Request restringido a ADMIN y DISPATCH_OFFICE por perfil.</summary>
    [Authorize(Profiles = $"{ProfileConstants.Admin},{ProfileConstants.DispatchOffice}")]
    public record AdminOnlyRequest : IRequest<string>;

    /// <summary>Request restringido solo por policy (sin Profiles).</summary>
    [Authorize(Policy = "CanManageUsers")]
    public record PolicyOnlyRequest : IRequest<string>;

    /// <summary>Request con dos [Authorize] — ambos deben cumplirse (AND lógico).</summary>
    [Authorize(Profiles = $"{ProfileConstants.Admin}")]
    [Authorize(Policy = "CanManageUsers")]
    public record MultipleAttributeRequest : IRequest<string>;

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static (AuthorizationBehavior<TReq, string> behavior,
                    RequestHandlerDelegate<string>       next)
        Build<TReq>(string profile, bool policyResult = true)
        where TReq : notnull
    {
        var userMock = new Mock<ICurrentUserService>();
        userMock.Setup(s => s.IsAuthenticated).Returns(true);
        userMock.Setup(s => s.ProfileReference).Returns(profile);
        userMock.Setup(s => s.IsInAnyProfile(It.IsAny<string[]>()))
            .Returns((string[] profiles) =>
                profiles.Any(p => string.Equals(p, profile, StringComparison.OrdinalIgnoreCase)));

        var policyMock = new Mock<IPolicyEvaluator>();
        policyMock.Setup(p => p.AuthorizeAsync(It.IsAny<string>()))
            .ReturnsAsync(policyResult);

        var behavior = new AuthorizationBehavior<TReq, string>(
            userMock.Object, policyMock.Object);

        RequestHandlerDelegate<string> next = _ => Task.FromResult("ok");
        return (behavior, next);
    }

    private static (AuthorizationBehavior<TReq, string> behavior,
                    RequestHandlerDelegate<string>       next)
        BuildUnauthenticated<TReq>()
        where TReq : notnull
    {
        var userMock = new Mock<ICurrentUserService>();
        userMock.Setup(s => s.IsAuthenticated).Returns(false);

        var policyMock = new Mock<IPolicyEvaluator>();

        var behavior = new AuthorizationBehavior<TReq, string>(
            userMock.Object, policyMock.Object);

        RequestHandlerDelegate<string> next = _ => Task.FromResult("ok");
        return (behavior, next);
    }

    // ─── Sin [Authorize] — pasa siempre

    [Fact]
    public async Task Request_WithoutAuthorizeAttribute_ShouldPassThrough()
    {
        var (behavior, next) = Build<OpenRequest>(ProfileConstants.Producer);

        var result = await behavior.Handle(new OpenRequest(), next, CancellationToken.None);

        Assert.Equal("ok", result);
    }

    // ─── Autenticación básica ─────────────────────────────────────────────────

    [Fact]
    public async Task UnauthenticatedUser_WithAuthorizeAttribute_ShouldThrowForbidden()
    {
        var (behavior, next) = BuildUnauthenticated<AdminOnlyRequest>();

        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => behavior.Handle(new AdminOnlyRequest(), next, CancellationToken.None));
    }

    // ─── Comprobación por perfiles ────────────────────────────────────────────

    [Fact]
    public async Task Request_WithAuthorizeAttribute_ShouldSucceed_WhenInProfile()
    {
        var (behavior, next) = Build<AdminOnlyRequest>(ProfileConstants.Admin);

        var result = await behavior.Handle(new AdminOnlyRequest(), next, CancellationToken.None);

        Assert.Equal("ok", result);
    }

    [Fact]
    public async Task Request_WithAuthorizeAttribute_ShouldSucceed_ForDispatchOffice()
    {
        var (behavior, next) = Build<AdminOnlyRequest>(ProfileConstants.DispatchOffice);

        var result = await behavior.Handle(new AdminOnlyRequest(), next, CancellationToken.None);

        Assert.Equal("ok", result);
    }

    [Fact]
    public async Task Request_WithAuthorizeAttribute_ShouldFail_WhenNotInProfile()
    {
        var (behavior, next) = Build<AdminOnlyRequest>(ProfileConstants.Carrier);

        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => behavior.Handle(new AdminOnlyRequest(), next, CancellationToken.None));
    }

    [Fact]
    public async Task Producer_ShouldFail_ForAdminOnlyRequest()
    {
        var (behavior, next) = Build<AdminOnlyRequest>(ProfileConstants.Producer);

        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => behavior.Handle(new AdminOnlyRequest(), next, CancellationToken.None));
    }

    // ─── Comprobación por policy ──────────────────────────────────────────────

    [Fact]
    public async Task Request_WithPolicyAttribute_ShouldSucceed_WhenPolicyPasses()
    {
        var (behavior, next) = Build<PolicyOnlyRequest>(ProfileConstants.Admin, policyResult: true);

        var result = await behavior.Handle(new PolicyOnlyRequest(), next, CancellationToken.None);

        Assert.Equal("ok", result);
    }

    [Fact]
    public async Task Request_WithPolicyAttribute_ShouldFail_WhenPolicyFails()
    {
        var (behavior, next) = Build<PolicyOnlyRequest>(ProfileConstants.Carrier, policyResult: false);

        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => behavior.Handle(new PolicyOnlyRequest(), next, CancellationToken.None));
    }

    // ─── Múltiples [Authorize] — AND lógico ──────────────────────────────────

    [Fact]
    public async Task Request_WithMultipleAuthorizeAttributes_ShouldSucceed_WhenAllPass()
    {
        // Profile = ADMIN + Policy = true → ambos pasan.
        var (behavior, next) = Build<MultipleAttributeRequest>(ProfileConstants.Admin, policyResult: true);

        var result = await behavior.Handle(new MultipleAttributeRequest(), next, CancellationToken.None);

        Assert.Equal("ok", result);
    }

    [Fact]
    public async Task Request_WithMultipleAuthorizeAttributes_ShouldFail_WhenProfileFails()
    {
        // Profile = CARRIER (no en [ADMIN]) → falla aunque la policy pase.
        var (behavior, next) = Build<MultipleAttributeRequest>(ProfileConstants.Carrier, policyResult: true);

        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => behavior.Handle(new MultipleAttributeRequest(), next, CancellationToken.None));
    }

    [Fact]
    public async Task Request_WithMultipleAuthorizeAttributes_ShouldFail_WhenPolicyFails()
    {
        // Profile = ADMIN (pasa) pero Policy = false → falla el segundo atributo.
        var (behavior, next) = Build<MultipleAttributeRequest>(ProfileConstants.Admin, policyResult: false);

        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => behavior.Handle(new MultipleAttributeRequest(), next, CancellationToken.None));
    }
}
