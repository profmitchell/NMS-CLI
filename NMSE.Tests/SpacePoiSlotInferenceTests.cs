using NMSE.Core;
using NMSE.Data;

namespace NMSE.Tests;

/// <summary>
/// Tests for the space POI slot type inference and the Space POI table loader.
/// </summary>
public class SpacePoiSlotInferenceTests
{
    private static string? FindResourceJsonDir()
    {
        // Walk up from test assembly directory to find Resources/json
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        for (int i = 0; i < 10; i++)
        {
            var candidate = Path.Combine(dir, "Resources", "json");
            if (Directory.Exists(candidate)) return candidate;
            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }
        return null;
    }

    private static bool TryLoadTypes(out IReadOnlyList<SpacePoiTypeRule> types)
    {
        types = SpacePoiTableDatabase.Types;
        var dir = FindResourceJsonDir();
        if (dir == null) return false;
        return SpacePoiTableDatabase.LoadFromFile(Path.Combine(dir, "Space POI.json"));
    }

    private static string? TypeAt(SpacePoiSlotInferenceResult result, IReadOnlyList<SpacePoiTypeRule> types, int slot) =>
        result.Slots[slot].TypeIsUnique ? types[result.Slots[slot].TypeIndex].Type : null;

    [Fact]
    public void LoadFromFile_LoadsTypesInGenerationOrder()
    {
        if (!TryLoadTypes(out var types)) return;

        Assert.Equal(14, types.Count);
        Assert.Equal("AsteroidBelt", types[0].Type);
        Assert.Equal(1, types[0].MinCount);
        Assert.Equal(3, types[0].MaxCount);
        Assert.Equal("AbandonedBase", types[^1].Type);

        var hulk = types.First(t => t.Type == "Hulk");
        Assert.Equal(0, hulk.MinCount);
        Assert.Equal(3, hulk.MaxCount);
        Assert.Equal(2, hulk.ForcedHiddenExtras);
        Assert.Equal(SpaceStationLogic.SpacePoiDiscoveryLevel.Undiscovered, hulk.NormalInitialLevel);
        Assert.Equal("UI_SPACEPOI_TYPE_HULK", hulk.NameLocKey);

        var outpostSlime = types.First(t => t.Type == "OutpostSlime");
        Assert.Equal("UI_SPACEPOI_TYPE_OUTPOST_SLIME", outpostSlime.NameLocKey);

        // Abandoned/empty system spawn restrictions come from the table.
        Assert.False(types.First(t => t.Type == "Outpost").AllowedInAbandonedSystem);
        Assert.True(types.First(t => t.Type == "AsteroidBelt").AllowedInAbandonedSystem);
        Assert.False(types.First(t => t.Type == "Hulk").AllowedInEmptySystem);
    }

    [Fact]
    public void Infer_AbandonedSystem_UsesRestrictedRules()
    {
        if (!TryLoadTypes(out var types)) return;

        // Recorded abandoned-mode value (Hunyevs XVII): slots 0=2, 3=1, 5=2, 6=2,
        // 7=2, 14=2. It has no position for a mandatory Outpost, so the standard
        // rules find no layout at all.
        Assert.Null(SpacePoiSlotInference.Infer(0x2000A842, 0, types));

        // With the abandoned-system rules (no Outpost) the layout is identified.
        var result = SpacePoiSlotInference.Infer(0x2000A842, 0, types, abandonedSystems: true);
        Assert.NotNull(result);

        Assert.Equal("AsteroidBelt", TypeAt(result, types, 0));
        Assert.Equal("Hulk", TypeAt(result, types, 1));
        Assert.Equal("Hulk", TypeAt(result, types, 2));
        Assert.Equal("OutpostSlime", TypeAt(result, types, 3));
        Assert.Equal("OutpostSlime", TypeAt(result, types, 4));
        Assert.Equal("Star", TypeAt(result, types, 5));
        Assert.Equal("BasePlatform", TypeAt(result, types, 6));
        Assert.Equal("BasePlatform", TypeAt(result, types, 7));
        Assert.Equal("BasePlatform", TypeAt(result, types, 8));
        Assert.Equal("Flavour", TypeAt(result, types, 12));
    }

