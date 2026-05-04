using System.Security.Claims;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Domain.Authorization;
using GreenTransit.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Moq;

namespace GreenTransit.Tests.Authorization;

/// <summary>
/// Tests unitarios para ProfileAuthorizationHandler.
/// Verifica que la matriz de permisos se cumple: qué perfiles pasan qué requirements.
/// </summary>
public sealed class ProfileAuthorizationHandlerTests
{
    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static ProfileAuthorizationHandler BuildHandler(string profileReference)
    {
        var mock = new Mock<ICurrentUserService>();
        mock.Setup(s => s.IsAuthenticated).Returns(true);
        mock.Setup(s => s.ProfileReference).Returns(profileReference);
        mock.Setup(s => s.IsInAnyProfile(It.IsAny<string[]>()))
            .Returns((string[] profiles) =>
                profiles.Any(p => string.Equals(p, profileReference, StringComparison.OrdinalIgnoreCase)));
        return new ProfileAuthorizationHandler(mock.Object);
    }

    private static AuthorizationHandlerContext BuildContext(ProfileRequirement requirement)
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity("test"));
        return new AuthorizationHandlerContext([requirement], user, null);
    }

    private static async Task<bool> EvaluateAsync(string profile, params string[] allowedProfiles)
    {
        var handler     = BuildHandler(profile);
        var requirement = new ProfileRequirement(allowedProfiles);
        var context     = BuildContext(requirement);
        await handler.HandleAsync(context);
        return context.HasSucceeded;
    }

    // ─── ADMIN ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(ProfileConstants.DispatchOffice, ProfileConstants.Admin)]
    [InlineData(ProfileConstants.Admin)]
    [InlineData(ProfileConstants.Scrap, ProfileConstants.Admin)]
    [InlineData(ProfileConstants.PlantOp, ProfileConstants.Admin)]
    [InlineData(ProfileConstants.CacOp, ProfileConstants.Admin)]
    public async Task Admin_ShouldSucceed_ForAllPolicies(params string[] allowedProfiles)
    {
        var result = await EvaluateAsync(ProfileConstants.Admin, allowedProfiles);
        Assert.True(result);
    }

    // ─── PRODUCER ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Producer_ShouldSucceed_ForCanCreateOwnServiceOrders()
    {
        // PRODUCER puede crear SOs propias.
        var result = await EvaluateAsync(
            ProfileConstants.Producer,
            ProfileConstants.Producer, ProfileConstants.PublicEnt,
            ProfileConstants.DispatchOffice, ProfileConstants.Admin);
        Assert.True(result);
    }

    [Fact]
    public async Task Producer_ShouldFail_ForCanManageWasteMoves()
    {
        // PRODUCER no puede crear ni editar traslados.
        var result = await EvaluateAsync(
            ProfileConstants.Producer,
            ProfileConstants.DispatchOffice, ProfileConstants.Admin);
        Assert.False(result);
    }

    [Fact]
    public async Task Producer_ShouldFail_ForCanManageEntryPlants()
    {
        var result = await EvaluateAsync(
            ProfileConstants.Producer,
            ProfileConstants.PlantOp, ProfileConstants.Admin);
        Assert.False(result);
    }

    // ─── CARRIER ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Carrier_ShouldFail_ForCanManageEntryPlants()
    {
        var result = await EvaluateAsync(
            ProfileConstants.Carrier,
            ProfileConstants.PlantOp, ProfileConstants.Admin);
        Assert.False(result);
    }

    [Fact]
    public async Task Carrier_ShouldFail_ForCanManageWasteMoves()
    {
        // CARRIER no crea traslados; solo actualiza los asignados (OwnDataRequirement).
        var result = await EvaluateAsync(
            ProfileConstants.Carrier,
            ProfileConstants.DispatchOffice, ProfileConstants.Admin);
        Assert.False(result);
    }

    [Fact]
    public async Task Carrier_ShouldFail_ForCanManageServiceOrders()
    {
        var result = await EvaluateAsync(
            ProfileConstants.Carrier,
            ProfileConstants.DispatchOffice, ProfileConstants.Admin);
        Assert.False(result);
    }

    // ─── DISPATCH_OFFICE ─────────────────────────────────────────────────────

    [Fact]
    public async Task DispatchOffice_ShouldSucceed_ForCanManageWasteMoves()
    {
        var result = await EvaluateAsync(
            ProfileConstants.DispatchOffice,
            ProfileConstants.DispatchOffice, ProfileConstants.Admin);
        Assert.True(result);
    }

    [Fact]
    public async Task DispatchOffice_ShouldSucceed_ForCanManageEntities()
    {
        var result = await EvaluateAsync(
            ProfileConstants.DispatchOffice,
            ProfileConstants.DispatchOffice, ProfileConstants.Admin);
        Assert.True(result);
    }

    [Fact]
    public async Task DispatchOffice_ShouldFail_ForAdminOnlyPolicies()
    {
        // DISPATCH_OFFICE no gestiona usuarios ni perfiles.
        var result = await EvaluateAsync(
            ProfileConstants.DispatchOffice,
            ProfileConstants.Admin);
        Assert.False(result);
    }

    // ─── PLANT_OP ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task PlantOp_ShouldSucceed_ForCanManageEntryPlants()
    {
        var result = await EvaluateAsync(
            ProfileConstants.PlantOp,
            ProfileConstants.PlantOp, ProfileConstants.Admin);
        Assert.True(result);
    }

    [Fact]
    public async Task PlantOp_ShouldSucceed_ForCanManageTreatments()
    {
        var result = await EvaluateAsync(
            ProfileConstants.PlantOp,
            ProfileConstants.PlantOp, ProfileConstants.Admin);
        Assert.True(result);
    }

    [Fact]
    public async Task PlantOp_ShouldFail_ForCanManageEntryCACs()
    {
        var result = await EvaluateAsync(
            ProfileConstants.PlantOp,
            ProfileConstants.CacOp, ProfileConstants.Admin);
        Assert.False(result);
    }

    // ─── CAC_OP ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task CacOp_ShouldSucceed_ForCanManageEntryCACs()
    {
        var result = await EvaluateAsync(
            ProfileConstants.CacOp,
            ProfileConstants.CacOp, ProfileConstants.Admin);
        Assert.True(result);
    }

    [Fact]
    public async Task CacOp_ShouldFail_ForCanManageEntryPlants()
    {
        // CAC_OP no opera entradas de planta.
        var result = await EvaluateAsync(
            ProfileConstants.CacOp,
            ProfileConstants.PlantOp, ProfileConstants.Admin);
        Assert.False(result);
    }

    // ─── Unauthenticated ─────────────────────────────────────────────────────

    [Fact]
    public async Task UnauthenticatedUser_ShouldFail_ForAnyRequirement()
    {
        var mock = new Mock<ICurrentUserService>();
        mock.Setup(s => s.IsAuthenticated).Returns(false);
        mock.Setup(s => s.ProfileReference).Returns(string.Empty);
        mock.Setup(s => s.IsInAnyProfile(It.IsAny<string[]>())).Returns(false);

        var handler     = new ProfileAuthorizationHandler(mock.Object);
        var requirement = new ProfileRequirement(ProfileConstants.Admin);
        var context     = BuildContext(requirement);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }
}
