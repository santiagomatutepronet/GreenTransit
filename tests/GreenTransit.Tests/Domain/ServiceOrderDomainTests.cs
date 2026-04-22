using FluentAssertions;
using GreenTransit.Domain.Entities;

namespace GreenTransit.Tests.Domain;

/// <summary>
/// Tests de las reglas de dominio de la entidad ServiceOrder.
/// Verifican contratos de datos sin dependencias de infraestructura.
/// </summary>
public sealed class ServiceOrderDomainTests
{
    [Fact]
    public void ServiceOrderNumber_WhenSet_ShouldNotBeNullOrWhiteSpace()
    {
        // Arrange & Act
        var order = new ServiceOrder { ServiceOrderNumber = "SO-TEST-001" };

        // Assert
        order.ServiceOrderNumber.Should().NotBeNullOrWhiteSpace();
        order.ServiceOrderNumber.Should().Be("SO-TEST-001");
    }

    [Fact]
    public void OwnerId_WhenAssigned_ShouldMatchExpectedValue()
    {
        // Arrange
        var expectedOwnerId = Guid.NewGuid();

        // Act
        var order = new ServiceOrder { OwnerId = expectedOwnerId };

        // Assert
        order.OwnerId.Should().NotBeNull();
        order.OwnerId.Should().Be(expectedOwnerId);
    }

    [Fact]
    public void OwnerId_WhenNotAssigned_ShouldBeNull()
    {
        // La propiedad OwnerId es nullable: sin asignación debe ser null
        var order = new ServiceOrder();

        order.OwnerId.Should().BeNull();
    }

    [Fact]
    public void ServiceOrder_ImplementsITenantEntity()
    {
        // Verifica que ServiceOrder participa en el mecanismo multi-tenant
        var order = new ServiceOrder();

        order.Should().BeAssignableTo<GreenTransit.Domain.Interfaces.ITenantEntity>();
    }

    [Fact]
    public void ServiceOrder_ImplementsIAuditableEntity()
    {
        // Verifica que ServiceOrder tiene campos de auditoría CreatedAt / UpdatedAt
        var order = new ServiceOrder();

        order.Should().BeAssignableTo<GreenTransit.Domain.Interfaces.IAuditableEntity>();
    }

    [Fact]
    public void Version_DefaultValue_ShouldBeOne()
    {
        // El campo de control de concurrencia optimista debe iniciar en 1
        var order = new ServiceOrder();

        order.Version.Should().Be(1);
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("InProgress")]
    [InlineData("Completed")]
    [InlineData("Cancelled")]
    public void Status_AcceptsValidValues(string status)
    {
        var order = new ServiceOrder { Status = status };

        order.Status.Should().Be(status);
    }
}
