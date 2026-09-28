using NMSE.Extractor.Data;

namespace NMSE.Extractor.Tests;

public class CategorizerTests
{
    private static Dictionary<string, object?> MakeItem(string id, string name, string group) => new()
    {
        ["Id"] = id, ["Name"] = name, ["Group"] = group
    };

    private static Dictionary<string, object?> MakeBuildingPartItem(string id, string name, string group) => new()
    {
        ["Id"] = id, ["Name"] = name, ["Group"] = group,
        ["ProductCategory"] = "BuildingPart", ["SubstanceCategory"] = "BuildingPart"
    };

    [Fact]
    public void CategorizeItem_EmptyGroup_ReturnsNull()
    {
        var item = MakeItem("TEST", "Test Item", "");
        Assert.Null(Categorizer.CategorizeItem(item));
    }

    [Theory]
    [InlineData("Edible Product", "Food.json")]
    [InlineData("Carnivore Bait", "Food.json")]
    [InlineData("Crafted Technology Component", "Products.json")]
    [InlineData("Abundant Mineral", "Raw Materials.json")]
    [InlineData("Unrefined Organic Element", "Raw Materials.json")]
    [InlineData("Common Fish", "Fish.json")]
    [InlineData("Trade Goods", "Trade.json")]
    [InlineData("Construction module", "Buildings.json")]
    [InlineData("Decoration", "Buildings.json")]
    [InlineData("Wall Access Route", "Buildings.json")]
    [InlineData("Constructable Relic", "Buildings.json")]
    [InlineData("Access Card", "Constructed Technology.json")]
    [InlineData("High value curiosity", "Curiosities.json")]
    [InlineData("Mission Location System", "Corvette.json")]
    [InlineData("Self-Mounted Refiner Unit", "Technology.json")]
    [InlineData("Priceless Fragment", "Technology.json")]
    public void CategorizeItem_ExactGroupMatches(string group, string expectedFile)
    {
        var item = MakeItem("TEST", "Test Item", group);
        Assert.Equal(expectedFile, Categorizer.CategorizeItem(item));
    }

    [Fact]
    public void CategorizeItem_UpgradeInGroup_GoesToUpgrades()
    {
        var item = MakeItem("TEST", "Test Item", "A-Class Hyperdrive Upgrade");
        Assert.Equal("Upgrades.json", Categorizer.CategorizeItem(item));
    }

    [Fact]
    public void CategorizeItem_UpgradeInName_GoesToUpgrades()
    {
        var item = MakeItem("TEST", "Some Upgrade Module", "SomeGroup");
        Assert.Equal("Upgrades.json", Categorizer.CategorizeItem(item));
    }

    [Theory]
    [InlineData("Corvette Hull", "Corvette.json")]
    [InlineData("Corvette Engine", "Corvette.json")]
    public void CategorizeItem_CorvettePrefixMatches(string group, string expectedFile)
    {
        var item = MakeItem("TEST", "Test Item", group);
        Assert.Equal(expectedFile, Categorizer.CategorizeItem(item));
    }

    [Fact]
    public void CategorizeItem_ExocraftInGroup_GoesToExocraft()
    {
        var item = MakeItem("TEST", "Test Item", "Exocraft Tech");
        Assert.Equal("Exocraft.json", Categorizer.CategorizeItem(item));
    }

    [Theory]
    [InlineData("Space Station Decoration", "Station.json")]
    [InlineData("Orbital Base Module", "Station.json")]
    public void CategorizeItem_StationGroups_GoToStation(string group, string expectedFile)
    {
        var item = MakeItem("TEST", "Test Item", group);
        Assert.Equal(expectedFile, Categorizer.CategorizeItem(item));
    }

    // STA_ROOM_NPCVEH: station part whose name contains "Exocraft" - the station
    // group rule must win over the vehicle keyword routing.
    [Fact]
    public void CategorizeItem_StationPartWithVehicleKeywordName_GoesToStation()
    {
        var item = MakeBuildingPartItem("STA_ROOM_NPCVEH", "Exocraft Terminal", "Orbital Base Module");
        Assert.Equal("Station.json", Categorizer.CategorizeItem(item));
    }

    // BuildingPart products matching vehicle keywords are base/freighter build
    // parts (rooms, bays, terminals), not exocraft tech.
    [Theory]
    [InlineData("GARAGE_SUB", "Nautilon Chamber", "Submarine Docking Bay")]
    [InlineData("FRE_ROOM_NPCVEH", "Exocraft Specialist's Room", "Worker Terminal")]
    [InlineData("GARAGE_FLOAT", "Nautilon Platform", "Surface-Deployable Submarine Bay")]
    public void CategorizeItem_BuildingPartWithVehicleKeyword_GoesToBuildings(string id, string name, string group)
    {
        var item = MakeBuildingPartItem(id, name, group);
        Assert.Equal("Buildings.json", Categorizer.CategorizeItem(item));
    }

    // SHIPSUMMON: BuildingPart product whose group was removed from the Others
    // exact rules; it falls through to the BuildingPart fallback.
    [Fact]
    public void CategorizeItem_ShipSummoningBeacon_GoesToBuildings()
    {
        var item = MakeBuildingPartItem("SHIPSUMMON", "Muster Point", "Ship-summoning beacon");
        Assert.Equal("Buildings.json", Categorizer.CategorizeItem(item));
    }

