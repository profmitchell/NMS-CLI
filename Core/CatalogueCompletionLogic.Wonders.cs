using System.Globalization;
using NMSE.Models;

namespace NMSE.Core;

/// <summary>
/// Wonder slot completion: fills empty planet/creature/flora/mineral slots from the
/// baked surrogate pack (injecting the matching DiscoveryManager records, planet
/// stats and discovery owners) and copies treasure/weird base part records from the
/// catalogue pack. Filled player slots are always kept.
/// </summary>
internal static partial class CatalogueCompletionLogic
{
    /// <summary>Wonder keys whose slots are filled from baked discovery surrogates.</summary>
    internal static readonly string[] DiscoveryWonderKeys =
    [
        "WonderPlanetRecords",
        "WonderCreatureRecords",
        "WonderFloraRecords",
        "WonderMineralRecords",
    ];

    /// <summary>All wonder keys in UI order.</summary>
    internal static readonly string[] WonderKeys =
    [
        "WonderPlanetRecords",
        "WonderCreatureRecords",
        "WonderFloraRecords",
        "WonderMineralRecords",
        "WonderTreasureRecords",
        "WonderWeirdBasePartRecords",
    ];

    private static readonly Dictionary<string, (string DiscoveryType, int MinSlots)> DiscoveryWonderConfig =
        new(StringComparer.Ordinal)
        {
            ["WonderPlanetRecords"] = ("Planet", 11),
            ["WonderCreatureRecords"] = ("Animal", 15),
            ["WonderFloraRecords"] = ("Flora", 8),
            ["WonderMineralRecords"] = ("Mineral", 8),
        };

    /// <summary>True when the wonder key uses discovery surrogates (planet/creature/flora/mineral).</summary>
    internal static bool IsDiscoveryWonder(string wonderKey) => DiscoveryWonderConfig.ContainsKey(wonderKey);

    /// <summary>True when a wonder record represents a real discovery (not an empty slot).</summary>
    internal static bool IsWonderSlotFilled(JsonObject? slot)
    {
        if (slot == null) return false;

        var gid = slot.GetArray("GenerationID");
        if (gid != null && gid.Length >= 2 && (IsNonZero(gid.Get(0)) || IsNonZero(gid.Get(1))))
            return true;

        return !string.IsNullOrEmpty(slot.GetString("TreasureType"))
            || !string.IsNullOrEmpty(slot.GetString("Filename"));
    }

    /// <summary>
    /// Computes the wonder completion counts: filled live slots versus the expected
    /// slot total (baked fallback slots for discovery types, filled pack records for
    /// treasure/weird base part records).
    /// </summary>
    internal static (int Have, int Total) GetWonderCompletion(
        JsonObject playerState,
        string wonderKey,
        IReadOnlyList<JsonObject>? packRecords,
        IReadOnlyDictionary<string, IReadOnlyList<JsonObject>> fallbackSlots)
    {
        var current = playerState.GetArray(wonderKey);

        if (IsDiscoveryWonder(wonderKey))
        {
            fallbackSlots.TryGetValue(wonderKey, out var slots);
            int total = slots?.Count(IsWonderSlotFilled) ?? 0;
            if (total == 0)
                total = DiscoveryWonderConfig[wonderKey].MinSlots;

            int have = 0;
            for (int i = 0; i < total; i++)
            {
                if (current != null && i < current.Length && IsWonderSlotFilled(current.GetObject(i)))
                    have++;
            }
            return (have, total);
        }

        int expected = 0, filled = 0;
        if (packRecords != null)
        {
            for (int i = 0; i < packRecords.Count; i++)
            {
                if (!IsWonderSlotFilled(packRecords[i])) continue;
                expected++;
                if (current != null && i < current.Length && IsWonderSlotFilled(current.GetObject(i)))
                    filled++;
            }
        }
        return (filled, expected);
    }

    /// <summary>
    /// Fills every empty slot in the given wonder array. Discovery types inject the
    /// surrogate's DiscoveryManager records, PLANET_STATS rows and discovery owners
    /// first, and only write slots that are backed by a discovery record.
    /// </summary>
    /// <returns>The number of slots filled.</returns>
    internal static int FillWonderSlots(
        JsonObject saveRoot,
        JsonObject playerState,
        string wonderKey,
        IReadOnlyList<JsonObject>? packRecords,
        IReadOnlyDictionary<string, IReadOnlyList<JsonObject>> fallbackSlots)
    {
        var slots = EnsureWonderArray(playerState, wonderKey);
        EnsureWonderLength(slots, wonderKey, packRecords, fallbackSlots);

        int filled = 0;
        for (int i = 0; i < slots.Length; i++)
        {
            if (FillWonderSlotAt(saveRoot, playerState, slots, wonderKey, i, packRecords, fallbackSlots))
                filled++;
        }
        return filled;
    }

