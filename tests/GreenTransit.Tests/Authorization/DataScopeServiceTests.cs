using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Domain.Authorization;
using GreenTransit.Domain.Entities;
using GreenTransit.Infrastructure.Services;
using Moq;

namespace GreenTransit.Tests.Authorization;

/// <summary>
/// Tests unitarios para DataScopeService.
/// Verifica que ApplyScope filtra (o no) según el perfil del usuario.
/// </summary>
public sealed class DataScopeServiceTests
{
    private static readonly Guid TenantId    = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid EntityId    = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
    private static readonly Guid OtherEntity = Guid.Parse("dddddddd-0000-0000-0000-000000000002");

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static DataScopeService BuildService(
        string  profile,
        Guid?   linkedEntityId = null,
        bool    isAuthenticated = true)
    {
        var mock = new Mock<ICurrentUserService>();
        mock.Setup(s => s.IsAuthenticated).Returns(isAuthenticated);
        mock.Setup(s => s.OwnerId).Returns(TenantId);
        mock.Setup(s => s.ProfileReference).Returns(profile);
        mock.Setup(s => s.LinkedEntityId).Returns(linkedEntityId);
        mock.Setup(s => s.IsInProfile(It.IsAny<string>()))
            .Returns((string p) => string.Equals(p, profile, StringComparison.OrdinalIgnoreCase));
        mock.Setup(s => s.IsInAnyProfile(It.IsAny<string[]>()))
            .Returns((string[] profiles) =>
                profiles.Any(p => string.Equals(p, profile, StringComparison.OrdinalIgnoreCase)));

        return new DataScopeService(mock.Object);
    }

    // ─── HasFullAccess / GetEntityFilter ─────────────────────────────────────

    [Theory]
    [InlineData(ProfileConstants.Admin)]
    [InlineData(ProfileConstants.DispatchOffice)]
    public void Admin_ShouldHaveFullAccess_ForAllAreas(string profile)
    {
        var svc = BuildService(profile);

        foreach (var area in new[]
        {
            DataScopeAreas.ServiceOrders, DataScopeAreas.WasteMoves,
            DataScopeAreas.EntryPlants,   DataScopeAreas.EntryCACs,
            DataScopeAreas.TreatmentPlants, DataScopeAreas.Incidents,
            DataScopeAreas.Residues
        })
        {
            Assert.True(svc.HasFullAccess(area),
                $"{profile} debería tener acceso completo en {area}");
            Assert.Null(svc.GetEntityFilter(area));
        }
    }

    [Fact]
    public void Producer_ShouldHaveEntityFilter_ForServiceOrders()
    {
        var svc    = BuildService(ProfileConstants.Producer, linkedEntityId: EntityId);
        var filter = svc.GetEntityFilter(DataScopeAreas.ServiceOrders);
        Assert.Equal(EntityId, filter);
    }

    [Fact]
    public void Producer_ShouldHaveNoFilter_WhenNoLinkedEntity()
    {
        // Si no hay entidad vinculada, el filtro devuelve null (sin LinkedEntityId no filtra).
        var svc    = BuildService(ProfileConstants.Producer, linkedEntityId: null);
        var filter = svc.GetEntityFilter(DataScopeAreas.ServiceOrders);
        Assert.Null(filter);
    }

    [Fact]
    public void Carrier_ShouldHaveEntityFilter_ForWasteMoves()
    {
        var svc    = BuildService(ProfileConstants.Carrier, linkedEntityId: EntityId);
        var filter = svc.GetEntityFilter(DataScopeAreas.WasteMoves);
        Assert.Equal(EntityId, filter);
    }

    [Fact]
    public void DispatchOffice_ShouldHaveFullAccess_ForWasteMoves()
    {
        var svc    = BuildService(ProfileConstants.DispatchOffice);
        var filter = svc.GetEntityFilter(DataScopeAreas.WasteMoves);
        Assert.Null(filter);
    }

    [Fact]
    public void PlantOp_ShouldNotHaveAdditionalFilter_ForEntryPlants()
    {
        // PLANT_OP: el filtro de OwnerId ya es suficiente; no hay filtro adicional por entidad.
        var svc    = BuildService(ProfileConstants.PlantOp, linkedEntityId: EntityId);
        var filter = svc.GetEntityFilter(DataScopeAreas.EntryPlants);
        Assert.Null(filter);
    }

    [Fact]
    public void CacOp_ShouldNotHaveAdditionalFilter_ForEntryCACs()
    {
        var svc    = BuildService(ProfileConstants.CacOp, linkedEntityId: EntityId);
        var filter = svc.GetEntityFilter(DataScopeAreas.EntryCACs);
        Assert.Null(filter);
    }

    // ─── ApplyScope — ServiceOrders ──────────────────────────────────────────

    [Fact]
    public void Producer_ShouldFilterServiceOrders_ByLinkedEntity()
    {
        var svc = BuildService(ProfileConstants.Producer, linkedEntityId: EntityId);

        var orders = new[]
        {
            new ServiceOrder { Id = Guid.NewGuid(), IdIssuedBy = EntityId,    OwnerId = TenantId },
            new ServiceOrder { Id = Guid.NewGuid(), IdIssuedBy = OtherEntity, OwnerId = TenantId },
            new ServiceOrder { Id = Guid.NewGuid(), IdIssuedBy = null,        OwnerId = TenantId },
        }.AsQueryable();

        var result = svc.ApplyScope(orders).ToList();

        Assert.Single(result);
        Assert.Equal(EntityId, result[0].IdIssuedBy);
    }

