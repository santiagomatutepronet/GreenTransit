namespace GreenTransit.Application.Common.Interfaces;

public interface IPageDiscoveryService
{
    Task<int> SyncPageDefinitionsAsync(CancellationToken ct = default);
}
