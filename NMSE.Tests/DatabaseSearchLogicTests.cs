using NMSE.Core;
using NMSE.Data;

namespace NMSE.Tests;

/// <summary>
/// Tests for <see cref="DatabaseSearchLogic"/>: query parsing, substring and
/// wildcard matching, and multi-field item matching.
/// </summary>
[Collection("MutableStaticDatabases")]
public class DatabaseSearchLogicTests
{
    private static GameItem MakeItem() => new()
    {
        Id = "CASING",
        Name = "Metal Plating",
        NameLower = "metal plating",
        Subtitle = "Crafted Technology Component",
        Description = "A lightweight metal product, used in the construction of starships.",
        Category = "Component",
        TechnologyCategory = "",
        ProductCategory = "Component",
        SubstanceCategory = "Catalyst",
        WikiCategory = "Crafting",
        ItemType = "Products",
        SourceTable = "Product",
        Rarity = "Common",
        Quality = "",
        MaxStackSize = 2,
        ChargeValue = 0,
        TradeCategory = "None",
        Symbol = "",
        Icon = "CASING.png"
    };

    // --- ParseQuery ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void ParseQuery_EmptyInput_ReturnsEmptyQuery(string? input)
    {
        var query = DatabaseSearchLogic.ParseQuery(input);

        Assert.True(query.IsEmpty);
        Assert.Empty(query.Terms);
    }

    [Fact]
    public void ParseQuery_SplitsOnWhitespace()
    {
        var query = DatabaseSearchLogic.ParseQuery("  metal   plating  ");

        Assert.False(query.IsEmpty);
        Assert.Equal(new[] { "metal", "plating" }, query.Terms);
    }

    // --- Matches (substring) ---

    [Theory]
    [InlineData("Metal Plating", "metal", true)]
    [InlineData("Metal Plating", "PLATING", true)]
    [InlineData("Metal Plating", "Metal Plating", true)]
    [InlineData("Metal Plating", "met", true)]
    [InlineData("Metal Plating", "gold", false)]
    [InlineData("Metal Plating", "", false)]
    [InlineData("", "metal", false)]
    public void Matches_NoWildcard_UsesCaseInsensitiveSubstring(string value, string term, bool expected)
    {
        Assert.Equal(expected, DatabaseSearchLogic.Matches(value, term));
    }

    // --- Matches (wildcards) ---

    [Theory]
    [InlineData("Metal Plating", "metal*", true)]
    [InlineData("Metal Plating", "METAL*", true)]
    [InlineData("Metal Plating", "*plating", true)]
    [InlineData("Metal Plating", "*plat*", true)]
    [InlineData("Metal Plating", "*m?tal*", true)]
    [InlineData("Metal Plating", "m?tal", false)]
    [InlineData("Metal Plating", "M?tal", false)]
    [InlineData("Metal Plating", "M?tal*", true)]
    [InlineData("Metal Plating", "*p?ating", true)]
    [InlineData("Metal Plating", "m*t*l*p*", true)]
    [InlineData("Metal Plating", "*", true)]
    [InlineData("Metal Plating", "?????????????", true)]
    [InlineData("Metal Plating", "????????????", false)]
    [InlineData("Metal Plating", "x*", false)]
    [InlineData("Metal Plating", "*x", false)]
    [InlineData("Metal Plating", "M?t?l", false)]
    [InlineData("", "*", false)]
    public void Matches_Wildcard_UsesWholeValueGlob(string value, string term, bool expected)
    {
        Assert.Equal(expected, DatabaseSearchLogic.Matches(value, term));
    }

    [Fact]
    public void GlobMatch_ConsecutiveStars_Backtracks()
    {
        Assert.True(DatabaseSearchLogic.GlobMatch("abcdef", "a**f"));
        Assert.True(DatabaseSearchLogic.GlobMatch("abcdef", "**c**"));
        Assert.False(DatabaseSearchLogic.GlobMatch("abcdef", "**g**"));
    }

    // --- MatchesItem ---

    [Fact]
    public void MatchesItem_EmptyQuery_MatchesEverything()
    {
        Assert.True(DatabaseSearchLogic.MatchesItem(MakeItem(), DatabaseSearchLogic.EmptyQuery));
    }

    [Theory]
    [InlineData("CASING")]
    [InlineData("casing")]
    [InlineData("Metal Plating")]
    [InlineData("lightweight")]
    [InlineData("Component")]
    [InlineData("Catalyst")]
    [InlineData("Crafting")]
    [InlineData("Product")]
    [InlineData("CASING.png")]
    [InlineData("2")]
    public void MatchesItem_MatchesAnyField(string term)
    {
        var query = DatabaseSearchLogic.ParseQuery(term);

        Assert.True(DatabaseSearchLogic.MatchesItem(MakeItem(), query));
    }

