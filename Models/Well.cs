namespace AemEnersolSync.Models;

/// <summary>
/// A well belonging to a <see cref="Platform"/>. The API supplies both
/// <see cref="Id"/> and <see cref="PlatformId"/>, so the FK needs no inference.
/// </summary>
public class Well
{
    public int Id { get; set; }

    public int PlatformId { get; set; }

    public string UniqueName { get; set; } = string.Empty;

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    /// <summary>Nullable: the Dummy endpoint omits this key entirely.</summary>
    public DateTime? CreatedAt { get; set; }

    /// <summary>Nullable: the Dummy endpoint omits this key entirely.</summary>
    public DateTime? UpdatedAt { get; set; }

    public Platform? Platform { get; set; }
}