    /// <summary>Fills a single empty wonder slot.</summary>
    /// <returns>True when the slot was filled.</returns>
    internal static bool FillWonderSlot(
        JsonObject saveRoot,
        JsonObject playerState,
        string wonderKey,
        int index,
        IReadOnlyList<JsonObject>? packRecords,
        IReadOnlyDictionary<string, IReadOnlyList<JsonObject>> fallbackSlots)
    {
        if (index < 0) return false;

        var slots = EnsureWonderArray(playerState, wonderKey);
        while (slots.Length <= index)
            slots.Add(CreateEmptyWonderSlot());

        EnsureWonderLength(slots, wonderKey, packRecords, fallbackSlots);
        return FillWonderSlotAt(saveRoot, playerState, slots, wonderKey, index, packRecords, fallbackSlots);
    }

    private static void EnsureWonderLength(
        JsonArray slots,
        string wonderKey,
        IReadOnlyList<JsonObject>? packRecords,
        IReadOnlyDictionary<string, IReadOnlyList<JsonObject>> fallbackSlots)
    {
        if (IsDiscoveryWonder(wonderKey))
        {
            fallbackSlots.TryGetValue(wonderKey, out var surrogates);
            var (_, minSlots) = DiscoveryWonderConfig[wonderKey];
            int target = Math.Max(Math.Max(slots.Length, minSlots), surrogates?.Count ?? 0);
            while (slots.Length < target)
                slots.Add(CreateEmptyWonderSlot());
            return;
        }

        if (packRecords == null) return;
        while (slots.Length < packRecords.Count)
            slots.Add(new JsonObject());
    }

    private static bool FillWonderSlotAt(
        JsonObject saveRoot,
        JsonObject playerState,
        JsonArray slots,
        string wonderKey,
        int index,
        IReadOnlyList<JsonObject>? packRecords,
        IReadOnlyDictionary<string, IReadOnlyList<JsonObject>> fallbackSlots)
    {
        if (index < 0 || index >= slots.Length) return false;
        if (IsWonderSlotFilled(slots.GetObject(index))) return false;

        if (IsDiscoveryWonder(wonderKey))
        {
            if (!fallbackSlots.TryGetValue(wonderKey, out var surrogates) || surrogates.Count == 0)
                return false;

            // Dependency injection (DiscoveryManager records, PLANET_STATS, owners) is
            // deferred to save time so a tick/untick round trip leaves no residue.
            var surrogate = surrogates[index % surrogates.Count];
            var record = new JsonObject();
            record.Set("GenerationID", surrogate.GetArray("GenerationID")?.DeepClone() ?? CreateEmptyGenerationId());
            record.Set("WonderStatValue", surrogate.Get("WonderStatValue") ?? 1.0);
            record.Set("SeenInFrontend", true);
            slots.Set(index, record);
            return true;
        }

        if (packRecords == null || index >= packRecords.Count || !IsWonderSlotFilled(packRecords[index]))
            return false;

        var copied = packRecords[index].DeepClone();
        copied.Set("SeenInFrontend", true);
        slots.Set(index, copied);
        return true;
    }

    /// <summary>Clears a single wonder slot back to its empty state.</summary>
    /// <returns>True when the slot changed.</returns>
    internal static bool ClearWonderSlot(JsonObject playerState, string wonderKey, int index)
    {
        var slots = playerState.GetArray(wonderKey);
        if (slots == null || index < 0 || index >= slots.Length) return false;
        if (!IsWonderSlotFilled(slots.GetObject(index))) return false;

        slots.Set(index, CreateEmptyWonderSlot());
        return true;
    }

    private static JsonArray EnsureWonderArray(JsonObject playerState, string wonderKey)
    {
        var slots = playerState.GetArray(wonderKey);
        if (slots == null)
        {
            slots = new JsonArray();
            playerState.Set(wonderKey, slots);
        }
        return slots;
    }

    private static JsonObject CreateEmptyWonderSlot()
    {
        var slot = new JsonObject();
        slot.Set("GenerationID", CreateEmptyGenerationId());
        slot.Set("WonderStatValue", 0.0);
        slot.Set("SeenInFrontend", false);
        return slot;
    }

    private static JsonArray CreateEmptyGenerationId()
    {
        var generationId = new JsonArray();
        generationId.Add(0);
        generationId.Add(0);
        return generationId;
    }

