namespace AemEnersolSync.Models;

/// <summary>
/// A platform as returned by the AEM Enersol API. <see cref="Id"/> is supplied by
/// that API (not generated locally) and is the key we upsert on.
/// </summary>
public class Platform
{
    public int Id { get; set; }

    public string UniqueName { get; set; } = string.Empty;

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    /// <summary>Nullable: the Dummy endpoint omits this key entirely.</summary>
    public DateTime? CreatedAt { get; set; }

    /// <summary>Nullable: the Dummy endpoint omits this key entirely.</summary>
    public DateTime? UpdatedAt { get; set; }

    public ICollection<Well> Wells { get; set; } = new List<Well>();
}
