namespace NMSE.Core;

/// <summary>
/// Long-term memory of the inferred POI layout for each system, keyed by the system's
/// decimal UA. Stored in <c>Discoveries/space-poi-layouts.json</c> so the slot type
/// labels survive later edits to the packed discovery values (which otherwise remove
/// the information needed to infer the layout).
/// </summary>
internal sealed class SpacePoiLayoutsFile : DiscoveriesFile
{
    /// <summary>
    /// Layout tokens per system UA. Each value holds 32 tokens: a POI type name,
    /// "." for an unknown/ambiguous slot, or "-" for an unallocated slot.
    /// </summary>
    public Dictionary<string, string[]> Layouts { get; set; } = new(StringComparer.Ordinal);
}
