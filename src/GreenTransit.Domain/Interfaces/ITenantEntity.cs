namespace GreenTransit.Domain.Interfaces;

public interface ITenantEntity
{
    Guid? OwnerId { get; set; }
}
