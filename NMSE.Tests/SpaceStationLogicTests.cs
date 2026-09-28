using NMSE.Core;
using NMSE.Data;
using NMSE.IO;
using NMSE.Models;

namespace NMSE.Tests;

/// <summary>
/// Tests for the Cosmos v7.0 space station logic: station system detection,
/// stat read/write, claimable minimums, UA-to-portal-code conversion and
/// SpacePoiDiscoveries / station base matching.
/// </summary>
public class SpaceStationLogicTests
{
    private static bool _mapperLoaded;
    private static readonly object MapperLock = new();

    /// <summary>Loads the name mapper so reference saves deobfuscate correctly.</summary>
    private static void EnsureMapperLoaded()
    {
        lock (MapperLock)
        {
            if (_mapperLoaded) return;
            var mapperPath = FindRefPath("Resources", "map", "mapping.json");
            if (mapperPath != null)
            {
                var mapper = new JsonNameMapper();
                mapper.Load(mapperPath);
                JsonParser.SetDefaultMapper(mapper);
            }
            _mapperLoaded = true;
        }
    }
    private static JsonObject BuildStat(string id, int? intValue)
    {
        var stat = new JsonObject();
        stat.Set("Id", id);
        var value = new JsonObject();
        if (intValue.HasValue)
            value.Set("IntValue", intValue.Value);
        stat.Set("Value", value);
        return stat;
    }

    private static JsonObject BuildStationEntry(long address, params (string Id, int? Value)[] stats)
    {
        var entry = new JsonObject();
        entry.Set("Address", address);
        var inner = new JsonArray();
        foreach (var (id, value) in stats)
            inner.Add(BuildStat(id, value));
        entry.Set("Stats", inner);
        return entry;
    }

    private static JsonObject BuildStationEntryWithDefaults(long address)
    {
        var stats = new (string Id, int? Value)[SpaceStationLogic.StationStatIds.Length];
        for (int i = 0; i < stats.Length; i++)
            stats[i] = (SpaceStationLogic.StationStatIds[i], null);
        return BuildStationEntry(address, stats);
    }

    private static string? FindRefPath(params string[] parts)
    {
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        for (int i = 0; i < 10; i++)
        {
            var candidate = Path.Combine(new[] { dir }.Concat(parts).ToArray());
            if (File.Exists(candidate) || Directory.Exists(candidate)) return candidate;
            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }
        return null;
    }

    // --- FindStationSystems -------------------------------------------------

    [Fact]
    public void FindStationSystems_ReturnsOnlyStationEntries()
    {
        var playerState = new JsonObject();
        var stats = new JsonArray();
        stats.Add(BuildStationEntryWithDefaults(39583697985345L));
        stats.Add(BuildStationEntry(4514599014572299L,
            ("^ALL_CREATURES", 1), ("^DEATHS", 2), ("^DISC_MINERALS", 14)));
        var other = new JsonObject();
        other.Set("Address", 0L);
        var otherInner = new JsonArray();
        for (int i = 0; i < 7; i++)
            otherInner.Add(BuildStat($"^OTHER_{i}", i));
        other.Set("Stats", otherInner);
        stats.Add(other);
        playerState.Set("Stats", stats);

        var found = SpaceStationLogic.FindStationSystems(playerState);

        Assert.Single(found);
        Assert.Equal(0, found[0].StatsIndex);
        Assert.Equal(39583697985345L, found[0].Address);
    }

    [Fact]
    public void FindStationSystems_PlayerGlobalEntry_WithAllStatIds_IsExcluded()
    {
        var playerState = new JsonObject();
        var stats = new JsonArray();

        // Mirrors Stats[0]: Address 0, all seven station Ids plus many others.
        var global = new JsonObject();
        global.Set("Address", 0L);
        var inner = new JsonArray();
        inner.Add(BuildStat("^ALL_CREATURES", 1));
        for (int i = 0; i < SpaceStationLogic.StationStatIds.Length; i++)
            inner.Add(BuildStat(SpaceStationLogic.StationStatIds[i], null));
        inner.Add(BuildStat("^DEATHS", 2));
        global.Set("Stats", inner);
        stats.Add(global);

        playerState.Set("Stats", stats);
        Assert.Empty(SpaceStationLogic.FindStationSystems(playerState));
    }

