using System.Globalization;
using NMSE.Config;
using NMSE.Core;

namespace NMSE.Data;

/// <summary>
/// Remembers the inferred POI layout per system (UA) in the Discoveries data files,
/// so the slot type labels survive later edits to the packed discovery values (which
/// otherwise remove the information needed to infer the layout).
/// </summary>
internal static class SpacePoiLayoutCache
{
    private const string FileName = "space-poi-layouts.json";
    private const string LegacyPrefix = "spacepoi.layout.";
    private const string LegacyVersionedPrefix = "spacepoi.layout.v2.";
    private static bool _migrationChecked;

    /// <summary>Returns the remembered layout tokens for a system, or null if none.</summary>
    public static string[]? Get(ulong ua)
    {
        var file = GetFile();
        return file.Layouts.TryGetValue(BuildKey(ua), out var tokens)
               && tokens.Length == SpaceStationLogic.PackedDataSlotCount
            ? tokens
            : null;
    }

    /// <summary>
    /// Merges freshly inferred layout tokens into the remembered layout for a system.
    /// Known values are never downgraded; the file is written on <see cref="SaveIfDirty"/>.
    /// </summary>
    public static void Remember(ulong ua, IReadOnlyList<string> tokens)
    {
        var file = GetFile();
        string key = BuildKey(ua);
        file.Layouts.TryGetValue(key, out var remembered);
        var merged = SpacePoiSlotTokens.Merge(tokens, remembered);

        if (remembered == null || !remembered.SequenceEqual(merged, StringComparer.Ordinal))
        {
            file.Layouts[key] = merged;
            DiscoveriesStore.MarkDirty(file);
        }
    }

    /// <summary>Writes the layout file when it changed.</summary>
    public static void SaveIfDirty() => DiscoveriesStore.SaveDirty();

    /// <summary>
    /// Imports layouts remembered in NMSE.conf before the Discoveries store existed.
    /// Only the versioned entries are imported; the earlier unversioned ones used a
    /// less reliable rule and are dropped. Returns the number of layouts imported.
    /// </summary>
    internal static int ImportLegacyLayouts(SpacePoiLayoutsFile file, IEnumerable<KeyValuePair<string, string>> properties)
    {
        int imported = 0;
        foreach (var (key, value) in properties)
        {
            if (!key.StartsWith(LegacyVersionedPrefix, StringComparison.Ordinal))
                continue;

            string systemKey = key[LegacyVersionedPrefix.Length..];
            var tokens = SpacePoiSlotTokens.Parse(value);
            if (systemKey.Length == 0 || tokens.Length != SpaceStationLogic.PackedDataSlotCount)
                continue;

            file.Layouts[systemKey] = tokens;
            imported++;
        }
        return imported;
    }

    private static SpacePoiLayoutsFile GetFile()
    {
        var file = DiscoveriesStore.Get(FileName, AppJsonContext.Default.SpacePoiLayoutsFile);
        if (!_migrationChecked)
        {
            _migrationChecked = true;
            MigrateLegacyEntries(file);
        }
        return file;
    }

    /// <summary>
    /// One-time migration of the layout cache out of the application config file.
    /// </summary>
    private static void MigrateLegacyEntries(SpacePoiLayoutsFile file)
    {
        var config = AppConfig.Instance;
        var legacy = config.GetProperties(LegacyPrefix).ToList();
        if (legacy.Count == 0) return;

        if (ImportLegacyLayouts(file, legacy) > 0)
            DiscoveriesStore.MarkDirty(file);

        config.RemoveProperties(LegacyPrefix);
        config.Save();
    }

    private static string BuildKey(ulong ua) =>
        ua.ToString(CultureInfo.InvariantCulture);
}
