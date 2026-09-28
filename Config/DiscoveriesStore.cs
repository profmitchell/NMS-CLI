using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using NMSE.Core;

namespace NMSE.Config;

/// <summary>
/// Loads and saves the editor's long-term data files from the Discoveries folder next
/// to the application executable. Each data set is a separate JSON file with its own
/// format version, so features such as the space POI layout memory, and future
/// cross-save libraries (discoveries, ships, multitools, ...) can evolve independently.
/// Writes are atomic (temporary file then replace) and unreadable files are backed up
/// rather than deleted.
/// </summary>
internal static class DiscoveriesStore
{
    /// <summary>Folder name used next to the application executable.</summary>
    public const string FolderName = "Discoveries";

    private static readonly object Sync = new();
    private static readonly Dictionary<string, object> Loaded = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Action> Savers = new(StringComparer.OrdinalIgnoreCase);
    private static string? _directoryOverride;

    /// <summary>Directory holding the data files.</summary>
    public static string DataDirectory =>
        _directoryOverride ?? Path.Combine(AppContext.BaseDirectory, FolderName);

    /// <summary>
    /// Overrides the data folder and forgets all cached files. Pass null to restore the
    /// default location. Used by tests.
    /// </summary>
    public static void SetDirectoryOverride(string? directory)
    {
        lock (Sync)
        {
            _directoryOverride = directory;
            Loaded.Clear();
            Savers.Clear();
        }
    }

    /// <summary>
    /// Returns the data file, loading it from disk on first use. The same instance is
    /// returned for later calls.
    /// </summary>
    /// <param name="fileName">File name inside the Discoveries folder.</param>
    /// <param name="typeInfo">Source-generated serializer metadata for the file type.</param>
    public static T Get<T>(string fileName, JsonTypeInfo<T> typeInfo)
        where T : DiscoveriesFile, new()
    {
        lock (Sync)
        {
            if (Loaded.TryGetValue(fileName, out object? existing) && existing is T typed)
                return typed;

            var file = LoadFromDisk(fileName, typeInfo);
            Loaded[fileName] = file;
            Savers[fileName] = () => SaveToDisk(fileName, file, typeInfo);
            return file;
        }
    }

    /// <summary>Marks a data file as modified; it is written on the next <see cref="SaveDirty"/>.</summary>
    public static void MarkDirty(DiscoveriesFile file) => file.Dirty = true;

    /// <summary>Writes every modified data file.</summary>
    public static void SaveDirty()
    {
        List<(Action Save, DiscoveriesFile File)> pending = new();
        lock (Sync)
        {
            foreach (var (fileName, save) in Savers)
            {
                if (Loaded.TryGetValue(fileName, out object? loaded) && loaded is DiscoveriesFile file && file.Dirty)
                    pending.Add((save, file));
            }
        }

        foreach (var (save, file) in pending)
        {
            save();
            file.Dirty = false;
        }
    }

    private static T LoadFromDisk<T>(string fileName, JsonTypeInfo<T> typeInfo)
        where T : DiscoveriesFile, new()
    {
        string path = Path.Combine(DataDirectory, fileName);
        if (!File.Exists(path)) return new T();

        T? file = null;
        try
        {
            // The stream must be closed before a backup rename can succeed.
            using (var stream = File.OpenRead(path))
                file = JsonSerializer.Deserialize(stream, typeInfo);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to read {fileName}: {ex.Message}");
            BackupFile(path, "corrupt");
            return new T();
        }

        if (file == null) return new T();

        if (file.Version > new T().Version)
        {
            // Written by a newer editor version: keep a copy and start fresh
            // rather than misreading it.
            BackupFile(path, "newer-version");
            return new T();
        }

        file.Dirty = false;
        return file;
    }

    private static void SaveToDisk<T>(string fileName, T file, JsonTypeInfo<T> typeInfo)
        where T : DiscoveriesFile
    {
        try
        {
            string directory = DataDirectory;
            Directory.CreateDirectory(directory);

            string path = Path.Combine(directory, fileName);
            string temp = path + ".tmp";
            using (var stream = File.Create(temp))
                JsonSerializer.Serialize(stream, file, typeInfo);
            File.Move(temp, path, overwrite: true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save {fileName}: {ex.Message}");
        }
    }

    private static void BackupFile(string path, string reason)
    {
        try
        {
            string stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
            File.Move(path, $"{path}.{reason}-{stamp}", overwrite: true);
        }
        catch
        {
            // Backup is best effort; a fresh file is created either way.
        }
    }
}
