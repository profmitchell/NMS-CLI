using System.Text.Json;
using NMSE.Core;

namespace NMSE.Data;

/// <summary>
/// Static space POI table loaded from <c>Resources/json/Space POI.json</c> (extractor
/// output). The list is in generation order, which is the order the game assigns the
/// packed <c>SpacePoiDiscoveries</c> slots: normal instances first, then each type's
/// forced hidden extras.
/// </summary>
internal static class SpacePoiTableDatabase
{
    /// <summary>Type rules in generation order. Empty until loaded.</summary>
    public static readonly IReadOnlyList<SpacePoiTypeRule> Types = new List<SpacePoiTypeRule>();

    /// <summary>
    /// Loads the space POI table from disk. Returns false when the file is missing or
    /// invalid; the caller should then fall back to generic slot numbers.
    /// </summary>
    public static bool LoadFromFile(string jsonPath)
    {
        var types = (List<SpacePoiTypeRule>)Types;
        types.Clear();

        if (!File.Exists(jsonPath)) return false;

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllBytes(jsonPath));
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return false;

            foreach (var elem in doc.RootElement.EnumerateArray())
            {
                string type = GetString(elem, "Type");
                if (string.IsNullOrEmpty(type)) continue;

                types.Add(new SpacePoiTypeRule
                {
                    Type = type,
                    MinCount = GetInt(elem, "MinCount"),
                    MaxCount = GetInt(elem, "MaxCount"),
                    ForcedHiddenExtras = GetInt(elem, "ForcedHiddenExtras"),
                    NormalInitialLevel = ParseLevel(GetString(elem, "NormalInitialLevel")),
                    AllowedInAbandonedSystem = GetBool(elem, "AllowedInAbandonedSystem", true),
                    AllowedInEmptySystem = GetBool(elem, "AllowedInEmptySystem", true),
                    NameLocKey = GetString(elem, "NameLocKey"),
                    Name = GetString(elem, "Name"),
                    DisplayName = GetString(elem, "Name"),
                });
            }

            return types.Count > 0;
        }
        catch
        {
            types.Clear();
            return false;
        }
    }

    /// <summary>
    /// Applies the active game language to the type display names, falling back to the
    /// English name from the table. Returns the number of types updated.
    /// </summary>
    public static int ApplyLocalisation(LocalisationService service)
    {
        int updated = 0;
        foreach (var type in Types)
        {
            type.DisplayName = service.Lookup(type.NameLocKey) ?? type.Name;
            updated++;
        }
        return updated;
    }

    /// <summary>Reverts the type display names to the English names from the table.</summary>
    public static void RevertLocalisation()
    {
        foreach (var type in Types)
            type.DisplayName = type.Name;
    }

    /// <summary>Parses a discovery level name from the table.</summary>
    private static SpaceStationLogic.SpacePoiDiscoveryLevel ParseLevel(string value) => value switch
    {
        "Discovered" => SpaceStationLogic.SpacePoiDiscoveryLevel.Discovered,
        "Undiscovered" => SpaceStationLogic.SpacePoiDiscoveryLevel.Undiscovered,
        "Completed" => SpaceStationLogic.SpacePoiDiscoveryLevel.Completed,
        _ => SpaceStationLogic.SpacePoiDiscoveryLevel.Hidden,
    };

    private static string GetString(JsonElement elem, string name) =>
        elem.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString() ?? ""
            : "";

    private static int GetInt(JsonElement elem, string name) =>
        elem.TryGetProperty(name, out var prop) && prop.TryGetInt32(out int value) ? value : 0;

    private static bool GetBool(JsonElement elem, string name, bool fallback) =>
        elem.TryGetProperty(name, out var prop) && prop.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? prop.GetBoolean()
            : fallback;
}
