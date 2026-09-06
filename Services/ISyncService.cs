namespace AemEnersolSync.Services;

public interface ISyncService
{
    Task<SyncResult> SyncAsync(PlatformWellDataset dataset, CancellationToken cancellationToken = default);
}