    [Fact]
    public void Infer_GeroguQili_ReturnsUniqueLayout()
    {
        if (!TryLoadTypes(out var types)) return;

        var result = SpacePoiSlotInference.Infer(0x2206, 0, types);

        Assert.NotNull(result);
        Assert.Equal(1, result!.LayoutCount);

        var expected = new[]
        {
            "AsteroidBelt", "Hulk", "Hulk", "Hulk", "Outpost", "OutpostSlime", "Star",
            "BasePlatform", "BasePlatform", "AbandonedFreighter", "SpaceWhale",
            "Flavour", "Flavour", "BasePlatform_Ice", "BasePlatform_Ice"
        };
        for (int slot = 0; slot < expected.Length; slot++)
        {
            Assert.True(result.Slots[slot].TypeIsUnique, $"slot {slot} should have a unique type");
            Assert.Equal(expected[slot], TypeAt(result, types, slot));
        }
        for (int slot = expected.Length; slot < SpacePoiSlotInference.SlotCount; slot++)
            Assert.False(result.Slots[slot].DefinitelyAllocated, $"slot {slot} should be unallocated");
    }

    [Fact]
    public void Infer_IawateReference_UniqueAndAmbiguousSlots()
    {
        if (!TryLoadTypes(out var types)) return;

        var result = SpacePoiSlotInference.Infer(0x5000A882, 0x2A, types);

        Assert.NotNull(result);
        Assert.True(result!.LayoutCount > 1);

        // Slots pinned by the recorded Iawate state.
        Assert.Equal("AsteroidBelt", TypeAt(result, types, 0));
        Assert.Equal("Hulk", TypeAt(result, types, 1));
        Assert.Equal("Hulk", TypeAt(result, types, 2));
        Assert.Equal("Outpost", TypeAt(result, types, 3));
        Assert.Equal("OutpostSlime", TypeAt(result, types, 4));
        Assert.Equal("Star", TypeAt(result, types, 5));
        Assert.Equal("BasePlatform", TypeAt(result, types, 6));
        Assert.Equal("BasePlatform", TypeAt(result, types, 7));
        Assert.Equal("BasePlatform", TypeAt(result, types, 8));
        Assert.Equal("BasePlatform", TypeAt(result, types, 9));
        Assert.Equal("AbandonedFreighter", TypeAt(result, types, 10));
        Assert.Equal("SpaceWhale", TypeAt(result, types, 11));
        Assert.Equal("Flavour", TypeAt(result, types, 12));
        Assert.Equal("Flavour", TypeAt(result, types, 13));
        Assert.Equal("Derelict", TypeAt(result, types, 14));
        Assert.Equal("Derelict", TypeAt(result, types, 15));

        // The WasteSite/IceField tail is ambiguous in the reference value, and slots
        // 19/20 are only allocated in some consistent layouts, so neither may be
        // labelled (a label must hold in every consistent layout).
        Assert.Null(TypeAt(result, types, 16));
        Assert.Null(TypeAt(result, types, 17));
        Assert.Null(TypeAt(result, types, 18));
        Assert.Null(TypeAt(result, types, 19));
        Assert.Null(TypeAt(result, types, 20));
        Assert.False(result.Slots[19].TypeIsUnique);
        Assert.False(result.Slots[20].TypeIsUnique);

        // Slots beyond the 21 allocated POIs cannot be allocated.
        for (int slot = 21; slot < SpacePoiSlotInference.SlotCount; slot++)
            Assert.False(result.Slots[slot].DefinitelyAllocated, $"slot {slot} should be unallocated");
    }

    [Fact]
    public void Infer_EditedAllDiscovered_DoesNotLabelPartiallyAllocatedSlots()
    {
        if (!TryLoadTypes(out var types)) return;

        // Gerogu-Qili after every slot was set to Discovered (all values 2) except
        // slot 14 (internal 13) which is Hidden. Thousands of layouts fit, and the
        // tail slots 28-30 are only allocated in a few of them. They must not be
        // labelled: in the true 15-slot layout they do not exist.
        var result = SpacePoiSlotInference.Infer(0xA2AAAAAA, 0xAAAAAAAA, types);

        Assert.NotNull(result);
        Assert.True(result!.LayoutCount > 1000);
        Assert.Equal("AsteroidBelt", TypeAt(result, types, 0));

        foreach (int slot in new[] { 27, 28, 29 })
        {
            Assert.False(result.Slots[slot].TypeIsUnique, $"slot {slot} must not be labelled");
            Assert.False(result.Slots[slot].DefinitelyAllocated, $"slot {slot} is only partially allocated");
        }

        Assert.False(result.Slots[30].CoveredByLayouts);
        Assert.False(result.Slots[31].CoveredByLayouts);
    }

