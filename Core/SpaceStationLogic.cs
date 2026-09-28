using System.Globalization;
using NMSE.Core.Utilities;
using NMSE.Models;

namespace NMSE.Core;

/// <summary>
/// Logic for the Space Station tab: per-system station stats
/// (<c>Stats[n].Stats[x].Value.IntValue</c>), the SpacePoiDiscoveries array,
/// UA-to-portal-code conversion and matching of PlayerSpaceStationBase /
/// corvette (PlayerShipBase) entries in PersistentPlayerBases.
/// </summary>
internal static class SpaceStationLogic
{
    /// <summary>The Vy'keen standing stat Id.</summary>
    public const string StatWarStanding = "^WAR_STANDING";
    /// <summary>The space POI missions stat Id.</summary>
    public const string StatSpPoiMissions = "^SP_POI_MISSIONS";
    /// <summary>The Vy'keen guild standing stat Id.</summary>
    public const string StatWGuildStand = "^WGUILD_STAND";
    /// <summary>The Gek guild standing stat Id.</summary>
    public const string StatEGuildStand = "^EGUILD_STAND";
    /// <summary>The Trade standing stat Id.</summary>
    public const string StatTraStanding = "^TRA_STANDING";
    /// <summary>The Explorer standing stat Id.</summary>
    public const string StatExpStanding = "^EXP_STANDING";
    /// <summary>The Trade guild standing stat Id.</summary>
    public const string StatTGuildStand = "^TGUILD_STAND";

    /// <summary>All seven stat Ids that identify a per-system space station entry in <c>Stats[n]</c>.</summary>
    public static readonly string[] StationStatIds =
    {
        StatWarStanding, StatSpPoiMissions, StatWGuildStand, StatEGuildStand,
        StatTraStanding, StatExpStanding, StatTGuildStand
    };

    /// <summary>
    /// Display order for the seven station stats: guild standings together, then race
    /// standings, then space POI missions.
    /// </summary>
    public static readonly string[] DisplayStatOrder =
    {
        StatWGuildStand, StatEGuildStand, StatTGuildStand,
        StatWarStanding, StatTraStanding, StatExpStanding,
        StatSpPoiMissions
    };

    /// <summary>Minimum guild standing value required to claim a space station.</summary>
    public const int GuildStandTarget = 15;

    /// <summary>Minimum race standing value required to claim a space station.</summary>
    public const int StandingTarget = 30;

    /// <summary>Minimum space POI missions value required to claim a space station.</summary>
    public const int SpPoiMissionsTarget = 5;

    /// <summary>
    /// The progress sentinel the game writes to completed mission records
    /// (int.MaxValue). Used as the "Completed" value for mission state combos.
    /// </summary>
    public const int MissionCompletedSentinel = int.MaxValue;

    /// <summary>Mission state shown in the Cosmos Missions combos.</summary>
    public enum MissionState
    {
        /// <summary>Mission entry missing or progress -1.</summary>
        NotStarted = 0,
        /// <summary>Mission in progress (progress 0 or higher).</summary>
        Active = 1,
        /// <summary>Mission marked complete with the completed sentinel.</summary>
        Completed = 2
    }

    /// <summary>
    /// Cosmos v7.0 station-claim missions surfaced as state combos in the Space
    /// Station panel. IDs are stored with the leading "^" prefix.
    /// </summary>
    public static readonly string[] CosmosMissionIds =
    {
        "^CLAIM_STAT_TUT", "^SPACEBASE_TUT", "^STATIONOWN_WIKI"
    };

    /// <summary>
    /// Returns the localisation key for a Cosmos mission's display label,
    /// or <c>null</c> for unknown mission IDs.
    /// </summary>
    public static string? GetCosmosMissionLabelKey(string missionId) => missionId switch
    {
        "^CLAIM_STAT_TUT" => "base.station.mission_claim_stat_tut",
        "^SPACEBASE_TUT" => "base.station.mission_spacebase_tut",
        "^STATIONOWN_WIKI" => "base.station.mission_stationown_wiki",
        _ => null
    };

