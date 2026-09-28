using NMSE.Models;

namespace NMSE.Core;

/// <summary>
/// Account-level catalogue completion (accountdata.hg): fossil SeenProducts and
/// raw material SeenSubstances in <c>UserSettingsData</c>.
/// </summary>
internal static partial class CatalogueCompletionLogic
{
    /// <summary>Builds the set of IDs currently present in a Seen* account array.</summary>
    internal static HashSet<string> GetSeenIdSet(JsonObject userSettings, string arrayName)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var array = userSettings.GetArray(arrayName);
        if (array == null) return set;

        for (int i = 0; i < array.Length; i++)
        {
            if (array.Get(i) as string is string value && value.Length > 0)
                set.Add(NormalizeId(value));
        }
        return set;
    }

    /// <summary>Computes completion counts for a Seen* account array.</summary>
    internal static (int Have, int Total) GetSeenCompletion(
        JsonObject userSettings, string arrayName, IReadOnlyList<string> packIds)
    {
        var current = GetSeenIdSet(userSettings, arrayName);
        int have = 0, total = 0;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string raw in packIds)
        {
            string id = NormalizeId(raw);
            if (id.Length == 0 || !seen.Add(id)) continue;
            total++;
            if (current.Contains(id)) have++;
        }
        return (have, total);
    }

    /// <summary>Appends missing Seen* entries with the game's caret-prefixed form.</summary>
    /// <returns>The number of IDs added.</returns>
    internal static int AddMissingSeen(JsonObject userSettings, string arrayName, IReadOnlyList<string> packIds)
    {
        var array = userSettings.GetArray(arrayName);
        if (array == null)
        {
            array = new JsonArray();
            userSettings.Set(arrayName, array);
        }

        var have = GetSeenIdSet(userSettings, arrayName);
        int added = 0;
        foreach (string raw in packIds)
        {
            string id = NormalizeId(raw);
            if (id.Length == 0 || !have.Add(id)) continue;
            array.Add("^" + id);
            added++;
        }
        return added;
    }

    /// <summary>Removes the given IDs from a Seen* account array.</summary>
    /// <returns>The number of IDs removed.</returns>
    internal static int RemoveSeen(JsonObject userSettings, string arrayName, IReadOnlyList<string> packIds)
    {
        var array = userSettings.GetArray(arrayName);
        if (array == null) return 0;

        var targets = new HashSet<string>(
            packIds.Select(NormalizeId).Where(id => id.Length > 0),
            StringComparer.OrdinalIgnoreCase);
        if (targets.Count == 0) return 0;

        int removed = 0;
        for (int i = array.Length - 1; i >= 0; i--)
        {
            string id = array.Get(i) as string is string value ? NormalizeId(value) : "";
            if (id.Length == 0 || !targets.Contains(id)) continue;
            array.RemoveAt(i);
            removed++;
        }
        return removed;
    }
}