    private static bool IsNonZero(object? value) => value switch
    {
        null => false,
        string s => s.Length > 0 && s != "0" && s != "0x0" && s != "0x00",
        int i => i != 0,
        long l => l != 0,
        double d => d != 0,
        RawDouble rd => rd.Value != 0,
        _ => false
    };

    /// <summary>
    /// Injects the DiscoveryManager records, PLANET_STATS rows and discovery owners
    /// referenced by a surrogate slot. Safe to call repeatedly (deduplicated).
    /// </summary>
    private static void InjectSurrogateContents(
        JsonObject saveRoot,
        JsonObject playerState,
        JsonObject surrogate)
    {
        var discoveryRecords = GetDiscoveryManagerRecords(saveRoot);
        var signatures = GetDiscoverySignatureSet(discoveryRecords);

        var owners = new List<JsonObject>();

        var discoveriesAdded = 0;
        var statsAdded = 0;

        foreach (string key in new[] { "Animal", "Flora", "Mineral", "Planet" })
        {
            if (surrogate.GetObject(key) is not JsonObject record) continue;

            string? signature = DiscoveryRecordSignature(record);
            if (signature != null && signatures.Add(signature))
            {
                discoveryRecords.Add(record.DeepClone());
                discoveriesAdded++;
            }

            if (key == "Planet") continue;
            if (record.GetObject("OWS") is JsonObject ows && NormalizeOwner(ows) is JsonObject owner)
                owners.Add(owner);
        }

        if (surrogate.GetObject("PlanetStats") is JsonObject planetStats)
            statsAdded += MergePlanetStats(playerState, planetStats);

        if (surrogate.GetObject("Planet")?.GetObject("OWS") is JsonObject planetOwner
            && NormalizeOwner(planetOwner) is JsonObject normalizedPlanetOwner)
        {
            owners.Add(normalizedPlanetOwner);
        }

        if (owners.Count > 0)
            MergeDiscoveryOwners(saveRoot, owners);

        if (discoveriesAdded > 0 || statsAdded > 0)
            UpdateReserveStore(saveRoot, discoveryRecords.Length);
    }

    /// <summary>
    /// Injects the DiscoveryManager records, PLANET_STATS rows and discovery owners
    /// required by filled discovery wonder slots. Called before the save is written;
    /// idempotent and only injects slots that match the baked surrogates.
    /// </summary>
    /// <returns>The number of filled slots whose dependencies were ensured.</returns>
    internal static int InjectWonderDependencies(
        JsonObject saveRoot,
        JsonObject playerState,
        IReadOnlyDictionary<string, IReadOnlyList<JsonObject>> fallbackSlots)
    {
        int injected = 0;
        foreach (string key in DiscoveryWonderKeys)
        {
            if (!fallbackSlots.TryGetValue(key, out var surrogates) || surrogates.Count == 0) continue;

            var slots = playerState.GetArray(key);
            if (slots == null) continue;

            for (int i = 0; i < slots.Length; i++)
            {
                var record = slots.GetObject(i);
                if (record == null || !IsWonderSlotFilled(record)) continue;

                var surrogate = surrogates[i % surrogates.Count];
                if (!GenerationIdMatches(record, surrogate)) continue;

                InjectSurrogateContents(saveRoot, playerState, surrogate);
                injected++;
            }
        }
        return injected;
    }

    /// <summary>True when a filled slot holds the same GenerationID as a baked surrogate.</summary>
    private static bool GenerationIdMatches(JsonObject record, JsonObject surrogate)
    {
        var recordId = record.GetArray("GenerationID");
        var surrogateId = surrogate.GetArray("GenerationID");
        if (recordId == null || surrogateId == null || recordId.Length < 2 || surrogateId.Length < 2)
            return false;

        return string.Equals(InvariantValue(recordId.Get(0)), InvariantValue(surrogateId.Get(0)), StringComparison.Ordinal)
            && string.Equals(InvariantValue(recordId.Get(1)), InvariantValue(surrogateId.Get(1)), StringComparison.Ordinal);
    }

    private static JsonArray GetDiscoveryManagerRecords(JsonObject saveRoot)
    {
        var manager = saveRoot.GetObject("DiscoveryManagerData");
        if (manager == null)
        {
            manager = new JsonObject();
            saveRoot.Set("DiscoveryManagerData", manager);
        }

        var v1 = manager.GetObject("DiscoveryData-v1");
        if (v1 == null)
        {
            v1 = new JsonObject();
            manager.Set("DiscoveryData-v1", v1);
        }

        var store = v1.GetObject("Store");
        if (store == null)
        {
            store = new JsonObject();
            v1.Set("Store", store);
        }

        var records = store.GetArray("Record");
        if (records == null)
        {
            records = new JsonArray();
            store.Set("Record", records);
        }
        return records;
    }