    /// <summary>
    /// Reads the progress of the first MissionProgress entry with the given mission ID.
    /// </summary>
    /// <param name="playerState">The PlayerStateData object.</param>
    /// <param name="missionId">The mission ID, with the leading "^" prefix (e.g. "^CLAIM_STAT_TUT").</param>
    /// <returns>The progress value, or <c>null</c> if no entry exists.</returns>
    public static int? GetMissionProgress(JsonObject playerState, string missionId)
    {
        var missions = playerState.GetArray("MissionProgress");
        if (missions == null) return null;

        for (int i = 0; i < missions.Length; i++)
        {
            var entry = missions.GetObject(i);
            if (entry == null) continue;

            string? id = null;
            try { id = entry.GetString("Mission"); } catch { }
            if (!string.Equals(id, missionId, StringComparison.Ordinal)) continue;

            try { return entry.GetInt("Progress"); }
            catch { return null; }
        }

        return null;
    }

    /// <summary>
    /// Writes the progress of the first MissionProgress entry with the given mission ID,
    /// creating a well-formed entry (matching the game's own entry structure) if none exists.
    /// </summary>
    public static void SetMissionProgress(JsonObject playerState, string missionId, int progress)
    {
        var missions = playerState.GetArray("MissionProgress");
        if (missions == null)
        {
            missions = new JsonArray();
            playerState.Set("MissionProgress", missions);
        }

        for (int i = 0; i < missions.Length; i++)
        {
            var entry = missions.GetObject(i);
            if (entry == null) continue;

            string? id = null;
            try { id = entry.GetString("Mission"); } catch { }
            if (!string.Equals(id, missionId, StringComparison.Ordinal)) continue;

            entry.Set("Progress", progress);
            return;
        }

        missions.Add(BuildMissionEntry(missionId, progress));
    }

    /// <summary>Builds a new MissionProgress entry in the game's own structure.</summary>
    private static JsonObject BuildMissionEntry(string missionId, int progress)
    {
        var entry = new JsonObject();
        entry.Set("Mission", missionId);
        entry.Set("Progress", progress);
        entry.Set("Seed", 0);
        entry.Set("Data", 0);
        entry.Set("Stat", 0);

        var participants = new JsonArray();
        string[] types = { "None", "MissionGiver", "MissionGiverReference", "Primary",
                           "Secondary1", "Secondary2", "Secondary3", "Secondary4",
                           "Secondary5", "Secondary6", "Secondary7", "Secondary8",
                           "Secondary9" };
        foreach (string type in types)
        {
            var participant = new JsonObject();
            participant.Set("UA", 0);

            var buildingSeed = new JsonArray();
            buildingSeed.Add(true);
            buildingSeed.Add("0x0");
            participant.Set("BuildingSeed", buildingSeed);

            var location = new JsonArray();
            location.Add(0.0);
            location.Add(0.0);
            location.Add(0.0);
            participant.Set("BuildingLocation", location);

            var participantType = new JsonObject();
            participantType.Set("ParticipantType", type);
            participant.Set("ParticipantType", participantType);

            participants.Add(participant);
        }
        entry.Set("Participants", participants);

        return entry;
    }

    /// <summary>Maps a mission's stored progress to its UI state.</summary>
    public static MissionState GetMissionState(JsonObject playerState, string missionId)
    {
        int? progress = GetMissionProgress(playerState, missionId);
        if (progress == null || progress == -1)
            return MissionState.NotStarted;
        if (progress == MissionCompletedSentinel)
            return MissionState.Completed;
        return MissionState.Active;
    }

    /// <summary>
    /// Applies a UI mission state to the save. "Active" preserves an existing in-progress
    /// value rather than resetting it (so a mission at step 10 stays at step 10).
    /// </summary>
    public static void ApplyMissionState(JsonObject playerState, string missionId, MissionState state)
    {
        switch (state)
        {
            case MissionState.NotStarted:
                SetMissionProgress(playerState, missionId, -1);
                break;
            case MissionState.Active:
                int? current = GetMissionProgress(playerState, missionId);
                if (current == null || current == -1)
                    SetMissionProgress(playerState, missionId, 0);
                break;
            case MissionState.Completed:
                SetMissionProgress(playerState, missionId, MissionCompletedSentinel);
                break;
        }
    }

