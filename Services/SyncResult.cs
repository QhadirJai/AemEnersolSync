namespace AemEnersolSync.Services;

/// <summary>Per-entity tally returned by a sync run.</summary>
public class SyncEntityResult
{
    public int Fetched { get; set; }

    public int Inserted { get; set; }

    public int Updated { get; set; }

    /// <summary>Wells whose platformId matched no known platform; inserting them would break the FK.</summary>
    public int Skipped { get; set; }
}

/// <summary>Outcome of one sync run, returned by the endpoint that triggers it.</summary>
public class SyncResult
{
    public string Dataset { get; set; } = string.Empty;

    public DateTime StartedAt { get; set; }

    public DateTime CompletedAt { get; set; }

    public SyncEntityResult Platforms { get; set; } = new();

    public SyncEntityResult Wells { get; set; } = new();
}
