using System.Text.Json;
using NMSE.Extractor.Data;

namespace NMSE.Extractor.Tests;

public class CataloguePackBuilderTests
{
    [Fact]
    public void WriteCataloguePack_DerivesRecipesFossilsAndWordGroups()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"nmse_pack_builder_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var baseData = new Dictionary<string, List<Dictionary<string, object?>>>
            {
                ["Recipes"] =
                [
                    new() { ["Id"] = "RECIPE_1" },
                    new() { ["Id"] = "RECIPE_2" },
                ],
                ["ShipComponents"] =
                [
                    new() { ["Id"] = "FOS_HEAD_AA" },
                    new() { ["Id"] = "FIGHT_COCKAA" },
                ],
                ["Words"] =
                [
                    new()
                    {
                        ["Groups"] = new Dictionary<string, object?>
                        {
                            ["^TRA_A"] = 0,
                            ["^TRA_B"] = 4,
                        },
                    },
                ],
            };

            File.WriteAllText(Path.Combine(dir, "cataloguematerials.MXML"), """
<?xml version="1.0" encoding="utf-8"?>
<Data template="cGcWiki">
  <Property name="Categories">
    <Property name="Categories" value="GcWikiCategory" _index="0">
      <Property name="Items">
        <Property name="Items" value="MAT_A" _index="0" />
        <Property name="Items" value="MAT_B" _index="1" />
      </Property>
    </Property>
  </Property>
</Data>
""");

            CataloguePackBuilder.WriteCataloguePack(dir, baseData, dir);

            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "Catalogue Pack.json")));
            var root = document.RootElement;

            Assert.Equal(2, root.GetProperty("KnownRefinerRecipes").GetArrayLength());
            Assert.Equal(65535, root.GetProperty("KnownPortalRunes").GetInt32());

            var materials = root.GetProperty("CatalogueMaterials");
            Assert.Equal(2, materials.GetArrayLength());
            Assert.Equal("MAT_A", materials[0].GetString());
            Assert.Equal("MAT_B", materials[1].GetString());
            Assert.Equal(0, root.GetProperty("CatalogueBuilding").GetArrayLength());
            Assert.Equal(0, root.GetProperty("CatalogueCrafting").GetArrayLength());

            var fossils = root.GetProperty("Fossils");
            Assert.Equal(1, fossils.GetArrayLength());
            Assert.Equal("FOS_HEAD_AA", fossils[0].GetString());

            var groups = root.GetProperty("KnownWordGroups");
            Assert.Equal("^TRA_A", groups[0].GetProperty("Group").GetString());
            Assert.True(groups[0].GetProperty("Races")[0].GetBoolean());
            Assert.Equal("^TRA_B", groups[1].GetProperty("Group").GetString());
            Assert.True(groups[1].GetProperty("Races")[3].GetBoolean()); // Atlas ordinal 4 -> index 3
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }
}
