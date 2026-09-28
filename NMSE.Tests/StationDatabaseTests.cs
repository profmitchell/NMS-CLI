using NMSE.Core;
using NMSE.Data;

namespace NMSE.Tests;

/// <summary>
/// Tests for the Cosmos v7.0 Station.json database (space station build parts)
/// and the re-categorised BuildingPart items moved into Buildings.json.
/// </summary>
public class StationDatabaseTests
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

    /// <summary>Loads the full item database from the Resources/json directory.</summary>
    private static GameItemDatabase LoadDatabase()
    {
        var db = new GameItemDatabase();
        var jsonDir = FindResourceJsonDir();
        if (jsonDir == null)
            throw new InvalidOperationException("Resources/json directory not found");
        db.LoadItemsFromJsonDirectory(jsonDir);
        return db;
    }

    [Fact]
    public void StationDatabase_ContainsAllStationParts()
    {
        string jsonPath = Path.Combine(FindResourceJsonDir() ?? "", "Station.json");
        if (!File.Exists(jsonPath)) return; // Skip until the extractor re-run produces the file

        var db = LoadDatabase();
        var stationItems = db.GetItemsByType("Station").ToList();

        // The reference data has exactly 149 STA_* items across the two station groups.
        Assert.Equal(149, stationItems.Count);
        Assert.All(stationItems, item => Assert.StartsWith("STA_", item.Id, StringComparison.Ordinal));
    }

    [Fact]
    public void StationDatabase_AllItemsAreBuildingPartProducts()
    {
        string jsonPath = Path.Combine(FindResourceJsonDir() ?? "", "Station.json");
        if (!File.Exists(jsonPath)) return;

        var db = LoadDatabase();
        var stationItems = db.GetItemsByType("Station").ToList();
        Assert.NotEmpty(stationItems);

        foreach (var item in stationItems)
        {
            Assert.Equal("Product", item.SourceTable);
            Assert.False(item.IsBuilding, $"{item.Id} should not be a Buildings-type item");
        }
    }

    [Fact]
    public void StationDatabase_ContainsKnownExamples()
    {
        string jsonPath = Path.Combine(FindResourceJsonDir() ?? "", "Station.json");
        if (!File.Exists(jsonPath)) return;

        var db = LoadDatabase();

        var sign = db.GetItem("STA_SMALLSIGN");
        Assert.NotNull(sign);
        Assert.Equal("Satellite Sign", sign!.Name);
        Assert.Equal("Station", sign.ItemType);

        var numeric = db.GetItem("STA_78");
        Assert.NotNull(numeric);
        Assert.Equal("Curious Fauna Display", numeric!.Name);

        var terminal = db.GetItem("STA_ROOM_NPCVEH");
        Assert.NotNull(terminal);
        Assert.Equal("Exocraft Terminal", terminal!.Name);
    }

    [Fact]
    public void StationItem_CargoOnly_RejectedFromTech_AcceptedInCargo()
    {
        var item = new GameItem
        {
            ItemType = "Station",
            Id = "STA_SMALLSIGN",
            Name = "Satellite Sign",
            SourceTable = "Product"
        };

        // No Category/TechnologyCategory -> never allowed in tech-only inventories.
        Assert.False(InventoryStackDatabase.CanAddItemToInventory(item, isTechOnly: true, isCargo: false));
        // Products are allowed in cargo.
        Assert.True(InventoryStackDatabase.CanAddItemToInventory(item, isTechOnly: false, isCargo: true));
        // Allowed in general inventories.
        Assert.True(InventoryStackDatabase.CanAddItemToInventory(item, isTechOnly: false, isCargo: false));
    }

    [Fact]
    public void StationItem_ResolvesAsProductInventoryType()
    {
        var item = new GameItem
        {
            ItemType = "Station",
            Id = "STA_78",
            Name = "Curious Fauna Display",
            SourceTable = "Product"
        };

        Assert.Equal("Product", InventoryStackDatabase.ResolveInventoryType("Station"));
        Assert.Equal("Product", InventoryStackDatabase.ResolveInventoryTypeForItem(item));
    }

    [Fact]
    public void StationItem_TrackedAsKnownProduct()
    {
        // Station items must be classified as products in the discovery logic
        // (like Corvette parts), so they are tracked in KnownProducts.
        Assert.Contains("Station", CatalogueLogic.ProductItemTypes);
    }

    [Fact]
    public void SwarmTrophyB_MovedToBuildings_IsBuildingAndSpecialShop()
    {
        string jsonPath = Path.Combine(FindResourceJsonDir() ?? "", "Buildings.json");
        if (!File.Exists(jsonPath)) return;

        var db = LoadDatabase();
        var item = db.GetItem("SWARM_TROPHY_B");
        if (item == null) return; // Skip until the extractor re-run moves it

        Assert.Equal("Buildings", item.ItemType);
        Assert.True(item.IsBuilding, "SWARM_TROPHY_B must be flagged as a building");
        Assert.Equal("SpecialShop", item.TradeCategory);
        Assert.False(item.CanPickUp);
    }

    [Fact]
    public void ShipSummon_MovedToBuildings()
    {
        string jsonPath = Path.Combine(FindResourceJsonDir() ?? "", "Buildings.json");
        if (!File.Exists(jsonPath)) return;

        var db = LoadDatabase();
        var item = db.GetItem("SHIPSUMMON");
        if (item == null) return; // Skip until the extractor re-run moves it

        Assert.Equal("Buildings", item.ItemType);
        Assert.True(item.IsBuilding);
    }
}