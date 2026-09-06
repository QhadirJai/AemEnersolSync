namespace AemEnersolSync.Services;

/// <summary>
/// The two datasets the API exposes. Dummy returns the same shape with keys removed and
/// an extra key added, which is what proves the sync tolerates a changing payload.
/// </summary>
public enum PlatformWellDataset
{
    Actual,
    Dummy
}