    [Fact]
    public void MatchesItem_AllTermsMustMatch()
    {
        var item = MakeItem();

        Assert.True(DatabaseSearchLogic.MatchesItem(item, DatabaseSearchLogic.ParseQuery("metal product")));
        Assert.False(DatabaseSearchLogic.MatchesItem(item, DatabaseSearchLogic.ParseQuery("metal gold")));
        Assert.False(DatabaseSearchLogic.MatchesItem(item, DatabaseSearchLogic.ParseQuery("plating* gold")));
    }

    [Fact]
    public void MatchesItem_NullAndEmptyFields_DoNotThrow()
    {
        var item = new GameItem { Id = "EMPTY", Name = "" };
        item.NameLocStr = null;
        item.DescriptionLocStr = null;

        Assert.True(DatabaseSearchLogic.MatchesItem(item, DatabaseSearchLogic.ParseQuery("empty")));
        Assert.False(DatabaseSearchLogic.MatchesItem(item, DatabaseSearchLogic.ParseQuery("missing")));
    }

    // --- GetDisplayCategory ---

    [Fact]
    public void GetDisplayCategory_TechnologyCategoryWins()
    {
        var item = new GameItem { TechnologyCategory = "Ship", Category = "Ship", Subtitle = "Hyperdrive" };

        Assert.Equal("Ship", DatabaseSearchLogic.GetDisplayCategory(item));
    }

    [Fact]
    public void GetDisplayCategory_UsesCategoryForSubstances()
    {
        var item = new GameItem { Category = "Fuel", Subtitle = "Fuel" };

        Assert.Equal("Fuel", DatabaseSearchLogic.GetDisplayCategory(item));
    }

    [Fact]
    public void GetDisplayCategory_FallsBackToGroupForProducts()
    {
        // Decals and other products have no Category in the JSON; Group is the
        // human-readable grouping shown on the item, e.g. "Decoration".
        var item = new GameItem { Subtitle = "Decoration", ProductCategory = "BuildingPart" };

        Assert.Equal("Decoration", DatabaseSearchLogic.GetDisplayCategory(item));
    }

    [Fact]
    public void GetDisplayCategory_FallsBackToProductCategoryWithoutGroup()
    {
        var item = new GameItem { ProductCategory = "Consumable", SubstanceCategory = "Fuel" };

        Assert.Equal("Consumable", DatabaseSearchLogic.GetDisplayCategory(item));
    }

    [Fact]
    public void GetDisplayCategory_FallsBackToSubstanceCategory()
    {
        var item = new GameItem { SubstanceCategory = "Metal" };

        Assert.Equal("Metal", DatabaseSearchLogic.GetDisplayCategory(item));
    }

    [Theory]
    [InlineData("None")]
    [InlineData("")]
    public void GetDisplayCategory_IgnoresNoneAndEmpty(string value)
    {
        var item = new GameItem { Category = value, Subtitle = value, ProductCategory = value, SubstanceCategory = value };

        Assert.Equal("", DatabaseSearchLogic.GetDisplayCategory(item));
    }

    // --- Integration with the real bundled database ---

    private static string? FindResourceJsonDir()
    {
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

    [Fact]
    public void DatabaseSearchLogic_RealDatabase_FindsKnownItem()
    {
        var jsonDir = FindResourceJsonDir();
        if (jsonDir == null) return; // Skip when the working directory does not contain Resources

        var db = new GameItemDatabase();
        db.LoadItemsFromJsonDirectory(jsonDir);

        var byId = DatabaseSearchLogic.ParseQuery("casing");
        var idMatches = db.Items.Values
            .Where(item => DatabaseSearchLogic.MatchesItem(item, byId))
            .ToList();
        Assert.Contains(idMatches, item => item.Id == "CASING");

        var byWildcard = DatabaseSearchLogic.ParseQuery("*plating*");
        var wildcardMatches = db.Items.Values
            .Where(item => DatabaseSearchLogic.MatchesItem(item, byWildcard))
            .ToList();
        Assert.Contains(wildcardMatches, item => item.Id == "CASING");
    }

    [Fact]
    public void DatabaseSearchLogic_RealDatabase_ProductItemsHaveDisplayCategory()
    {
        var jsonDir = FindResourceJsonDir();
        if (jsonDir == null) return; // Skip when the working directory does not contain Resources

        var db = new GameItemDatabase();
        db.LoadItemsFromJsonDirectory(jsonDir);

        // Decals are products with no raw Category; they must still resolve to a
        // human-readable category (their Group, "Decoration") instead of blank.
        var decal = db.Items.Values.FirstOrDefault(item => item.Id == "EXPD_DECAL01");
        if (decal == null) return;

        Assert.Equal("BuildingPart", decal.ProductCategory);
        Assert.Equal("Decoration", DatabaseSearchLogic.GetDisplayCategory(decal));
    }
}
