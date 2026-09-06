namespace AemEnersolSync.Models.Dtos;

/// <summary>
/// A well nested inside a <see cref="PlatformDto"/>. The API supplies platformId directly,
/// so it doubles as the foreign key and never has to be inferred from the parent.
/// </summary>
public class WellDto
{
    public int Id { get; set; }

    public int? PlatformId { get; set; }

    public string? UniqueName { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    /// <summary>Missing from the Dummy dataset.</summary>
    public DateTime? CreatedAt { get; set; }

    /// <summary>Missing from the Dummy dataset.</summary>
    public DateTime? UpdatedAt { get; set; }
}
