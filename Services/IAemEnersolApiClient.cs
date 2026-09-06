using AemEnersolSync.Models.Dtos;

namespace AemEnersolSync.Services;

public interface IAemEnersolApiClient
{
    /// <summary>Logs in and returns the platforms (with their nested wells) for one dataset.</summary>
    Task<IReadOnlyList<PlatformDto>> GetPlatformsAsync(
        PlatformWellDataset dataset,
        CancellationToken cancellationToken = default);
}