    // SWARM_TROPHY_B: BuildingPart product with no exact group rule.
    [Fact]
    public void CategorizeItem_BuildingPartUnknownGroup_GoesToBuildings()
    {
        var item = MakeBuildingPartItem("SWARM_TROPHY_B", "Prismatic Core", "Antivitreous Device Memento");
        Assert.Equal("Buildings.json", Categorizer.CategorizeItem(item));
    }

    // A non-BuildingPart product with a vehicle keyword still goes to Exocraft.
    [Fact]
    public void CategorizeItem_NonBuildingPartVehicleKeyword_StillGoesToExocraft()
    {
        var item = MakeItem("VEHICLE_ENGINE", "Fusion Engine", "Exocraft Power System");
        Assert.Equal("Exocraft.json", Categorizer.CategorizeItem(item));
    }

    [Fact]
    public void CategorizeItem_DynamicTechModulePattern_GoesToTechModule()
    {
        var item = MakeItem("TEST", "Test Item", "S-Class Mining Beam Upgrade");
        // "upgrade" keyword sends it to Upgrades.json (higher priority)
        Assert.Equal("Upgrades.json", Categorizer.CategorizeItem(item));
    }

    [Fact]
    public void CategorizeItem_JunkGroup_ReturnsNull()
    {
        var item = MakeItem("TEST", "Test Item", "Biggs Test Group");
        Assert.Null(Categorizer.CategorizeItem(item));
    }

    [Fact]
    public void CategorizeItem_UntranslatedName_ReturnsNull()
    {
        var item = MakeItem("UI_TEST", "UI_TEST", "Some Valid Group");
        Assert.Null(Categorizer.CategorizeItem(item));
    }

    [Fact]
    public void CategorizeItem_UnknownGroup_GoesToOthers()
    {
        var item = MakeItem("TEST", "Test Item", "Never Before Seen Category");
        Assert.Equal("Others.json", Categorizer.CategorizeItem(item));
    }

    [Fact]
    public void CategorizeItem_TechPackException_ReturnsNull()
    {
        var item = MakeItem("U_TECHPACK_CORE", "Core Package", "Archived Technology Package");
        Assert.Null(Categorizer.CategorizeItem(item));
    }

    [Fact]
    public void CategorizeItem_TechBoxException_ReturnsNull()
    {
        var item = MakeItem("U_TECHBOX_ALIEN", "Alien Implant", "Potent Nodule");
        Assert.Null(Categorizer.CategorizeItem(item));
    }

    [Theory]
    [InlineData("SPACEGUNK1")]
    [InlineData("SPACEGUNK2")]
    [InlineData("SPACEGUNK3")]
    [InlineData("SPACEGUNK4")]
    [InlineData("SPACEGUNK5")]
    public void CategorizeItem_SpaceGunk_GoesToRawMaterials(string itemId)
    {
        var item = MakeItem(itemId, "Residual Goop", "Junk");
        Assert.Equal("Raw Materials.json", Categorizer.CategorizeItem(item));
    }

    // Verify Raw Materials re-routing categories match expected
    [Theory]
    [InlineData("Reward Item", "Others.json")]
    [InlineData("Technological Currency", "Others.json")]
    [InlineData("Anomalous Material", "Curiosities.json")]
    [InlineData("Compressed Atmospheric Gas", "Raw Materials.json")]
    public void CategorizeItem_RawMaterialReRoutingGroups(string group, string expectedFile)
    {
        var item = MakeItem("TEST", "Test Material", group);
        Assert.Equal(expectedFile, Categorizer.CategorizeItem(item));
    }

    // Verify T_BOBBLE items go to Others (Starship Interior Adornment)
    [Fact]
    public void CategorizeItem_StarshipInteriorAdornment_GoesToOthers()
    {
        var item = MakeItem("BOBBLE_ATLAS", "Atlas Bobblehead", "Starship Interior Adornment");
        Assert.Equal("Others.json", Categorizer.CategorizeItem(item));
    }

    // Verify "Exclusive Spacecraft" goes to Starships (Starship routing runs before exact rules)
    [Fact]
    public void CategorizeItem_ExclusiveSpacecraft_GoesToStarships()
    {
        var item = MakeItem("TEST", "Test Ship", "Exclusive Spacecraft");
        Assert.Equal("Starships.json", Categorizer.CategorizeItem(item));
    }

    // Verify Starship Core Component goes to Upgrades (via StarshipUpgradeGroups)
    [Fact]
    public void CategorizeItem_StarshipCoreComponent_GoesToUpgrades()
    {
        var item = MakeItem("TEST", "Test Core", "Starship Core Component");
        Assert.Equal("Upgrades.json", Categorizer.CategorizeItem(item));
    }

    // Verify ship component groups go to Others (excluded from Starships)
    [Theory]
    [InlineData("Starship Exhaust Override", "Others.json")]
    [InlineData("Damaged Starship Component", "Others.json")]
    public void CategorizeItem_ExcludedStarshipGroups_GoToOthers(string group, string expectedFile)
    {
        var item = MakeItem("TEST", "Test Component", group);
        Assert.Equal(expectedFile, Categorizer.CategorizeItem(item));
    }
}