    [Fact]
    public void Admin_ShouldNotFilterServiceOrders()
    {
        var svc = BuildService(ProfileConstants.Admin);

        var orders = new[]
        {
            new ServiceOrder { Id = Guid.NewGuid(), IdIssuedBy = EntityId,    OwnerId = TenantId },
            new ServiceOrder { Id = Guid.NewGuid(), IdIssuedBy = OtherEntity, OwnerId = TenantId },
        }.AsQueryable();

        var result = svc.ApplyScope(orders).ToList();

        Assert.Equal(2, result.Count);
    }

    // ─── ApplyScope — WasteMoves ─────────────────────────────────────────────

    [Fact]
    public void Carrier_ShouldFilterWasteMoves_ByLinkedEntity()
    {
        var svc = BuildService(ProfileConstants.Carrier, linkedEntityId: EntityId);

        var moves = new[]
        {
            new WasteMove
            {
                Id = Guid.NewGuid(), OwnerId = TenantId,
                WasteMoveResidues = [new WasteMoveResidue { IdCarrier = EntityId }]
            },
            new WasteMove
            {
                Id = Guid.NewGuid(), OwnerId = TenantId,
                WasteMoveResidues = [new WasteMoveResidue { IdCarrier = OtherEntity }]
            },
            new WasteMove
            {
                Id = Guid.NewGuid(), OwnerId = TenantId,
                WasteMoveResidues = []
            },
        }.AsQueryable();

        var result = svc.ApplyScope(moves).ToList();

        Assert.Single(result);
        Assert.Contains(result, wm => wm.WasteMoveResidues.Any(r => r.IdCarrier == EntityId));
    }

    [Fact]
    public void DispatchOffice_ShouldNotFilterWasteMoves()
    {
        var svc = BuildService(ProfileConstants.DispatchOffice);

        var moves = new[]
        {
            new WasteMove { Id = Guid.NewGuid(), OwnerId = TenantId, WasteMoveResidues = [] },
            new WasteMove { Id = Guid.NewGuid(), OwnerId = TenantId, WasteMoveResidues = [] },
        }.AsQueryable();

        Assert.Equal(2, svc.ApplyScope(moves).Count());
    }

    // ─── ApplyScope — Residues ───────────────────────────────────────────────

    [Fact]
    public void Producer_ShouldFilterResidues_ToOwnProductsOnly()
    {
        var svc = BuildService(ProfileConstants.Producer, linkedEntityId: EntityId);

        var residues = new[]
        {
            new Residue { Id = Guid.NewGuid(), IdProducer = EntityId,    ResidueType = "Product" },
            new Residue { Id = Guid.NewGuid(), IdProducer = EntityId,    ResidueType = "ProductSpec" },
            new Residue { Id = Guid.NewGuid(), IdProducer = EntityId,    ResidueType = "Waste" },
            new Residue { Id = Guid.NewGuid(), IdProducer = OtherEntity, ResidueType = "Product" },
        }.AsQueryable();

        var result = svc.ApplyScope(residues).ToList();

        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.Equal(EntityId, r.IdProducer));
        Assert.All(result, r => Assert.True(r.ResidueType is "Product" or "ProductSpec"));
    }

    [Fact]
    public void Admin_ShouldNotFilterResidues()
    {
        var svc = BuildService(ProfileConstants.Admin);

        var residues = new[]
        {
            new Residue { Id = Guid.NewGuid(), IdProducer = EntityId,    ResidueType = "Product" },
            new Residue { Id = Guid.NewGuid(), IdProducer = OtherEntity, ResidueType = "Waste" },
        }.AsQueryable();

        Assert.Equal(2, svc.ApplyScope(residues).Count());
    }

    // ─── ApplyScope — EntryPlants / EntryCACs (sin filtro adicional) ─────────

    [Fact]
    public void PlantOp_ShouldNotFilterEntryPlants_BeyondOwnerId()
    {
        var svc = BuildService(ProfileConstants.PlantOp, linkedEntityId: EntityId);

        var entries = new[]
        {
            new EntryPlant { Id = Guid.NewGuid(), OwnerId = TenantId },
            new EntryPlant { Id = Guid.NewGuid(), OwnerId = TenantId },
        }.AsQueryable();

        Assert.Equal(2, svc.ApplyScope(entries).Count());
    }

    [Fact]
    public void CacOp_ShouldNotFilterEntryCACs_BeyondOwnerId()
    {
        var svc = BuildService(ProfileConstants.CacOp, linkedEntityId: EntityId);

        var entries = new[]
        {
            new EntryCAC { Id = Guid.NewGuid(), OwnerId = TenantId },
            new EntryCAC { Id = Guid.NewGuid(), OwnerId = TenantId },
        }.AsQueryable();

        Assert.Equal(2, svc.ApplyScope(entries).Count());
    }
}