    [Fact]
    public void Infer_UnallocatedSlotEdited_StillInfersLayout()
    {
        if (!TryLoadTypes(out var types)) return;

        // Hand-edited unallocated slot 25 (slot 9 of PackedData1) set to Discovered.
        // The strict pass rejects it; the floor-only fallback still maps the layout,
        // though with fewer unique slots than the strict pass.
        ulong packed1 = 0x2A | (2UL << 18);
        var result = SpacePoiSlotInference.Infer(0x5000A882, packed1, types);

        Assert.NotNull(result);
        Assert.True(result!.LayoutCount > 1);
        Assert.Equal("AsteroidBelt", TypeAt(result, types, 0));
        Assert.Equal("Star", TypeAt(result, types, 5));
        Assert.Null(TypeAt(result, types, 19));
        Assert.Null(TypeAt(result, types, 14));
        Assert.False(result.Slots[25].DefinitelyAllocated);
    }

    [Fact]
    public void Infer_ValuesBelowInitialLevel_ReturnsNull()
    {
        if (!TryLoadTypes(out var types)) return;

        // Clear slot 5 (Star, initial level Discovered). No layout can fit a Hidden
        // value where a Discovered-floor type must sit.
        ulong withoutStar = 0x5000A882 & ~(3UL << 10);
        Assert.Null(SpacePoiSlotInference.Infer(withoutStar, 0x2A, types));
    }

    [Fact]
    public void Infer_NoRules_ReturnsNull()
    {
        Assert.Null(SpacePoiSlotInference.Infer(0, 0, Array.Empty<SpacePoiTypeRule>()));
    }

    [Fact]
    public void SlotTokens_MergeFreshWinsAndCacheFills()
    {
        var fresh = new[] { "AsteroidBelt", SpacePoiSlotTokens.Unknown, SpacePoiSlotTokens.Unallocated, "Hulk" };
        var remembered = new[] { "BasePlatform", "Star", "Derelict", "Flavour" };

        var merged = SpacePoiSlotTokens.Merge(fresh, remembered);

        Assert.Equal("AsteroidBelt", merged[0]); // fresh wins
        Assert.Equal("Star", merged[1]);         // cache fills the unknown
        Assert.Equal(SpacePoiSlotTokens.Unallocated, merged[2]); // fresh unallocated wins
        Assert.Equal("Hulk", merged[3]);         // fresh wins
    }

    [Fact]
    public void SlotTokens_MergeWithoutCacheKeepsFresh()
    {
        var fresh = new[] { "Hulk", SpacePoiSlotTokens.Unknown };
        var merged = SpacePoiSlotTokens.Merge(fresh, null);
        Assert.Equal(fresh, merged);
    }

    [Fact]
    public void SlotTokens_FormatAndParseRoundTrip()
    {
        var tokens = new[] { "AsteroidBelt", SpacePoiSlotTokens.Unknown, SpacePoiSlotTokens.Unallocated, "BasePlatform_Ice" };
        string text = SpacePoiSlotTokens.Format(tokens);
        Assert.Equal("AsteroidBelt|.|-|BasePlatform_Ice", text);
        Assert.Equal(tokens, SpacePoiSlotTokens.Parse(text));
        Assert.Empty(SpacePoiSlotTokens.Parse(null));
    }

    [Fact]
    public void ApplyLocalisation_UsesGameStringsAndReverts()
    {
        if (!TryLoadTypes(out var types)) return;
        var dir = FindResourceJsonDir();
        if (dir == null) return;

        SpacePoiTableDatabase.RevertLocalisation();
        foreach (var type in types)
            Assert.Equal(type.Name, type.DisplayName);

        var service = new LocalisationService();
        service.SetLangDirectory(Path.Combine(dir, "lang"));
        if (!service.LoadLanguage("de-DE")) return;

        SpacePoiTableDatabase.ApplyLocalisation(service);
        foreach (var type in types)
            Assert.Equal(service.Lookup(type.NameLocKey) ?? type.Name, type.DisplayName);

        SpacePoiTableDatabase.RevertLocalisation();
        foreach (var type in types)
            Assert.Equal(type.Name, type.DisplayName);
    }
}
