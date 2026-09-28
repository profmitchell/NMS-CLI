using System.Text.Json.Serialization;

namespace NMSE.Core;

/// <summary>
/// Base class for long-term editor data files kept in the Discoveries folder.
/// Each file carries a format <see cref="Version"/> for future migrations and a
/// transient dirty flag used by the store to batch writes.
/// </summary>
internal abstract class DiscoveriesFile
{
    /// <summary>Format version of the file. Increment when the layout changes.</summary>
    public int Version { get; set; } = 1;

    /// <summary>True when the in-memory data has unsaved changes.</summary>
    [JsonIgnore]
    public bool Dirty { get; set; }
}
