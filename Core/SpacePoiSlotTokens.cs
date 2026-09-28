namespace NMSE.Core;

/// <summary>
/// Helpers for the per-system POI layout token strings remembered between sessions.
/// Each of the 32 tokens is either a POI type name, <see cref="Unknown"/> when the
/// layout inference could not decide, or <see cref="Unallocated"/> when no POI exists
/// in that slot.
/// </summary>
internal static class SpacePoiSlotTokens
{
    /// <summary>Token for slots the inference could not resolve.</summary>
    public const string Unknown = ".";

    /// <summary>Token for slots no consistent layout allocates.</summary>
    public const string Unallocated = "-";

    /// <summary>Splits a remembered layout string into tokens.</summary>
    public static string[] Parse(string? text) =>
        string.IsNullOrEmpty(text) ? Array.Empty<string>() : text.Split('|');

    /// <summary>Joins tokens into the remembered layout string form.</summary>
    public static string Format(IReadOnlyList<string> tokens) => string.Join("|", tokens);

    /// <summary>
    /// Merges freshly inferred tokens with remembered ones. Fresh knowledge wins;
    /// remembered values fill slots the fresh inference left unknown. A known value is
    /// never downgraded to unknown.
    /// </summary>
    public static string[] Merge(IReadOnlyList<string> fresh, IReadOnlyList<string>? remembered)
    {
        var merged = new string[fresh.Count];
        for (int i = 0; i < fresh.Count; i++)
        {
            string value = fresh[i];
            if (value == Unknown && remembered != null && i < remembered.Count && !string.IsNullOrEmpty(remembered[i]))
                value = remembered[i];
            merged[i] = value;
        }
        return merged;
    }
}
