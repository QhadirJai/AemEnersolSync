using System.Text.Json.Serialization;

namespace AemEnersolSync.Models.Dtos;

/// <summary>
/// A platform as returned by GetPlatformWellActual / GetPlatformWellDummy. Every property
/// is nullable because the Dummy dataset omits keys the Actual one supplies; unmapped keys
/// it adds (such as "lastUpdate") are ignored by System.Text.Json without extra code.
/// </summary>
public class PlatformDto
{
    public int Id { get; set; }

    public string? UniqueName { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    /// <summary>Missing from the Dummy dataset.</summary>
    public DateTime? CreatedAt { get; set; }

    /// <summary>Missing from the Dummy dataset.</summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>The API nests the wells under the singular key "well".</summary>
    [JsonPropertyName("well")]
    public List<WellDto>? Wells { get; set; }
}
