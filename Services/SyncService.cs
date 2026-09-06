using AemEnersolSync.Data;
using AemEnersolSync.Models;
using AemEnersolSync.Models.Dtos;
using Microsoft.EntityFrameworkCore;

namespace AemEnersolSync.Services;

/// <summary>
/// Fetches a dataset and upserts it on the API-supplied Id: existing rows are updated,
/// new ones inserted. Rows the API no longer returns are left in place, so a partial
/// response can never wipe local data.
/// </summary>
public class SyncService : ISyncService
{
    private readonly IAemEnersolApiClient _apiClient;
    private readonly AppDbContext _dbContext;
    private readonly ILogger<SyncService> _logger;

    public SyncService(IAemEnersolApiClient apiClient, AppDbContext dbContext, ILogger<SyncService> logger)
    {
        _apiClient = apiClient;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<SyncResult> SyncAsync(PlatformWellDataset dataset, CancellationToken cancellationToken = default)
    {
        var result = new SyncResult { Dataset = dataset.ToString(), StartedAt = DateTime.UtcNow };

        var platformDtos = await _apiClient.GetPlatformsAsync(dataset, cancellationToken);
        var wellDtos = FlattenWells(platformDtos);

        result.Platforms.Fetched = platformDtos.Count;
        result.Wells.Fetched = wellDtos.Count;

        await UpsertPlatformsAsync(platformDtos, result.Platforms, cancellationToken);
        await UpsertWellsAsync(wellDtos, platformDtos, result.Wells, cancellationToken);

        // One SaveChanges keeps the run in a single transaction, and EF orders the platform
        // inserts ahead of the wells that reference them.
        await _dbContext.SaveChangesAsync(cancellationToken);

        result.CompletedAt = DateTime.UtcNow;
        _logger.LogInformation(
            "{Dataset} sync complete: platforms {PlatformsInserted} inserted / {PlatformsUpdated} updated, " +
            "wells {WellsInserted} inserted / {WellsUpdated} updated / {WellsSkipped} skipped",
            dataset, result.Platforms.Inserted, result.Platforms.Updated,
            result.Wells.Inserted, result.Wells.Updated, result.Wells.Skipped);

        return result;
    }

    /// <summary>
    /// Lifts the nested wells into one list, deduped on Id. A well that omits platformId
    /// inherits it from the platform it arrived under.
    /// </summary>
    private static List<WellDto> FlattenWells(IReadOnlyList<PlatformDto> platforms)
    {
        var wells = new Dictionary<int, WellDto>();

        foreach (var platform in platforms)
        {
            foreach (var well in platform.Wells ?? Enumerable.Empty<WellDto>())
            {
                well.PlatformId ??= platform.Id;
                wells[well.Id] = well;
            }
        }

        return wells.Values.ToList();
    }

    private async Task UpsertPlatformsAsync(
        IReadOnlyList<PlatformDto> dtos,
        SyncEntityResult tally,
        CancellationToken cancellationToken)
    {
        var ids = dtos.Select(d => d.Id).Distinct().ToList();
        var existing = await _dbContext.Platforms
            .Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        foreach (var dto in Deduplicate(dtos, d => d.Id))
        {
            if (existing.TryGetValue(dto.Id, out var platform))
            {
                tally.Updated++;
            }
            else
            {
                platform = new Platform { Id = dto.Id };
                _dbContext.Platforms.Add(platform);
                existing[dto.Id] = platform;
                tally.Inserted++;
            }

            // Only the fields the payload actually carried are written: the Dummy dataset
            // omits createdAt/updatedAt, and a missing key must not blank a stored value.
            if (dto.UniqueName is not null) platform.UniqueName = dto.UniqueName;
            if (dto.Latitude is not null) platform.Latitude = dto.Latitude.Value;
            if (dto.Longitude is not null) platform.Longitude = dto.Longitude.Value;
            if (dto.CreatedAt is not null) platform.CreatedAt = dto.CreatedAt;
            if (dto.UpdatedAt is not null) platform.UpdatedAt = dto.UpdatedAt;
        }
    }

    private async Task UpsertWellsAsync(
        IReadOnlyList<WellDto> dtos,
        IReadOnlyList<PlatformDto> platformDtos,
        SyncEntityResult tally,
        CancellationToken cancellationToken)
    {
        var ids = dtos.Select(d => d.Id).Distinct().ToList();
        var existing = await _dbContext.Wells
            .Where(w => ids.Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, cancellationToken);

        // A well may point at a platform inserted in this same run or at one already stored
        // from an earlier run; both are valid parents.
        var knownPlatformIds = platformDtos.Select(p => p.Id).ToHashSet();
        var otherIds = dtos.Select(d => d.PlatformId ?? 0)
                           .Where(id => id != 0 && !knownPlatformIds.Contains(id))
                           .Distinct()
                           .ToList();
        if (otherIds.Count > 0)
        {
            var persisted = await _dbContext.Platforms
                .Where(p => otherIds.Contains(p.Id))
                .Select(p => p.Id)
                .ToListAsync(cancellationToken);
            knownPlatformIds.UnionWith(persisted);
        }

        foreach (var dto in Deduplicate(dtos, d => d.Id))
        {
            var platformId = dto.PlatformId ?? 0;
            if (!knownPlatformIds.Contains(platformId))
            {
                _logger.LogWarning(
                    "Skipping well {WellId}: platform {PlatformId} is unknown", dto.Id, platformId);
                tally.Skipped++;
                continue;
            }

            if (existing.TryGetValue(dto.Id, out var well))
            {
                tally.Updated++;
            }
            else
            {
                well = new Well { Id = dto.Id };
                _dbContext.Wells.Add(well);
                existing[dto.Id] = well;
                tally.Inserted++;
            }

            well.PlatformId = platformId;
            if (dto.UniqueName is not null) well.UniqueName = dto.UniqueName;
            if (dto.Latitude is not null) well.Latitude = dto.Latitude.Value;
            if (dto.Longitude is not null) well.Longitude = dto.Longitude.Value;
            if (dto.CreatedAt is not null) well.CreatedAt = dto.CreatedAt;
            if (dto.UpdatedAt is not null) well.UpdatedAt = dto.UpdatedAt;
        }
    }

    /// <summary>
    /// Keeps the last record per Id: a duplicate in the payload would otherwise make EF
    /// track two instances of the same key and throw on save.
    /// </summary>
    private static IEnumerable<T> Deduplicate<T>(IEnumerable<T> source, Func<T, int> keySelector) =>
        source.GroupBy(keySelector).Select(g => g.Last());
}