    [Fact]
    public void FindStationSystems_PartialStatSet_IsExcluded()
    {
        var playerState = new JsonObject();
        var stats = new JsonArray();
        stats.Add(BuildStationEntry(39583697985345L,
            (SpaceStationLogic.StatWarStanding, 33),
            (SpaceStationLogic.StatSpPoiMissions, 5)));
        playerState.Set("Stats", stats);

        Assert.Empty(SpaceStationLogic.FindStationSystems(playerState));
    }

    [Fact]
    public void FindStationSystems_MissingStats_ReturnsEmpty()
    {
        var found = SpaceStationLogic.FindStationSystems(new JsonObject());
        Assert.Empty(found);
    }

    [Fact]
    public void FindStationSystems_ReferenceSave_FindsThreeSystems()
    {
        string? savePath = FindRefPath("_ref", "_cosmos", "saves", "new", "save2.hg");
        if (savePath == null) return;
        EnsureMapperLoaded();

        var save = SaveFileManager.LoadSaveFile(savePath);
        var playerState = save.GetObject("PlayerStateData");
        Assert.NotNull(playerState);

        var found = SpaceStationLogic.FindStationSystems(playerState!);
        Assert.Equal(3, found.Count);
        Assert.Equal(346, found[0].StatsIndex);
        Assert.Equal(347, found[1].StatsIndex);
        Assert.Equal(348, found[2].StatsIndex);
    }

    // --- Stat read/write ----------------------------------------------------

    [Fact]
    public void GetStatValue_EmptyValue_ReturnsZero()
    {
        var entry = BuildStationEntryWithDefaults(39583697985345L);
        Assert.Equal(0, SpaceStationLogic.GetStatValue(entry, SpaceStationLogic.StatWarStanding));
    }

    [Fact]
    public void GetStatValue_MissingStat_ReturnsZero()
    {
        var entry = BuildStationEntry(39583697985345L, ("^UNRELATED", 7));
        Assert.Equal(0, SpaceStationLogic.GetStatValue(entry, SpaceStationLogic.StatWarStanding));
    }

    [Fact]
    public void SetStatValue_WritesIntValue_AndRoundTrips()
    {
        var entry = BuildStationEntryWithDefaults(39583697985345L);
        SpaceStationLogic.SetStatValue(entry, SpaceStationLogic.StatWarStanding, 33);

        Assert.Equal(33, SpaceStationLogic.GetStatValue(entry, SpaceStationLogic.StatWarStanding));
        var stat = SpaceStationLogic.FindStat(entry, SpaceStationLogic.StatWarStanding);
        Assert.NotNull(stat);
        Assert.Equal(33, stat!.GetObject("Value")!.GetInt("IntValue"));
    }

    // --- ApplyClaimableMinimums ---------------------------------------------

    [Fact]
    public void ApplyClaimableMinimums_RaisesLowValues_KeepsHighValues()
    {
        var entry = BuildStationEntry(39583697985345L,
            (SpaceStationLogic.StatWarStanding, 33),   // above 30: untouched
            (SpaceStationLogic.StatSpPoiMissions, 5),  // at target: untouched
            (SpaceStationLogic.StatWGuildStand, null), // missing: raised to 15
            (SpaceStationLogic.StatEGuildStand, 3),    // below 15: raised to 15
            (SpaceStationLogic.StatTraStanding, 3),    // below 30: raised to 30
            (SpaceStationLogic.StatExpStanding, null), // missing: raised to 30
            (SpaceStationLogic.StatTGuildStand, null)); // missing: raised to 15

        bool changed = SpaceStationLogic.ApplyClaimableMinimums(entry);

        Assert.True(changed);
        Assert.Equal(33, SpaceStationLogic.GetStatValue(entry, SpaceStationLogic.StatWarStanding));
        Assert.Equal(5, SpaceStationLogic.GetStatValue(entry, SpaceStationLogic.StatSpPoiMissions));
        Assert.Equal(SpaceStationLogic.GuildStandTarget, SpaceStationLogic.GetStatValue(entry, SpaceStationLogic.StatWGuildStand));
        Assert.Equal(SpaceStationLogic.GuildStandTarget, SpaceStationLogic.GetStatValue(entry, SpaceStationLogic.StatEGuildStand));
        Assert.Equal(SpaceStationLogic.StandingTarget, SpaceStationLogic.GetStatValue(entry, SpaceStationLogic.StatTraStanding));
        Assert.Equal(SpaceStationLogic.StandingTarget, SpaceStationLogic.GetStatValue(entry, SpaceStationLogic.StatExpStanding));
        Assert.Equal(SpaceStationLogic.GuildStandTarget, SpaceStationLogic.GetStatValue(entry, SpaceStationLogic.StatTGuildStand));
    }

