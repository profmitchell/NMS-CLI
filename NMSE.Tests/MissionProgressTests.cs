using NMSE.Core;
using NMSE.Data;
using NMSE.IO;
using NMSE.Models;

namespace NMSE.Tests;

/// <summary>
/// Tests for the Cosmos missions section: MissionProgress read/write, mission
/// state mapping (Not Started / Active / Completed) and the Cosmos mission ID set.
/// </summary>
public class MissionProgressTests
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
    private static JsonObject BuildPlayerStateWithMissions(params (string Id, int Progress)[] missions)
    {
        var playerState = new JsonObject();
        var array = new JsonArray();
        foreach (var (id, progress) in missions)
        {
            var entry = new JsonObject();
            entry.Set("Mission", id);
            entry.Set("Progress", progress);
            entry.Set("Seed", 0);
            entry.Set("Data", 0);
            entry.Set("Stat", 0);
            entry.Set("Participants", new JsonArray());
            array.Add(entry);
        }
        playerState.Set("MissionProgress", array);
        return playerState;
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

    // --- GetMissionProgress -------------------------------------------------

    [Fact]
    public void GetMissionProgress_MissingEntry_ReturnsNull()
    {
        var playerState = BuildPlayerStateWithMissions(("^OTHER", 5));
        Assert.Null(SpaceStationLogic.GetMissionProgress(playerState, "^CLAIM_STAT_TUT"));
    }

    [Fact]
    public void GetMissionProgress_FindsFirstMatchingEntry()
    {
        var playerState = BuildPlayerStateWithMissions(("^CLAIM_STAT_TUT", 10), ("^CLAIM_STAT_TUT", 99));
        Assert.Equal(10, SpaceStationLogic.GetMissionProgress(playerState, "^CLAIM_STAT_TUT"));
    }

    // --- SetMissionProgress -------------------------------------------------

    [Fact]
    public void SetMissionProgress_CreatesWellFormedEntry()
    {
        var playerState = new JsonObject();
        SpaceStationLogic.SetMissionProgress(playerState, "^CLAIM_STAT_TUT", 5);

        var missions = playerState.GetArray("MissionProgress");
        Assert.NotNull(missions);
        Assert.Equal(1, missions!.Length);

        var entry = missions!.GetObject(0);
        Assert.Equal("^CLAIM_STAT_TUT", entry.GetString("Mission"));
        Assert.Equal(5, entry.GetInt("Progress"));
        Assert.Equal(0, entry.GetInt("Seed"));
        Assert.Equal(0, entry.GetInt("Data"));
        Assert.Equal(0, entry.GetInt("Stat"));

        // The game's own entries carry a 13-entry Participants array.
        var participants = entry.GetArray("Participants");
        Assert.NotNull(participants);
        Assert.Equal(13, participants!.Length);
    }

    [Fact]
    public void SetMissionProgress_UpdatesExistingEntry_PreservesOtherFields()
    {
        var playerState = BuildPlayerStateWithMissions(("^CLAIM_STAT_TUT", 10));
        var missions = playerState.GetArray("MissionProgress")!;
        var original = missions.GetObject(0);
        original.Set("Seed", 12345);
        original.Set("Data", 678);

        SpaceStationLogic.SetMissionProgress(playerState, "^CLAIM_STAT_TUT", 15);

        Assert.Equal(1, missions.Length);
        Assert.Equal(15, original.GetInt("Progress"));
        Assert.Equal(12345, original.GetInt("Seed"));
        Assert.Equal(678, original.GetInt("Data"));
    }

    [Fact]
    public void SetMissionProgress_CreatesArrayWhenMissing()
    {
        var playerState = new JsonObject();
        SpaceStationLogic.SetMissionProgress(playerState, "^POI_BONES", 0);
        Assert.NotNull(playerState.GetArray("MissionProgress"));
    }

    // --- GetMissionState ----------------------------------------------------

    [Theory]
    [InlineData(null, (int)SpaceStationLogic.MissionState.NotStarted)]
    [InlineData(-1, (int)SpaceStationLogic.MissionState.NotStarted)]
    [InlineData(0, (int)SpaceStationLogic.MissionState.Active)]
    [InlineData(10, (int)SpaceStationLogic.MissionState.Active)]
    [InlineData(int.MaxValue, (int)SpaceStationLogic.MissionState.Completed)]
    public void GetMissionState_MapsProgress(int? progress, int expectedState)
    {
        var playerState = progress.HasValue
            ? BuildPlayerStateWithMissions(("^CLAIM_STAT_TUT", progress.Value))
            : new JsonObject();
        Assert.Equal((SpaceStationLogic.MissionState)expectedState, SpaceStationLogic.GetMissionState(playerState, "^CLAIM_STAT_TUT"));
    }

    // --- ApplyMissionState --------------------------------------------------

    [Fact]
    public void ApplyMissionState_NotStarted_SetsMinusOne()
    {
        var playerState = BuildPlayerStateWithMissions(("^CLAIM_STAT_TUT", 10));
        SpaceStationLogic.ApplyMissionState(playerState, "^CLAIM_STAT_TUT", SpaceStationLogic.MissionState.NotStarted);
        Assert.Equal(-1, SpaceStationLogic.GetMissionProgress(playerState, "^CLAIM_STAT_TUT"));
    }

    [Fact]
    public void ApplyMissionState_Active_CreatesEntryAtZero_WhenMissing()
    {
        var playerState = new JsonObject();
        SpaceStationLogic.ApplyMissionState(playerState, "^POI_BONES", SpaceStationLogic.MissionState.Active);
        Assert.Equal(0, SpaceStationLogic.GetMissionProgress(playerState, "^POI_BONES"));
    }

    [Fact]
    public void ApplyMissionState_Active_RaisesNotStartedToZero()
    {
        var playerState = BuildPlayerStateWithMissions(("^POI_BONES", -1));
        SpaceStationLogic.ApplyMissionState(playerState, "^POI_BONES", SpaceStationLogic.MissionState.Active);
        Assert.Equal(0, SpaceStationLogic.GetMissionProgress(playerState, "^POI_BONES"));
    }

    [Fact]
    public void ApplyMissionState_Active_PreservesExistingProgress()
    {
        var playerState = BuildPlayerStateWithMissions(("^CLAIM_STAT_TUT", 10));
        SpaceStationLogic.ApplyMissionState(playerState, "^CLAIM_STAT_TUT", SpaceStationLogic.MissionState.Active);
        Assert.Equal(10, SpaceStationLogic.GetMissionProgress(playerState, "^CLAIM_STAT_TUT"));
    }

    [Fact]
    public void ApplyMissionState_Completed_SetsSentinel()
    {
        var playerState = BuildPlayerStateWithMissions(("^POI_BONES", 2));
        SpaceStationLogic.ApplyMissionState(playerState, "^POI_BONES", SpaceStationLogic.MissionState.Completed);
        Assert.Equal(int.MaxValue, SpaceStationLogic.GetMissionProgress(playerState, "^POI_BONES"));
        Assert.Equal(SpaceStationLogic.MissionState.Completed, SpaceStationLogic.GetMissionState(playerState, "^POI_BONES"));
    }

    // --- CosmosMissionIds ---------------------------------------------------

    [Fact]
    public void CosmosMissionIds_AreTheThreeStationClaimMissions()
    {
        // Only the station-claim tutorials remain after testing; the other
        // Cosmos missions (space POI, hulk salvage, salvage contracts) were pruned.
        Assert.Equal(3, SpaceStationLogic.CosmosMissionIds.Length);
        Assert.Equal("^CLAIM_STAT_TUT", SpaceStationLogic.CosmosMissionIds[0]);
        Assert.Equal("^SPACEBASE_TUT", SpaceStationLogic.CosmosMissionIds[1]);
        Assert.Equal("^STATIONOWN_WIKI", SpaceStationLogic.CosmosMissionIds[2]);
    }

    [Fact]
    public void CosmosMissionIds_AllPrefixedAndKnownExamples()
    {
        var ids = SpaceStationLogic.CosmosMissionIds.ToList();
        Assert.Equal(3, ids.Count);
        Assert.All(ids, id => Assert.StartsWith("^", id, StringComparison.Ordinal));
        Assert.Contains("^CLAIM_STAT_TUT", ids);
        Assert.Contains("^SPACEBASE_TUT", ids);
        Assert.Contains("^STATIONOWN_WIKI", ids);
        Assert.DoesNotContain("^SYSMAP_TUT", ids);
        Assert.DoesNotContain("^SO_COLLECT_H1", ids); // pruned after testing
        Assert.DoesNotContain("^HULK_MELTDOWN", ids); // pruned after testing
        Assert.DoesNotContain("^POI_16_MSG", ids);    // pre-Cosmos, excluded
    }

    [Fact]
    public void GetCosmosMissionLabelKey_ReturnsExpectedKeys()
    {
        Assert.Equal("base.station.mission_claim_stat_tut", SpaceStationLogic.GetCosmosMissionLabelKey("^CLAIM_STAT_TUT"));
        Assert.Equal("base.station.mission_spacebase_tut", SpaceStationLogic.GetCosmosMissionLabelKey("^SPACEBASE_TUT"));
        Assert.Equal("base.station.mission_stationown_wiki", SpaceStationLogic.GetCosmosMissionLabelKey("^STATIONOWN_WIKI"));
        Assert.Null(SpaceStationLogic.GetCosmosMissionLabelKey("^SO_COLLECT_H1"));
        Assert.Null(SpaceStationLogic.GetCosmosMissionLabelKey(""));
    }

    // --- Reference save -----------------------------------------------------

    [Fact]
    public void ReferenceSave_MissionStates_Expected()
    {
        string? savePath = FindRefPath("_ref", "_cosmos", "saves", "new", "save2.hg");
        if (savePath == null) return;
        EnsureMapperLoaded();

        var save = SaveFileManager.LoadSaveFile(savePath);
        var playerState = save.GetObject("PlayerStateData");
        Assert.NotNull(playerState);

        // CLAIM_STAT_TUT is at progress 10 in the reference save (Active).
        Assert.Equal(10, SpaceStationLogic.GetMissionProgress(playerState!, "^CLAIM_STAT_TUT"));
        Assert.Equal(SpaceStationLogic.MissionState.Active, SpaceStationLogic.GetMissionState(playerState!, "^CLAIM_STAT_TUT"));

        // All Cosmos mission IDs resolve without throwing.
        foreach (string id in SpaceStationLogic.CosmosMissionIds)
            _ = SpaceStationLogic.GetMissionState(playerState!, id);
    }
}