    /// <summary>
    /// Returns the claimable minimum for a station stat Id, or 0 if the Id is not one
    /// of the seven station stats.
    /// </summary>
    public static int GetClaimableTarget(string statId)
    {
        switch (statId)
        {
            case StatWGuildStand:
            case StatEGuildStand:
            case StatTGuildStand:
                return GuildStandTarget;
            case StatWarStanding:
            case StatTraStanding:
            case StatExpStanding:
                return StandingTarget;
            case StatSpPoiMissions:
                return SpPoiMissionsTarget;
            default:
                return 0;
        }
    }

    /// <summary>
    /// A single space station system entry: its index in the <c>Stats</c> array,
    /// the raw address value and the JSON object holding the seven inner stats.
    /// </summary>
    public sealed class StationSystemEntry
    {
        /// <summary>Index of the entry within <c>PlayerStateData.Stats</c>.</summary>
        public int StatsIndex { get; init; }

        /// <summary>The raw Address value (a UA address, usually stored as a number).</summary>
        public object? Address { get; init; }

        /// <summary>The Stats entry object containing the inner Stats array.</summary>
        public JsonObject Entry { get; init; } = null!;

        /// <summary>Display text shown in the system list (station base name, system name or portal code).</summary>
        public string DisplayName { get; set; } = "";

        /// <inheritdoc />
        public override string ToString() => DisplayName;
    }

    /// <summary>
    /// Finds all entries in <c>PlayerStateData.Stats</c> that represent space station
    /// systems. A station entry must have a non-zero Address (each represents an individual
    /// system) and its inner Stats array must contain all seven station stat Ids.
    /// The player-global entry (Stats[0], Address 0) is excluded.
    /// </summary>
    /// <param name="playerState">The PlayerStateData object.</param>
    /// <returns>The matching entries in array order.</returns>
    public static List<StationSystemEntry> FindStationSystems(JsonObject playerState)
    {
        var result = new List<StationSystemEntry>();
        var stats = playerState.GetArray("Stats");
        if (stats == null) return result;

        for (int i = 0; i < stats.Length; i++)
        {
            var entry = stats.GetObject(i);
            if (entry == null) continue;

            string address = CoordinateHelper.NormalizeGalacticAddress(entry.Get("Address"));
            if (string.IsNullOrEmpty(address) || address.Equals("0x0", StringComparison.Ordinal))
                continue;

            var inner = entry.GetArray("Stats");
            if (inner == null || inner.Length < StationStatIds.Length) continue;

            int matched = 0;
            for (int j = 0; j < inner.Length && matched < StationStatIds.Length; j++)
            {
                var stat = inner.GetObject(j);
                if (stat == null) continue;
                string? id = null;
                try { id = stat.GetString("Id"); } catch { }
                if (id != null && Array.IndexOf(StationStatIds, id) >= 0)
                    matched++;
            }

            if (matched == StationStatIds.Length)
                result.Add(new StationSystemEntry { StatsIndex = i, Address = entry.Get("Address"), Entry = entry });
        }

        return result;
    }

    /// <summary>
    /// Locates the inner stat entry with the given Id within a station system entry.
    /// </summary>
    /// <param name="systemEntry">A station system entry returned by <see cref="FindStationSystems"/>.</param>
    /// <param name="statId">One of the seven station stat Ids.</param>
    /// <returns>The inner stat object, or <c>null</c> if not found.</returns>
    public static JsonObject? FindStat(JsonObject systemEntry, string statId)
    {
        var inner = systemEntry.GetArray("Stats");
        if (inner == null) return null;

        for (int i = 0; i < inner.Length; i++)
        {
            var stat = inner.GetObject(i);
            if (stat == null) continue;
            string? id = null;
            try { id = stat.GetString("Id"); } catch { }
            if (string.Equals(id, statId, StringComparison.Ordinal))
                return stat;
        }

        return null;
    }

