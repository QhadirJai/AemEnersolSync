namespace AemEnersolSync.Services;

/// <summary>
/// Bound from the "AemEnersolApi" section. The credentials are the ones supplied with the
/// assessment; the paths are configurable so a moved endpoint needs no code change.
/// </summary>
public class AemEnersolApiOptions
{
    public const string SectionName = "AemEnersolApi";

    public string BaseUrl { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string LoginPath { get; set; } = "api/Account/Login";

    public string ActualPath { get; set; } = "api/PlatformWell/GetPlatformWellActual";

    public string DummyPath { get; set; } = "api/PlatformWell/GetPlatformWellDummy";

    public int TimeoutSeconds { get; set; } = 60;
}