    [Fact]
    public void ApplyClaimableMinimums_AllHigh_ReturnsFalse()
    {
        var entry = BuildStationEntry(39583697985345L,
            (SpaceStationLogic.StatWarStanding, 40),
            (SpaceStationLogic.StatSpPoiMissions, 10),
            (SpaceStationLogic.StatWGuildStand, 20),
            (SpaceStationLogic.StatEGuildStand, 20),
            (SpaceStationLogic.StatTraStanding, 50),
            (SpaceStationLogic.StatExpStanding, 50),
            (SpaceStationLogic.StatTGuildStand, 20));

        bool changed = SpaceStationLogic.ApplyClaimableMinimums(entry);

        Assert.False(changed);
        Assert.Equal(40, SpaceStationLogic.GetStatValue(entry, SpaceStationLogic.StatWarStanding));
    }

    [Fact]
    public void DisplayStatOrder_GroupsGuildsRacesThenPoi()
    {
        Assert.Equal(SpaceStationLogic.StationStatIds.Length, SpaceStationLogic.DisplayStatOrder.Length);

        // Guild standings first, then race standings, then space POI missions.
        Assert.Equal(SpaceStationLogic.StatWGuildStand, SpaceStationLogic.DisplayStatOrder[0]);
        Assert.Equal(SpaceStationLogic.StatEGuildStand, SpaceStationLogic.DisplayStatOrder[1]);
        Assert.Equal(SpaceStationLogic.StatTGuildStand, SpaceStationLogic.DisplayStatOrder[2]);
        Assert.Equal(SpaceStationLogic.StatWarStanding, SpaceStationLogic.DisplayStatOrder[3]);
        Assert.Equal(SpaceStationLogic.StatTraStanding, SpaceStationLogic.DisplayStatOrder[4]);
        Assert.Equal(SpaceStationLogic.StatExpStanding, SpaceStationLogic.DisplayStatOrder[5]);
        Assert.Equal(SpaceStationLogic.StatSpPoiMissions, SpaceStationLogic.DisplayStatOrder[6]);

        // Every station stat Id appears exactly once.
        Assert.Empty(SpaceStationLogic.StationStatIds.Except(SpaceStationLogic.DisplayStatOrder));
    }

    [Fact]
    public void GetClaimableTarget_UnknownStat_ReturnsZero()
    {
        Assert.Equal(0, SpaceStationLogic.GetClaimableTarget("^NOT_A_STAT"));
        Assert.Equal(SpaceStationLogic.GuildStandTarget, SpaceStationLogic.GetClaimableTarget(SpaceStationLogic.StatEGuildStand));
        Assert.Equal(SpaceStationLogic.StandingTarget, SpaceStationLogic.GetClaimableTarget(SpaceStationLogic.StatExpStanding));
        Assert.Equal(SpaceStationLogic.SpPoiMissionsTarget, SpaceStationLogic.GetClaimableTarget(SpaceStationLogic.StatSpPoiMissions));
    }

    // --- AddressToPortalCode ------------------------------------------------