    /// <summary>
    /// Reads the integer value of a station stat. The value lives at
    /// <c>Stat.Value.IntValue</c>; a missing or empty Value object reads as 0.
    /// </summary>
    public static int GetStatValue(JsonObject systemEntry, string statId)
    {
        var stat = FindStat(systemEntry, statId);
        if (stat == null) return 0;

        var value = stat.GetObject("Value");
        if (value == null) return 0;

        try { return value.GetInt("IntValue"); }
        catch { return 0; }
    }

    /// <summary>
    /// Writes the integer value of a station stat, creating the Value object and
    /// <c>IntValue</c> field if they are missing.
    /// </summary>
    public static void SetStatValue(JsonObject systemEntry, string statId, int intValue)
    {
        var stat = FindStat(systemEntry, statId);
        if (stat == null) return;

        var value = stat.GetObject("Value");
        if (value == null)
        {
            value = new JsonObject();
            stat.Set("Value", value);
        }

        value.Set("IntValue", intValue);
    }

    /// <summary>
    /// Raises the seven station stats of the given system to the minimum values required
    /// to claim its space station. Values already at or above the targets are left untouched.
    /// </summary>
    /// <param name="systemEntry">A station system entry returned by <see cref="FindStationSystems"/>.</param>
    /// <returns><c>true</c> if at least one value was changed.</returns>
    public static bool ApplyClaimableMinimums(JsonObject systemEntry)
    {
        bool changed = false;
        foreach (string statId in StationStatIds)
        {
            int target = GetClaimableTarget(statId);
            if (target <= 0) continue;

            if (GetStatValue(systemEntry, statId) < target)
            {
                SetStatValue(systemEntry, statId, target);
                changed = true;
            }
        }
        return changed;
    }