    private static HashSet<string> GetDiscoverySignatureSet(JsonArray records)
    {
        var signatures = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < records.Length; i++)
        {
            string? signature = DiscoveryRecordSignature(records.GetObject(i));
            if (signature != null) signatures.Add(signature);
        }
        return signatures;
    }

    private static string? DiscoveryRecordSignature(JsonObject? record)
    {
        var dd = record?.GetObject("DD");
        var vp = dd?.GetArray("VP");
        if (dd == null || vp == null || vp.Length == 0) return null;

        string dt = dd.GetString("DT") ?? "";
        object? ua = dd.Get("UA");
        if (dt.Length == 0 || ua == null) return null;

        return string.Concat(dt, "|", InvariantValue(ua), "|", InvariantValue(vp.Get(0)));
    }

    private static int MergePlanetStats(JsonObject playerState, JsonObject planetStats)
    {
        var stats = playerState.GetArray("Stats");
        if (stats == null)
        {
            stats = new JsonArray();
            playerState.Set("Stats", stats);
        }

        var existing = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < stats.Length; i++)
        {
            var group = stats.GetObject(i);
            if (group == null || !IsStatGroup(group, "PLANET_STATS")) continue;
            string address = InvariantValue(group.Get("Address"));
            if (address.Length > 0) existing.Add(address);
        }

        string addressValue = InvariantValue(planetStats.Get("Address"));
        if (addressValue.Length == 0 || !existing.Add(addressValue)) return 0;

        stats.Add(planetStats.DeepClone());
        return 1;
    }

    private static void MergeDiscoveryOwners(JsonObject saveRoot, IReadOnlyList<JsonObject> owners)
    {
        var common = saveRoot.GetObject("CommonStateData");
        if (common == null)
        {
            common = new JsonObject();
            saveRoot.Set("CommonStateData", common);
        }

        var used = common.GetArray("UsedDiscoveryOwnersV2");
        if (used == null)
        {
            used = new JsonArray();
            common.Set("UsedDiscoveryOwnersV2", used);
        }

        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < used.Length; i++)
        {
            var owner = NormalizeOwner(used.GetObject(i));
            if (owner != null) keys.Add(OwnerKey(owner));
        }

        foreach (var owner in owners)
        {
            if (!keys.Add(OwnerKey(owner))) continue;
            used.Add(owner);
        }
    }

    private static JsonObject? NormalizeOwner(JsonObject? ows)
    {
        if (ows == null) return null;

        string uid = InvariantValue(ows.Get("UID"));
        if (uid.Length == 0) return null;

        var owner = new JsonObject();
        owner.Set("LID", InvariantValue(ows.Get("LID")));
        owner.Set("UID", uid);
        owner.Set("USN", InvariantValue(ows.Get("USN")));
        owner.Set("PTK", (ows.GetString("PTK") ?? "").Trim());
        owner.Set("TS", ows.Get("TS") is null ? 0 : ows.GetInt("TS"));
        return owner;
    }

    private static string OwnerKey(JsonObject owner) =>
        string.Concat(InvariantValue(owner.Get("UID")), "|", InvariantValue(owner.Get("PTK")));

    private static void UpdateReserveStore(JsonObject saveRoot, int recordCount)
    {
        var manager = saveRoot.GetObject("DiscoveryManagerData");
        var v1 = manager?.GetObject("DiscoveryData-v1");
        if (v1 == null) return;

        int current = v1.Get("ReserveStore") is null ? 0 : v1.GetInt("ReserveStore");
        if (recordCount > current)
            v1.Set("ReserveStore", recordCount);
    }

    private static bool IsStatGroup(JsonObject group, string groupId) =>
        string.Equals(NormalizeId(group.GetString("GroupId")), groupId, StringComparison.OrdinalIgnoreCase);

    private static string InvariantValue(object? value) => value switch
    {
        null => "",
        string s => s,
        int i => i.ToString(CultureInfo.InvariantCulture),
        long l => l.ToString(CultureInfo.InvariantCulture),
        double d => d.ToString(CultureInfo.InvariantCulture),
        float f => f.ToString(CultureInfo.InvariantCulture),
        decimal m => m.ToString(CultureInfo.InvariantCulture),
        RawDouble rd => rd.Value.ToString(CultureInfo.InvariantCulture),
        bool b => b ? "true" : "false",
        _ => ""
    };
}
