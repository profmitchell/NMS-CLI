using NMSE.Models;

namespace NMSE.Core;

/// <summary>
/// GLOBAL stat helpers shared by the Discovery Stats tab and the Collected
/// Knowledge tab. A negative value means "full" for most stats.
/// </summary>
internal static partial class CatalogueCompletionLogic
{
    /// <summary>Builds a map of GLOBAL_STATS entries keyed by normalised stat ID.</summary>
    internal static Dictionary<string, JsonObject> GetGlobalStatsMap(JsonObject playerState)
    {
        var result = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);
        var groups = playerState.GetArray("Stats");
        if (groups == null) return result;

        for (int i = 0; i < groups.Length; i++)
        {
            var group = groups.GetObject(i);
            if (group == null || !IsStatGroup(group, "GLOBAL_STATS")) continue;

            var stats = group.GetArray("Stats");
            if (stats == null) continue;

            for (int s = 0; s < stats.Length; s++)
            {
                var stat = stats.GetObject(s);
                if (stat == null) continue;

                string id = NormalizeId(stat.GetString("Id"));
                if (id.Length > 0) result.TryAdd(id, stat);
            }
        }
        return result;
    }

    /// <summary>Reads the IntValue of a GLOBAL stat entry (0 when missing).</summary>
    internal static int GetGlobalInt(JsonObject? stat)
    {
        if (stat?.GetObject("Value") is not JsonObject value) return 0;
        return value.Get("IntValue") is null ? 0 : value.GetInt("IntValue");
    }

    /// <summary>Writes the IntValue of a GLOBAL stat entry.</summary>
    internal static void SetGlobalInt(JsonObject stat, int value)
    {
        var holder = new JsonObject();
        holder.Set("IntValue", value);
        stat.Set("Value", holder);
    }

    /// <summary>
    /// True when a GLOBAL stat meets its target. Missing values are incomplete and
    /// negative values count as full (the game's "unlimited/complete" marker).
    /// </summary>
    internal static bool IsGlobalStatComplete(int? value, int target)
    {
        if (target <= 0) return true;
        if (value is not int current) return false;
        return current < 0 || current >= target;
    }

    /// <summary>Completion counts across a set of GLOBAL stat targets.</summary>
    internal static (int Have, int Total) GetGlobalStatsCompletion(
        JsonObject playerState, IReadOnlyDictionary<string, int> targets)
    {
        var map = GetGlobalStatsMap(playerState);
        int have = 0, total = 0;

        foreach (var (id, target) in targets)
        {
            if (target <= 0) continue;
            total++;

            int? value = map.TryGetValue(id, out var stat) ? GetGlobalInt(stat) : null;
            if (IsGlobalStatComplete(value, target)) have++;
        }
        return (have, total);
    }

    /// <summary>Raises a GLOBAL stat to at least the target (keeps negative full values).</summary>
    /// <returns>True when the stat changed.</returns>
    internal static bool SetGlobalStatAtLeast(JsonObject playerState, string statId, int target)
    {
        var stat = EnsureGlobalStat(playerState, statId);
        int current = GetGlobalInt(stat);
        if (current < 0 || current >= target) return false;

        SetGlobalInt(stat, target);
        return true;
    }

    /// <summary>Sets a GLOBAL stat to the exact target unless it is negative (full).</summary>
    /// <returns>True when the stat changed.</returns>
    internal static bool SetGlobalStat(JsonObject playerState, string statId, int target)
    {
        var stat = EnsureGlobalStat(playerState, statId);
        int current = GetGlobalInt(stat);
        if (current < 0 || current == target) return false;

        SetGlobalInt(stat, target);
        return true;
    }

    /// <summary>Sets a GLOBAL stat to an explicit value, creating it when missing.</summary>
    /// <returns>True when the stat changed.</returns>
    internal static bool SetGlobalStatValue(JsonObject playerState, string statId, int value)
    {
        var stat = EnsureGlobalStat(playerState, statId);
        if (GetGlobalInt(stat) == value) return false;

        SetGlobalInt(stat, value);
        return true;
    }

    /// <summary>Resets a GLOBAL stat to zero.</summary>
    /// <returns>True when the stat changed.</returns>
    internal static bool ClearGlobalStat(JsonObject playerState, string statId)
    {
        var map = GetGlobalStatsMap(playerState);
        if (!map.TryGetValue(NormalizeId(statId), out var stat)) return false;
        if (GetGlobalInt(stat) == 0) return false;

        SetGlobalInt(stat, 0);
        return true;
    }

    /// <summary>Finds or creates a GLOBAL stat entry with an initial value of zero.</summary>
    internal static JsonObject EnsureGlobalStat(JsonObject playerState, string statId)
    {
        statId = NormalizeId(statId);
        var map = GetGlobalStatsMap(playerState);
        if (map.TryGetValue(statId, out var existing)) return existing;

        var groups = playerState.GetArray("Stats");
        if (groups == null)
        {
            groups = new JsonArray();
            playerState.Set("Stats", groups);
        }

        JsonObject? globalGroup = null;
        for (int i = 0; i < groups.Length; i++)
        {
            var group = groups.GetObject(i);
            if (group != null && IsStatGroup(group, "GLOBAL_STATS"))
            {
                globalGroup = group;
                break;
            }
        }

        if (globalGroup == null)
        {
            globalGroup = new JsonObject();
            globalGroup.Set("GroupId", "^GLOBAL_STATS");
            globalGroup.Set("Address", 0);
            globalGroup.Set("Stats", new JsonArray());
            groups.Add(globalGroup);
        }

        var stats = globalGroup.GetArray("Stats");
        if (stats == null)
        {
            stats = new JsonArray();
            globalGroup.Set("Stats", stats);
        }

        var stat = new JsonObject();
        stat.Set("Id", "^" + statId);
        SetGlobalInt(stat, 0);
        stats.Add(stat);
        return stat;
    }
}
