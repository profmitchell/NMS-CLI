using System.Text.Encodings.Web;
using System.Text.Json;
using System.Xml.Linq;

namespace NMSE.Extractor.Data;

/// <summary>
/// Builds the derived catalogue completion data from the game's catalogue tables
/// and parsed data tables, writing it to <c>Catalogue Pack.json</c> in the JSON
/// output directory. Values that cannot be derived from game files (save-observed
/// wonder data, discovery stat targets, story completers and the account Seen
/// lists) are compiled into the editor instead.
/// </summary>
public static class CataloguePackBuilder
{
    private static readonly int[] WordRaceOrdinals = [0, 1, 2, 4, 8];

    private static readonly Dictionary<string, string> CatalogueTableKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CatalogueMaterials"] = "cataloguematerials.MXML",
        ["CatalogueBuilding"] = "cataloguebuilding.MXML",
        ["CatalogueCrafting"] = "cataloguecrafting.MXML",
    };

    /// <summary>
    /// Writes the derived catalogue pack file. Lists that cannot be derived are
    /// omitted so the editor falls back to its own known values.
    /// </summary>
    /// <param name="jsonDir">The extractor JSON output directory.</param>
    /// <param name="baseData">Parsed game data keyed by source name.</param>
    /// <param name="mbinDir">Directory containing converted MXML files.</param>
    public static void WriteCataloguePack(
        string jsonDir,
        Dictionary<string, List<Dictionary<string, object?>>> baseData,
        string mbinDir)
    {
        var root = new Dictionary<string, object?>
        {
            ["KnownRefinerRecipes"] = ReadIds(baseData, "Recipes"),
            ["KnownWordGroups"] = BuildWordGroups(baseData),
            ["Fossils"] = BuildFossils(baseData),
            ["KnownPortalRunes"] = 65535,
        };

        foreach (var (key, fileName) in CatalogueTableKeys)
            root[key] = ReadCatalogueItems(Path.Combine(mbinDir, fileName));

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        string path = Path.Combine(jsonDir, "Catalogue Pack.json");
        File.WriteAllText(path, JsonSerializer.Serialize(root, options));
        Console.WriteLine($"[OK] Catalogue Pack: {((List<string>)root["KnownRefinerRecipes"]!).Count} recipes, " +
            $"{((List<Dictionary<string, object?>>)root["KnownWordGroups"]!).Count} word groups, " +
            $"{((List<string>)root["Fossils"]!).Count} fossils, " +
            $"{((List<string>)root["CatalogueMaterials"]!).Count} catalogue materials, " +
            $"{((List<string>)root["CatalogueBuilding"]!).Count} catalogue building, " +
            $"{((List<string>)root["CatalogueCrafting"]!).Count} catalogue technology");
    }

    /// <summary>
    /// Reads the explicit item lists of a catalogue table (the custom list categories).
    /// </summary>
    private static List<string> ReadCatalogueItems(string mxmlPath)
    {
        var result = new List<string>();
        if (!File.Exists(mxmlPath)) return result;

        try
        {
            var doc = XDocument.Load(mxmlPath);
            foreach (var category in doc.Descendants("Property")
                .Where(e => e.Attribute("name")?.Value == "Categories"))
            {
                foreach (var items in category.Elements("Property")
                    .Where(e => e.Attribute("name")?.Value == "Items"))
                {
                    foreach (var item in items.Elements("Property"))
                    {
                        string? id = item.Attribute("value")?.Value;
                        if (!string.IsNullOrEmpty(id) && !result.Contains(id, StringComparer.OrdinalIgnoreCase))
                            result.Add(id);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WARN] Catalogue table {Path.GetFileName(mxmlPath)}: {ex.Message}");
        }
        return result;
    }

    private static List<string> ReadIds(
        Dictionary<string, List<Dictionary<string, object?>>> baseData, string key)
    {
        var ids = new List<string>();
        if (!baseData.TryGetValue(key, out var entries)) return ids;

        foreach (var entry in entries)
        {
            string id = entry.GetValueOrDefault("Id")?.ToString() ?? "";
            if (id.Length > 0 && !ids.Contains(id, StringComparer.OrdinalIgnoreCase))
                ids.Add(id);
        }
        return ids;
    }

    private static List<string> BuildFossils(
        Dictionary<string, List<Dictionary<string, object?>>> baseData)
    {
        var fossils = new List<string>();
        if (!baseData.TryGetValue("ShipComponents", out var components)) return fossils;

        foreach (var component in components)
        {
            string id = component.GetValueOrDefault("Id")?.ToString() ?? "";
            if (id.StartsWith("FOS_", StringComparison.OrdinalIgnoreCase) && !fossils.Contains(id, StringComparer.OrdinalIgnoreCase))
                fossils.Add(id);
        }
        return fossils;
    }

    private static List<Dictionary<string, object?>> BuildWordGroups(
        Dictionary<string, List<Dictionary<string, object?>>> baseData)
    {
        // Collect every group with the race ordinals that map to it.
        var raceFlags = new Dictionary<string, bool[]>(StringComparer.OrdinalIgnoreCase);
        if (baseData.TryGetValue("Words", out var words))
        {
            foreach (var word in words)
            {
                if (word.GetValueOrDefault("Groups") is not Dictionary<string, object?> groups) continue;
                foreach (var (group, ordinalValue) in groups)
                {
                    string groupId = group.TrimStart('^');
                    if (groupId.Length == 0) continue;

                    if (!raceFlags.TryGetValue(groupId, out var flags))
                    {
                        flags = new bool[WordRaceOrdinals.Length];
                        raceFlags[groupId] = flags;
                    }

                    int ordinal = ordinalValue switch
                    {
                        int i => i,
                        long l => (int)l,
                        _ => -1
                    };
                    int index = Array.IndexOf(WordRaceOrdinals, ordinal);
                    if (index >= 0) flags[index] = true;
                }
            }
        }

        var result = new List<Dictionary<string, object?>>();
        foreach (var (group, flags) in raceFlags.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
        {
            result.Add(new Dictionary<string, object?>
            {
                ["Group"] = "^" + group,
                ["Races"] = flags,
            });
        }
        return result;
    }
}
