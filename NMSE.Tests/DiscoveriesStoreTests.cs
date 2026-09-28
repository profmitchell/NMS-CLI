using NMSE.Config;
using NMSE.Core;
using NMSE.Data;

namespace NMSE.Tests;

/// <summary>
/// Tests for the Discoveries data store and the POI layout cache migration away from
/// the application config file.
/// </summary>
public class DiscoveriesStoreTests : IDisposable
{
    private readonly string _dir;

    public DiscoveriesStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"nmse_discoveries_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
        DiscoveriesStore.SetDirectoryOverride(_dir);
    }

    public void Dispose()
    {
        DiscoveriesStore.SetDirectoryOverride(null);
        try { Directory.Delete(_dir, true); } catch { }
    }

    [Fact]
    public void SaveDirty_WritesAndReloads()
    {
        var file = DiscoveriesStore.Get("space-poi-layouts.json", AppJsonContext.Default.SpacePoiLayoutsFile);
        var tokens = Enumerable.Repeat("AsteroidBelt", 32).ToArray();
        file.Layouts["12345"] = tokens;
        DiscoveriesStore.MarkDirty(file);
        DiscoveriesStore.SaveDirty();

        string path = Path.Combine(_dir, "space-poi-layouts.json");
        Assert.True(File.Exists(path));

        DiscoveriesStore.SetDirectoryOverride(_dir);
        var reloaded = DiscoveriesStore.Get("space-poi-layouts.json", AppJsonContext.Default.SpacePoiLayoutsFile);
        Assert.Equal(tokens, reloaded.Layouts["12345"]);
        Assert.False(reloaded.Dirty);
    }

    [Fact]
    public void Load_CorruptFile_BacksUpAndStartsFresh()
    {
        File.WriteAllText(Path.Combine(_dir, "space-poi-layouts.json"), "{ not valid json");

        var file = DiscoveriesStore.Get("space-poi-layouts.json", AppJsonContext.Default.SpacePoiLayoutsFile);

        Assert.Empty(file.Layouts);
        Assert.NotEmpty(Directory.GetFiles(_dir, "space-poi-layouts.json.corrupt-*"));
    }

    [Fact]
    public void Load_NewerVersion_BacksUpAndStartsFresh()
    {
        File.WriteAllText(Path.Combine(_dir, "space-poi-layouts.json"),
            "{\"Version\": 99, \"Layouts\": {\"1\": [\"AsteroidBelt\"]}}");

        var file = DiscoveriesStore.Get("space-poi-layouts.json", AppJsonContext.Default.SpacePoiLayoutsFile);

        Assert.Empty(file.Layouts);
        var files = Directory.GetFiles(_dir).Select(Path.GetFileName).ToArray();
        Assert.True(files.Any(name => name!.Contains("newer-version", StringComparison.Ordinal)),
            "files: " + string.Join(", ", files));
    }

    [Fact]
    public void ImportLegacyLayouts_ImportsOnlyVersionedValidEntries()
    {
        var file = new SpacePoiLayoutsFile();
        string tokens = string.Join("|", Enumerable.Repeat("Hulk", 32));
        var properties = new List<KeyValuePair<string, string>>
        {
            new("spacepoi.layout.v2.555", tokens),
            new("spacepoi.layout.444", tokens),          // unversioned rule, ignored
            new("spacepoi.layout.v2.333", "too|short"),  // invalid token count, ignored
            new("unrelated.key", "value"),
        };

        int imported = SpacePoiLayoutCache.ImportLegacyLayouts(file, properties);

        Assert.Equal(1, imported);
        Assert.True(file.Layouts.ContainsKey("555"));
        Assert.False(file.Layouts.ContainsKey("444"));
        Assert.False(file.Layouts.ContainsKey("333"));
    }
}