    [Theory]
    [InlineData(39583697985345L, "00244C41DF41")]              // 12-digit UA, zero reality + zero planet
    [InlineData(285876250075836L, "0104C055E2BC")]             // 13-digit UA
    [InlineData(95658791001921L, "00574C41DF41")]              // 12-digit UA
    [InlineData(4704814526177547L, "10B7FE91210B")]             // 14-digit UA
    [InlineData("0x311700FE91210B", "3117FE91210B")]           // hex string form
    [InlineData(0L, "000000000000")]                           // zero address
    public void AddressToPortalCode_ConvertsKnownAddresses(object address, string expected)
    {
        Assert.Equal(expected, SpaceStationLogic.AddressToPortalCode(address));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-an-address")]
    [InlineData("0xZZ")]
    [InlineData("0x12345678901234567890")]
    public void AddressToPortalCode_InvalidAddress_ReturnsEmpty(object? address)
    {
        Assert.Equal("", SpaceStationLogic.AddressToPortalCode(address));
    }

    // --- FindSpacePoiEntry --------------------------------------------------

    [Fact]
    public void FindSpacePoiEntry_MatchesByUa()
    {
        var array = new JsonArray();
        var entry0 = new JsonObject();
        entry0.Set("UA", 39583697985345L);
        entry0.Set("PackedData0", 1342220418);
        entry0.Set("PackedData1", 42);
        array.Add(entry0);
        var entry1 = new JsonObject();
        entry1.Set("UA", 0L);
        array.Add(entry1);

        var match = SpaceStationLogic.FindSpacePoiEntry(array, 39583697985345L);
        Assert.NotNull(match);
        Assert.Equal(0, match.Value.Index);
        Assert.Equal(1342220418, match.Value.Data.GetInt("PackedData0"));
    }

    [Fact]
    public void FindSpacePoiEntry_NoMatch_ReturnsNull()
    {
        var array = new JsonArray();
        var entry = new JsonObject();
        entry.Set("UA", 123L);
        array.Add(entry);

        Assert.Null(SpaceStationLogic.FindSpacePoiEntry(array, 999L));
        Assert.Null(SpaceStationLogic.FindSpacePoiEntry(null, 999L));
    }

    // --- ResolveStationBaseName / ResolveSystemName --------------------------

    [Fact]
    public void ResolveStationBaseName_ReturnsMatchingBaseName()
    {
        var playerState = new JsonObject();
        var bases = new JsonArray();
        var station = new JsonObject();
        station.Set("Name", "Operator 569 Junction");
        var baseType = new JsonObject();
        baseType.Set("PersistentBaseTypes", "PlayerSpaceStationBase");
        station.Set("BaseType", baseType);
        station.Set("GalacticAddress", 39583697985345L);
        bases.Add(station);
        playerState.Set("PersistentPlayerBases", bases);

        Assert.Equal("Operator 569 Junction", SpaceStationLogic.ResolveStationBaseName(playerState, 39583697985345L));
        Assert.Null(SpaceStationLogic.ResolveStationBaseName(playerState, 999L));
    }

    [Fact]
    public void ResolveSystemName_SystemDiscoveryWithCustomName_ReturnsName()
    {
        var save = new JsonObject();
        var discoveryData = new JsonObject();
        var store = new JsonObject();
        var records = new JsonArray();
        var record = new JsonObject();
        var discovery = new JsonObject();
        discovery.Set("UA", "0x24004C41DF41");
        discovery.Set("DT", "SolarSystem");
        var metadata = new JsonObject();
        metadata.Set("CN", "Hunyevs XVII // MAIN FAM");
        record.Set("DD", discovery);
        record.Set("DM", metadata);
        records.Add(record);
        store.Set("Record", records);
        discoveryData.Set("Store", store);
        var wrapper = new JsonObject();
        wrapper.Set("DiscoveryData-v1", discoveryData);
        save.Set("DiscoveryManagerData", wrapper);

        Assert.Equal("Hunyevs XVII // MAIN FAM", SpaceStationLogic.ResolveSystemName(save, 39583697985345L));
    }

    [Fact]
    public void ResolveSystemName_PlanetRecords_DoNotNameTheSystem()
    {
        // Planet records share the system address but carry the planet's name; they
        // must not be used as the system name.
        var save = new JsonObject();
        var discoveryData = new JsonObject();
        var store = new JsonObject();
        var records = new JsonArray();
        var record = new JsonObject();
        var discovery = new JsonObject();
        discovery.Set("UA", "0x4024004C41DF41");
        discovery.Set("DT", "Planet");
        var metadata = new JsonObject();
        metadata.Set("CN", "Cerces II");
        record.Set("DD", discovery);
        record.Set("DM", metadata);
        records.Add(record);
        store.Set("Record", records);
        discoveryData.Set("Store", store);
        var wrapper = new JsonObject();
        wrapper.Set("DiscoveryData-v1", discoveryData);
        save.Set("DiscoveryManagerData", wrapper);

        Assert.Null(SpaceStationLogic.ResolveSystemName(save, 39583697985345L));
    }

    [Fact]
    public void ResolveSystemName_SystemRecordWithoutCustomName_FallsBackToNameField()
    {
        var save = new JsonObject();
        var discoveryData = new JsonObject();
        var store = new JsonObject();
        var records = new JsonArray();
        var record = new JsonObject();
        var discovery = new JsonObject();
        discovery.Set("UA", "0x24004C41DF41");
        discovery.Set("DT", "SolarSystem");
        discovery.Set("Name", "Test System 569");
        record.Set("DD", discovery);
        records.Add(record);
        store.Set("Record", records);
        discoveryData.Set("Store", store);
        var wrapper = new JsonObject();
        wrapper.Set("DiscoveryData-v1", discoveryData);
        save.Set("DiscoveryManagerData", wrapper);

        Assert.Equal("Test System 569", SpaceStationLogic.ResolveSystemName(save, 39583697985345L));
    }

    [Fact]
    public void ResolveSystemName_NoRecords_ReturnsNull()
    {
        var save = new JsonObject();
        Assert.Null(SpaceStationLogic.ResolveSystemName(save, 39583697985345L));
    }

    [Fact]
    public void StationSystemEntry_ToString_ReturnsDisplayName()
    {
        var entry = new SpaceStationLogic.StationSystemEntry
        {
            StatsIndex = 0,
            Address = 39583697985345L,
            Entry = new JsonObject(),
            DisplayName = "Operator 569 Junction"
        };
        Assert.Equal("Operator 569 Junction", entry.ToString());
        Assert.Equal("Operator 569 Junction", entry.DisplayName);
    }

    // --- FindStationBase ----------------------------------------------------

    [Fact]
    public void FindStationBase_MatchesStationBase_IgnoresOthers()
    {
        var bases = new JsonArray();

        var station = new JsonObject();
        station.Set("Name", "Operator 569 Junction");
        var stationType = new JsonObject();
        stationType.Set("PersistentBaseTypes", "PlayerSpaceStationBase");
        station.Set("BaseType", stationType);
        station.Set("GalacticAddress", 39583697985345L);
        bases.Add(station);

        var home = new JsonObject();
        home.Set("Name", "Home Base");
        var homeType = new JsonObject();
        homeType.Set("PersistentBaseTypes", "HomePlanetBase");
        home.Set("BaseType", homeType);
        home.Set("GalacticAddress", 4514599014572299L);
        bases.Add(home);

        var match = SpaceStationLogic.FindStationBase(bases, 39583697985345L);
        Assert.NotNull(match);
        Assert.Equal(0, match.Value.DataIndex);
        Assert.Equal("Operator 569 Junction", match.Value.Data.GetString("Name"));

        Assert.Null(SpaceStationLogic.FindStationBase(bases, 4514599014572299L));
        Assert.Null(SpaceStationLogic.FindStationBase(null, 39583697985345L));
    }

    [Fact]
    public void FindStationBase_ReferenceSave_MatchesBase31()
    {
        string? savePath = FindRefPath("_ref", "_cosmos", "saves", "new", "save2.hg");
        if (savePath == null) return;
        EnsureMapperLoaded();

        var save = SaveFileManager.LoadSaveFile(savePath);
        var playerState = save.GetObject("PlayerStateData");
        Assert.NotNull(playerState);

        var match = SpaceStationLogic.FindStationBase(playerState!.GetArray("PersistentPlayerBases"), 39583697985345L);
        Assert.NotNull(match);
        Assert.Equal(31, match.Value.DataIndex);
        Assert.Equal("PlayerSpaceStationBase", SpaceStationLogic.GetBaseTypeName(match.Value.Data));
    }

    // --- Base kind classification -------------------------------------------

    [Theory]
    [InlineData("HomePlanetBase", (int)SpaceStationLogic.BaseKind.Home)]
    [InlineData("FreighterBase", (int)SpaceStationLogic.BaseKind.Freighter)]
    [InlineData("PlayerSpaceStationBase", (int)SpaceStationLogic.BaseKind.SpaceStation)]
    [InlineData("PlayerShipBase", (int)SpaceStationLogic.BaseKind.Corvette)]
    [InlineData("PlayerSpaceBase", (int)SpaceStationLogic.BaseKind.SpaceBase)]
    [InlineData("SomethingElse", (int)SpaceStationLogic.BaseKind.Other)]
    public void GetBaseKind_ClassifiesTypes(string type, int expectedKind)
    {
        var baseObj = new JsonObject();
        var baseType = new JsonObject();
        baseType.Set("PersistentBaseTypes", type);
        baseObj.Set("BaseType", baseType);

        Assert.Equal((SpaceStationLogic.BaseKind)expectedKind, SpaceStationLogic.GetBaseKind(baseObj));
    }

    // --- Corvette base name resolution --------------------------------------

    [Fact]
    public void ResolveCorvetteBaseName_ReadsShipOwnership()
    {
        var playerState = new JsonObject();
        var ownership = new JsonArray();
        var ship0 = new JsonObject();
        ship0.Set("Name", "The Bebop");
        ownership.Add(ship0);
        var ship1 = new JsonObject();
        ship1.Set("Name", "USCSS Abraxas");
        ownership.Add(ship1);
        playerState.Set("ShipOwnership", ownership);

        var baseObj = new JsonObject();
        baseObj.Set("UserData", 1L);

        Assert.Equal("USCSS Abraxas", SpaceStationLogic.ResolveCorvetteBaseName(playerState, baseObj));
    }

    [Fact]
    public void ResolveCorvetteBaseName_OutOfRange_ReturnsNull()
    {
        var playerState = new JsonObject();
        var ownership = new JsonArray();
        ownership.Add(new JsonObject());
        playerState.Set("ShipOwnership", ownership);

        var baseObj = new JsonObject();
        baseObj.Set("UserData", 99L);

        Assert.Null(SpaceStationLogic.ResolveCorvetteBaseName(playerState, baseObj));
    }

    // --- PackedData (SpacePoiDiscoveries) ---

    [Fact]
    public void PackedData_ParsesIntAndHexStrings()
    {
        var entry = new JsonObject();
        entry.Set("PackedData0", 16910466);
        Assert.True(SpaceStationLogic.TryGetPackedData(entry, "PackedData0", out ulong intValue, out bool intHex));
        Assert.Equal(16910466ul, intValue);
        Assert.False(intHex);

        entry.Set("PackedData0", "0xFFFFFFFFA020A882");
        Assert.True(SpaceStationLogic.TryGetPackedData(entry, "PackedData0", out ulong hexValue, out bool hexText));
        Assert.Equal(18446744072101079170ul, hexValue);
        Assert.True(hexText);

        entry.Set("PackedData0", 0x2Aul);
        Assert.True(SpaceStationLogic.TryGetPackedData(entry, "PackedData0", out ulong ulongValue, out bool ulongHex));
        Assert.Equal(42ul, ulongValue);
        Assert.False(ulongHex);
    }

    [Fact]
    public void PackedData_ParsesDecimalAndHexInput()
    {
        Assert.True(SpaceStationLogic.TryParsePackedData("0x882", out ulong hex));
        Assert.Equal(0x882ul, hex);
        Assert.True(SpaceStationLogic.TryParsePackedData("2178", out ulong dec));
        Assert.Equal(2178ul, dec);
        Assert.True(SpaceStationLogic.TryParsePackedData("0xFFFFFFFFA020A882", out ulong big));
        Assert.Equal(18446744072101079170ul, big);

        Assert.False(SpaceStationLogic.TryParsePackedData("not a number", out _));
        Assert.False(SpaceStationLogic.TryParsePackedData("", out _));
        Assert.False(SpaceStationLogic.TryParsePackedData("0xZZ", out _));
    }

    [Fact]
    public void PackedData_SetPreservesStoredRepresentation()
    {
        var entry = new JsonObject();
        entry.Set("PackedData0", 1);
        Assert.True(SpaceStationLogic.SetPackedData(entry, "PackedData0", 0x2206, preferHex: false));
        Assert.IsType<int>(entry.Get("PackedData0"));
        Assert.Equal(8710, entry.GetInt("PackedData0"));

        entry.Set("PackedData0", "0x882");
        Assert.True(SpaceStationLogic.SetPackedData(entry, "PackedData0", 0x2207, preferHex: true));
        Assert.IsType<string>(entry.Get("PackedData0"));
        Assert.Equal("0x2207", entry.Get("PackedData0"));

        // No-op writes keep the stored text and report no change.
        Assert.False(SpaceStationLogic.SetPackedData(entry, "PackedData0", 0x2207, preferHex: true));
    }

    [Fact]
    public void PackedData_SetForcesHexForHighBitValues()
    {
        var entry = new JsonObject();
        entry.Set("PackedData0", 0x01000882);
        Assert.True(SpaceStationLogic.SetPackedData(entry, "PackedData0", 0xFFFFFFFFA020A882, preferHex: false));
        Assert.IsType<string>(entry.Get("PackedData0"));
        Assert.Equal("0xFFFFFFFFA020A882", entry.Get("PackedData0"));
    }

    [Fact]
    public void PackedData_FormatsBothRepresentations()
    {
        Assert.Equal("8710", SpaceStationLogic.FormatPackedData(8710, asHex: false));
        Assert.Equal("0x2206", SpaceStationLogic.FormatPackedData(0x2206, asHex: true));
        Assert.Equal("0xFFFFFFFFA020A882", SpaceStationLogic.FormatPackedData(0xFFFFFFFFA020A882, asHex: true));
    }

    // --- PackedData slot helpers ---

    [Fact]
    public void PackedDataSlot_RoundTripsEverySlotAndLevel()
    {
        var levels = new[]
        {
            SpaceStationLogic.SpacePoiDiscoveryLevel.Hidden,
            SpaceStationLogic.SpacePoiDiscoveryLevel.Undiscovered,
            SpaceStationLogic.SpacePoiDiscoveryLevel.Discovered,
            SpaceStationLogic.SpacePoiDiscoveryLevel.Completed
        };

        for (int slot = 0; slot < SpaceStationLogic.PackedDataSlotCount; slot++)
        {
            ulong value = 0;
            foreach (var level in levels)
            {
                ulong updated = SpaceStationLogic.SetPackedDataSlot(value, slot, level);
                Assert.Equal(level, SpaceStationLogic.GetPackedDataSlot(updated, slot));

                // Every other slot must stay hidden when starting from zero.
                for (int other = 0; other < SpaceStationLogic.PackedDataSlotCount; other++)
                {
                    if (other == slot) continue;
                    Assert.Equal(SpaceStationLogic.SpacePoiDiscoveryLevel.Hidden,
                        SpaceStationLogic.GetPackedDataSlot(updated, other));
                }
            }
        }
    }

    [Fact]
    public void PackedDataSlot_SetPreservesOtherSlots()
    {
        // Recorded Iawate state 1: slot 10 hidden. Setting it to Discovered gives state 2.
        ulong state1 = 0x5000A882;
        ulong state2 = SpaceStationLogic.SetPackedDataSlot(state1, 10, SpaceStationLogic.SpacePoiDiscoveryLevel.Discovered);
        Assert.Equal(0x5020A882ul, state2);

        // Visiting the first Lost Starship: slot 14 Undiscovered -> Discovered.
        ulong state3 = SpaceStationLogic.SetPackedDataSlot(state2, 14, SpaceStationLogic.SpacePoiDiscoveryLevel.Discovered);
        Assert.Equal(0x6020A882ul, state3);

        // Visiting the second Lost Starship: slot 15 Undiscovered -> Discovered,
        // which sets bit 31 and is the point the game switches to hex storage.
        ulong state4 = SpaceStationLogic.SetPackedDataSlot(state3, 15, SpaceStationLogic.SpacePoiDiscoveryLevel.Discovered);
        Assert.Equal(0xA020A882ul, state4);

        // The 64-bit stored form must keep the sign-extension dword intact.
        ulong stored = 0xFFFFFFFFA020A882;
        ulong hiddenFlavour = SpaceStationLogic.SetPackedDataSlot(stored, 13, SpaceStationLogic.SpacePoiDiscoveryLevel.Undiscovered);
        Assert.Equal(0xFFFFFFFFA420A882ul, hiddenFlavour);
        ulong completedFlavour = SpaceStationLogic.SetPackedDataSlot(hiddenFlavour, 13, SpaceStationLogic.SpacePoiDiscoveryLevel.Completed);
        Assert.Equal(0xFFFFFFFFAC20A882ul, completedFlavour);
    }

    [Fact]
    public void PackedDataSlot_DecodesRecordedStates()
    {
        // PackedData0 of Iawate state 1 (0x5000A882).
        ulong state1 = 0x5000A882;
        foreach (int slot in new[] { 0, 3, 5, 6, 7 })
            Assert.Equal(SpaceStationLogic.SpacePoiDiscoveryLevel.Discovered,
                SpaceStationLogic.GetPackedDataSlot(state1, slot));
        foreach (int slot in new[] { 14, 15 })
            Assert.Equal(SpaceStationLogic.SpacePoiDiscoveryLevel.Undiscovered,
                SpaceStationLogic.GetPackedDataSlot(state1, slot));

        // PackedData1 of the same state (0x2A): slots 16, 17 and 18 discovered.
        ulong state1Packed1 = 0x2A;
        foreach (int slot in new[] { 0, 1, 2 })
            Assert.Equal(SpaceStationLogic.SpacePoiDiscoveryLevel.Discovered,
                SpaceStationLogic.GetPackedDataSlot(state1Packed1, slot));
    }

    [Fact]
    public void PackedDataSlot_OutOfRangeThrows()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => SpaceStationLogic.GetPackedDataSlot(0, -1));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => SpaceStationLogic.GetPackedDataSlot(0, SpaceStationLogic.PackedDataSlotCount));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => SpaceStationLogic.SetPackedDataSlot(0, SpaceStationLogic.PackedDataSlotCount,
                SpaceStationLogic.SpacePoiDiscoveryLevel.Hidden));
    }

    [Fact]
    public void PackedDataSlot_SetThroughEntryUpdatesValue()
    {
        var entry = new JsonObject();
        entry.Set("PackedData0", "0xFFFFFFFFA020A882");

        Assert.True(SpaceStationLogic.TryGetPackedData(entry, "PackedData0", out ulong value, out bool isHex));
        ulong updated = SpaceStationLogic.SetPackedDataSlot(value, 13, SpaceStationLogic.SpacePoiDiscoveryLevel.Undiscovered);
        Assert.True(SpaceStationLogic.SetPackedData(entry, "PackedData0", updated, preferHex: isHex));

        Assert.IsType<string>(entry.Get("PackedData0"));
        Assert.Equal("0xFFFFFFFFA420A882", entry.Get("PackedData0"));
    }
}