    /// <summary>
    /// Converts a UA address value to its 12-hex-digit portal code for glyph display.
    /// UA addresses are 14 hex digits (<c>[planet 1][system 3][reality 2][y 2][z 3][x 3]</c>)
    /// with leading zero nibbles stripped when stored; the reality index is dropped to
    /// obtain the portal code.
    /// </summary>
    /// <param name="address">The raw address value (number, hex string, etc.).</param>
    /// <returns>The 12-character portal code, or empty string if the value cannot be converted.</returns>
    public static string AddressToPortalCode(object? address)
    {
        string hex = CoordinateHelper.NormalizeGalacticAddress(address);
        if (!hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return "";

        string raw = hex[2..];
        if (raw.Length > 14) return "";
        raw = raw.PadLeft(14, '0');

        string portalCode = string.Concat(raw.AsSpan(0, 4), raw.AsSpan(6, 8));
        if (portalCode.Length != 12)
            return "";

        foreach (char c in portalCode)
        {
            if (!char.IsAsciiHexDigit(c))
                return "";
        }
        return portalCode;
    }

    /// <summary>
    /// Finds the SpacePoiDiscoveries entry whose UA matches the given address.
    /// </summary>
    /// <param name="spacePoiDiscoveries">The SpacePoiDiscoveries array.</param>
    /// <param name="address">The raw address value to match.</param>
    /// <returns>The entry index and object, or <c>null</c> if no match.</returns>
    public static (int Index, JsonObject Data)? FindSpacePoiEntry(JsonArray? spacePoiDiscoveries, object? address)
    {
        if (spacePoiDiscoveries == null) return null;

        string target = CoordinateHelper.NormalizeGalacticAddress(address);
        if (string.IsNullOrEmpty(target)) return null;

        for (int i = 0; i < spacePoiDiscoveries.Length; i++)
        {
            var entry = spacePoiDiscoveries.GetObject(i);
            if (entry == null) continue;

            object? ua = null;
            try { ua = entry.Get("UA"); } catch { }
            if (ua == null) continue;

            if (string.Equals(CoordinateHelper.NormalizeGalacticAddress(ua), target, StringComparison.Ordinal))
                return (i, entry);
        }

        return null;
    }

    /// <summary>
    /// Finds the PlayerSpaceStationBase entry in PersistentPlayerBases whose GalacticAddress
    /// matches the given address.
    /// </summary>
    /// <param name="bases">The PersistentPlayerBases array.</param>
    /// <param name="address">The raw address value to match.</param>
    /// <returns>The base array index and object, or <c>null</c> if no match.</returns>
    public static (int DataIndex, JsonObject Data)? FindStationBase(JsonArray? bases, object? address)
    {
        if (bases == null) return null;

        string target = CoordinateHelper.NormalizeGalacticAddress(address);
        if (string.IsNullOrEmpty(target)) return null;

        for (int i = 0; i < bases.Length; i++)
        {
            var baseObj = bases.GetObject(i);
            if (baseObj == null) continue;

            if (!string.Equals(GetBaseTypeName(baseObj), "PlayerSpaceStationBase", StringComparison.Ordinal))
                continue;

            object? ga = null;
            try { ga = baseObj.Get("GalacticAddress"); } catch { }
            if (ga == null) continue;

            if (string.Equals(CoordinateHelper.NormalizeGalacticAddress(ga), target, StringComparison.Ordinal))
                return (i, baseObj);
        }

        return null;
    }

    /// <summary>
    /// Returns the display name of a PlayerSpaceStationBase entry whose GalacticAddress
    /// matches the given address, or <c>null</c> if there is no such base.
    /// </summary>
    public static string? ResolveStationBaseName(JsonObject playerState, object? address)
    {
        var match = FindStationBase(playerState.GetArray("PersistentPlayerBases"), address);
        if (match is not (_, JsonObject data)) return null;

        string? name = null;
        try { name = data.GetString("Name"); } catch { }
        return string.IsNullOrEmpty(name) ? null : name;
    }

    /// <summary>
    /// Resolves a system's display name from the save's discovery records. System
    /// records carry the player-visible name in their discovery metadata
    /// (<c>DM.CN</c>), for example "Hunyevs XVII // MAIN FAM". Matching is at system
    /// level: the planet nibble of the discovery UA is ignored, and only system
    /// records are used so a planet name is never shown for its system.
    /// </summary>
    /// <param name="saveData">The root save object.</param>
    /// <param name="address">The raw address value to match.</param>
    /// <returns>The system name, or <c>null</c> if none can be resolved.</returns>
    public static string? ResolveSystemName(JsonObject saveData, object? address)
    {
        string target = SystemAddressKey(address);
        if (string.IsNullOrEmpty(target)) return null;

        var discoveryData = saveData.GetObject("DiscoveryManagerData.DiscoveryData-v1");
        var records = discoveryData?.GetObject("Store")?.GetArray("Record");
        if (records == null) return null;

        for (int i = 0; i < records.Length; i++)
        {
            var record = records.GetObject(i);
            var discovery = record?.GetObject("DD");
            if (discovery == null) continue;

            object? ua = null;
            try { ua = discovery.Get("UA"); } catch { }
            if (ua == null) continue;

            if (!string.Equals(SystemAddressKey(ua), target, StringComparison.Ordinal))
                continue;

            // Only system records carry the system name; skip planet records so a
            // planet name is never shown for its system.
            string? discoveryType = null;
            try { discoveryType = discovery.GetString("DT"); } catch { }
            if (!string.Equals(discoveryType, "SolarSystem", StringComparison.Ordinal))
                continue;

            string? name = null;
            try { name = record?.GetObject("DM")?.GetString("CN"); } catch { }
            if (string.IsNullOrEmpty(name))
            {
                try { name = discovery.GetString("Name"); } catch { }
            }
            if (!string.IsNullOrEmpty(name))
                return name;
        }

        return null;
    }

    /// <summary>
    /// Normalises an address to a system-level identity key: the 14-digit padded hex
    /// form with the leading planet nibble removed.
    /// </summary>
    private static string SystemAddressKey(object? address)
    {
        string hex = CoordinateHelper.NormalizeGalacticAddress(address);
        if (!hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return "";

        string raw = hex[2..];
        if (raw.Length > 14) return "";
        return raw.PadLeft(14, '0')[1..];
    }

    /// <summary>Reads the PersistentBaseTypes value from a base entry, or empty string.</summary>
    public static string GetBaseTypeName(JsonObject baseObj)
    {
        try
        {
            return baseObj.GetString("BaseType.PersistentBaseTypes")
                   ?? baseObj.GetString("BaseType")
                   ?? "";
        }
        catch
        {
            return "";
        }
    }

    /// <summary>Base entry classification used by the Bases list and its prefixes.</summary>
    public enum BaseKind
    {
        /// <summary>Not one of the recognised base types.</summary>
        Other,
        /// <summary>Planetary base with a base computer.</summary>
        Home,
        /// <summary>Freighter base.</summary>
        Freighter,
        /// <summary>Space station base.</summary>
        SpaceStation,
        /// <summary>Corvette base (PlayerShipBase).</summary>
        Corvette,
        /// <summary>Space base / asteroid base (PlayerSpaceBase).</summary>
        SpaceBase
    }

    /// <summary>
    /// Classifies a PersistentPlayerBases entry into the recognised base kinds.
    /// </summary>
    public static BaseKind GetBaseKind(JsonObject baseObj)
    {
        string type = GetBaseTypeName(baseObj);
        if (type.Equals("HomePlanetBase", StringComparison.Ordinal)) return BaseKind.Home;
        if (type.Equals("FreighterBase", StringComparison.Ordinal)) return BaseKind.Freighter;
        if (type.Equals("PlayerSpaceStationBase", StringComparison.Ordinal)) return BaseKind.SpaceStation;
        if (type.Equals("PlayerShipBase", StringComparison.Ordinal)) return BaseKind.Corvette;
        if (type.Equals("PlayerSpaceBase", StringComparison.Ordinal)) return BaseKind.SpaceBase;
        return BaseKind.Other;
    }

    /// <summary>
    /// Resolves the display name for a corvette base from its owning ship's name.
    /// The base's UserData field stores the index into ShipOwnership.
    /// </summary>
    /// <param name="playerState">The PlayerStateData object.</param>
    /// <param name="baseObj">The corvette base entry.</param>
    /// <returns>The ship name, or <c>null</c> if it cannot be resolved.</returns>
    public static string? ResolveCorvetteBaseName(JsonObject playerState, JsonObject baseObj)
    {
        try
        {
            int shipIndex;
            var userData = baseObj.GetValue("UserData");
            if (userData is RawDouble rd)
                shipIndex = Convert.ToInt32(rd.Value, CultureInfo.InvariantCulture);
            else if (userData is double d)
                shipIndex = Convert.ToInt32(d, CultureInfo.InvariantCulture);
            else if (userData is int i)
                shipIndex = i;
            else if (userData is long l)
                shipIndex = Convert.ToInt32(l, CultureInfo.InvariantCulture);
            else
                return null;

            var ownership = playerState.GetArray("ShipOwnership");
            if (ownership == null || shipIndex < 0 || shipIndex >= ownership.Length)
                return null;

            var ship = ownership.GetObject(shipIndex);
            if (ship == null) return null;

            string? name = null;
            try { name = ship.GetString("Name"); } catch { }
            return string.IsNullOrEmpty(name) ? null : name;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Number of 2-bit discovery-level slots in the packed Space POI data
    /// (16 per PackedData field).
    /// </summary>
    public const int PackedDataSlotCount = 32;

    /// <summary>
    /// Discovery level of a single packed Space POI slot
    /// (the game's <c>GcSpacePoiDiscoveryLevel</c> enum).
    /// </summary>
    public enum SpacePoiDiscoveryLevel
    {
        /// <summary>Not shown on the star map until revealed by a locator item.</summary>
        Hidden = 0,
        /// <summary>Shown as a generic "Unknown Signal" marker.</summary>
        Undiscovered = 1,
        /// <summary>The POI's name is shown on the star map.</summary>
        Discovered = 2,
        /// <summary>The POI's interaction is finished.</summary>
        Completed = 3
    }

    /// <summary>Reads the discovery level of one packed slot (0-based).</summary>
    /// <param name="value">A packed PackedData value.</param>
    /// <param name="slot">Slot index, 0 to <see cref="PackedDataSlotCount"/> - 1.</param>
    /// <returns>The slot's discovery level.</returns>
    public static SpacePoiDiscoveryLevel GetPackedDataSlot(ulong value, int slot)
    {
        if (slot < 0 || slot >= PackedDataSlotCount)
            throw new ArgumentOutOfRangeException(nameof(slot));

        return (SpacePoiDiscoveryLevel)((value >> (2 * slot)) & 3);
    }

    /// <summary>
    /// Returns the packed value with one slot set to the given discovery level,
    /// preserving every other slot.
    /// </summary>
    /// <param name="value">A packed PackedData value.</param>
    /// <param name="slot">Slot index, 0 to <see cref="PackedDataSlotCount"/> - 1.</param>
    /// <param name="level">The discovery level to store.</param>
    /// <returns>The updated packed value.</returns>
    public static ulong SetPackedDataSlot(ulong value, int slot, SpacePoiDiscoveryLevel level)
    {
        if (slot < 0 || slot >= PackedDataSlotCount)
            throw new ArgumentOutOfRangeException(nameof(slot));

        ulong mask = 3UL << (2 * slot);
        return (value & ~mask) | ((((ulong)level & 3) << (2 * slot)));
    }

    /// <summary>
    /// Reads a packed 64-bit SpacePoiDiscoveries value. The game stores the value
    /// either as a JSON integer or as a 0x-prefixed hex string, the latter used
    /// once the high bit is set (for example <c>0xFFFFFFFFA020A882</c>).
    /// </summary>
    /// <param name="entry">The SpacePoiDiscoveries entry.</param>
    /// <param name="key">The PackedData field name.</param>
    /// <param name="value">The parsed unsigned 64-bit value.</param>
    /// <param name="isHexText">True when the stored value is a hex string.</param>
    /// <returns>True when the field holds a parseable value.</returns>
    public static bool TryGetPackedData(JsonObject entry, string key, out ulong value, out bool isHexText)
    {
        value = 0;
        isHexText = false;

        object? raw = entry.Get(key);
        switch (raw)
        {
            case null:
                return false;
            case string text:
                isHexText = true;
                return TryParsePackedData(text, out value);
            case int i:
                value = unchecked((ulong)(long)i);
                return true;
            case long l:
                value = unchecked((ulong)l);
                return true;
            case ulong u:
                value = u;
                return true;
            case RawDouble rd:
                value = rd.Value < 0 ? unchecked((ulong)(long)rd.Value) : (ulong)rd.Value;
                return true;
            default:
                return false;
        }
    }

    /// <summary>Parses packed data text as decimal or 0x-prefixed hex.</summary>
    /// <param name="text">The user or save supplied text.</param>
    /// <param name="value">The parsed unsigned 64-bit value.</param>
    public static bool TryParsePackedData(string? text, out ulong value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;

        text = text.Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return ulong.TryParse(text.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);

        return ulong.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>Formats a packed value for display in decimal or 0x hex.</summary>
    /// <param name="value">The unsigned 64-bit value.</param>
    /// <param name="asHex">True to format as 0x-prefixed hex.</param>
    public static string FormatPackedData(ulong value, bool asHex) =>
        asHex
            ? "0x" + value.ToString("X", CultureInfo.InvariantCulture)
            : value.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Writes a packed value back to the entry, preserving the stored representation
    /// where possible. Values above <see cref="long.MaxValue"/> must be stored as a
    /// hex string because the save's JSON numbers are signed.
    /// </summary>
    /// <param name="entry">The SpacePoiDiscoveries entry.</param>
    /// <param name="key">The PackedData field name.</param>
    /// <param name="value">The unsigned 64-bit value to store.</param>
    /// <param name="preferHex">True when the value was originally stored as hex text.</param>
    /// <returns>True when the stored value changed.</returns>
    public static bool SetPackedData(JsonObject entry, string key, ulong value, bool preferHex)
    {
        object? current = entry.Get(key);
        bool useHex = preferHex || value > long.MaxValue;

        if (useHex)
        {
            string text = FormatPackedData(value, asHex: true);
            if (current is string existing && string.Equals(existing, text, StringComparison.OrdinalIgnoreCase))
                return false;
            entry.Set(key, text);
            return true;
        }

        if (value <= int.MaxValue)
        {
            if (current is int existingInt && (ulong)existingInt == value) return false;
            entry.Set(key, (int)value);
            return true;
        }

        if (current is long existingLong && (ulong)existingLong == value) return false;
        entry.Set(key, (long)value);
        return true;
    }
}