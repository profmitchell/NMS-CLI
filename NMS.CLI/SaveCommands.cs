using System.Globalization;
using NMSE.IO;

namespace NMS.CLI;

public static class SaveCommands
{
    private sealed record SaveVariant(string Kind, string Path, int? MemoryDatSlotIndex = null);

    private sealed record SaveSlotSummary(
        int Slot,
        string Name,
        string Difficulty,
        bool IsExpedition,
        DateTimeOffset? LastModified,
        IReadOnlyList<SaveVariant> Variants);

    public static int List(string? directory)
    {
        if (!TryResolveDirectory(directory, out string saveDir, out SaveFileManager.Platform platform, out string error))
        {
            Console.Error.WriteLine(error);
            return 1;
        }

        var slots = GetSaveSlots(saveDir, platform);
        Console.WriteLine($"Directory: {saveDir}");
        Console.WriteLine($"Platform: {platform}");

        if (slots.Count == 0)
        {
            Console.WriteLine("No save slots found.");
            return 0;
        }

        foreach (var slot in slots)
        {
            var meta = new List<string>();
            if (!string.IsNullOrEmpty(slot.Name)) meta.Add(slot.Name);
            if (!string.IsNullOrEmpty(slot.Difficulty)) meta.Add(slot.Difficulty);
            if (slot.IsExpedition) meta.Add("EXPEDITION");
            string suffix = meta.Count == 0 ? "" : $" - {string.Join(" - ", meta)}";
            string timestamp = slot.LastModified.HasValue
                ? $" [{slot.LastModified.Value.LocalDateTime:yyyy-MM-dd HH:mm}]"
                : "";

            Console.WriteLine($"Slot {slot.Slot}{suffix}{timestamp}");
            foreach (var variant in slot.Variants)
                Console.WriteLine($"  {variant.Kind}: {variant.Path}");
        }

        return 0;
    }

    public static int Info(string? directory, int slotNumber)
    {
        if (!TryResolveDirectory(directory, out string saveDir, out SaveFileManager.Platform platform, out string error))
        {
            Console.Error.WriteLine(error);
            return 1;
        }

        var slot = GetSaveSlots(saveDir, platform).FirstOrDefault(s => s.Slot == slotNumber);
        if (slot is null)
        {
            Console.Error.WriteLine($"Slot {slotNumber} was not found in '{saveDir}'.");
            return 1;
        }

        Console.WriteLine($"Directory: {saveDir}");
        Console.WriteLine($"Platform: {platform}");
        Console.WriteLine($"Slot: {slot.Slot}");
        Console.WriteLine($"Name: {slot.Name}");
        Console.WriteLine($"Difficulty: {slot.Difficulty}");
        Console.WriteLine($"Expedition: {(slot.IsExpedition ? "yes" : "no")}");
        Console.WriteLine($"Last Modified: {(slot.LastModified.HasValue ? slot.LastModified.Value.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) : "n/a")}");

        foreach (var variant in slot.Variants)
        {
            string extra = variant.MemoryDatSlotIndex.HasValue
                ? $" (memory.dat sub-slot {variant.MemoryDatSlotIndex.Value})"
                : "";
            Console.WriteLine($"{variant.Kind}: {variant.Path}{extra}");
        }

        return 0;
    }

    private static bool TryResolveDirectory(string? directory, out string saveDir, out SaveFileManager.Platform platform, out string error)
    {
        saveDir = directory ?? SaveFileManager.FindDefaultSaveDirectory() ?? "";
        if (string.IsNullOrWhiteSpace(saveDir))
        {
            error = "Could not locate a default save directory. Use --dir <path>.";
            platform = SaveFileManager.Platform.Unknown;
            return false;
        }

        if (!Directory.Exists(saveDir))
        {
            error = $"Save directory does not exist: {saveDir}";
            platform = SaveFileManager.Platform.Unknown;
            return false;
        }

        platform = SaveFileManager.DetectPlatform(saveDir);
        if (platform == SaveFileManager.Platform.Unknown)
        {
            error = $"Could not detect a supported save platform in: {saveDir}";
            return false;
        }

        error = "";
        return true;
    }

    private static List<SaveSlotSummary> GetSaveSlots(string saveDir, SaveFileManager.Platform platform)
    {
        return platform switch
        {
            SaveFileManager.Platform.XboxGamePass => GetXboxSlots(saveDir),
            SaveFileManager.Platform.PS4 when File.Exists(Path.Combine(saveDir, "memory.dat"))
                && Directory.GetFiles(saveDir, "savedata*.hg").Length == 0 => GetMemoryDatSlots(saveDir),
            SaveFileManager.Platform.Steam or SaveFileManager.Platform.GOG or SaveFileManager.Platform.PS4 or SaveFileManager.Platform.Switch
                => GetTwoFileSlots(saveDir, platform),
            _ => []
        };
    }

    private static List<SaveSlotSummary> GetTwoFileSlots(string saveDir, SaveFileManager.Platform platform)
    {
        var slots = new List<SaveSlotSummary>();

        for (int i = 0; i < 15; i++)
        {
            var pair = SaveSlotManager.GetAllSlotFiles(saveDir, i, platform);
            var variants = new List<SaveVariant>();
            foreach (var (slotFiles, index) in pair.Select((p, idx) => (p, idx)))
            {
                if (!string.IsNullOrEmpty(slotFiles.DataFile) && File.Exists(slotFiles.DataFile))
                {
                    variants.Add(new SaveVariant(index == 0 ? "Auto" : "Manual", slotFiles.DataFile));
                }
            }

            if (variants.Count == 0)
                continue;

            var selected = variants.Last();
            (string name, string difficulty, bool isExp) = ReadFileSummary(selected.Path);
            DateTimeOffset? lastModified = ReadNewestLastModified(variants.Select(v => v.Path));

            slots.Add(new SaveSlotSummary(i + 1, name, difficulty, isExp, lastModified, variants));
        }

        return slots;
    }

    private static List<SaveSlotSummary> GetXboxSlots(string saveDir)
    {
        var result = new List<SaveSlotSummary>();
        string indexPath = Path.Combine(saveDir, "containers.index");
        if (!File.Exists(indexPath)) return result;

        var all = ContainersIndexManager.ParseContainersIndex(indexPath);
        var grouped = all
            .Where(kvp => ContainersIndexManager.IsSaveSlot(kvp.Key))
            .Select(kvp => new { Id = kvp.Key, Slot = ContainersIndexManager.ExtractSlotNumber(kvp.Key), Info = kvp.Value })
            .Where(x => x.Slot > 0)
            .GroupBy(x => x.Slot)
            .OrderBy(g => g.Key);

        foreach (var group in grouped)
        {
            var variants = group
                .OrderBy(x => ContainersIndexManager.IsAutoSave(x.Id) ? 0 : 1)
                .Where(x => !string.IsNullOrEmpty(x.Info.DataFilePath) && File.Exists(x.Info.DataFilePath))
                .Select(x => new SaveVariant(ContainersIndexManager.IsAutoSave(x.Id) ? "Auto" : "Manual", x.Info.DataFilePath!))
                .ToList();

            if (variants.Count == 0)
                continue;

            var selected = variants.Last();
            (string name, string difficulty, bool isExp) = ReadFileSummary(selected.Path);
            DateTimeOffset? lastModified = ReadNewestLastModified(variants.Select(v => v.Path));

            result.Add(new SaveSlotSummary(group.Key, name, difficulty, isExp, lastModified, variants));
        }

        return result;
    }

    private static List<SaveSlotSummary> GetMemoryDatSlots(string saveDir)
    {
        var result = new List<SaveSlotSummary>();
        string memoryDat = Path.Combine(saveDir, "memory.dat");
        if (!File.Exists(memoryDat)) return result;

        var slots = MemoryDatManager.ReadSlots(memoryDat);
        for (int n = 1; n <= 5; n++)
        {
            int autoIdx = n * 2 - 1;
            int manualIdx = n * 2;
            var variants = new List<SaveVariant>();
            if (autoIdx < slots.Length && slots[autoIdx].Exists) variants.Add(new SaveVariant("Auto", memoryDat, autoIdx));
            if (manualIdx < slots.Length && slots[manualIdx].Exists) variants.Add(new SaveVariant("Manual", memoryDat, manualIdx));
            if (variants.Count == 0) continue;

            var selected = variants.Last();
            string json = MemoryDatManager.ExtractSlotData(memoryDat, selected.MemoryDatSlotIndex!.Value) ?? "";
            string name = SaveFileManager.DetectSaveNameFromJson(json);
            string difficulty = GameModeToString(SaveFileManager.DetectGameModeFromJson(json));
            SaveFileManager.DetectActiveContextFromJson(json, out bool isExpedition);

            DateTimeOffset? lastModified = variants
                .Select(v => v.MemoryDatSlotIndex.HasValue ? slots[v.MemoryDatSlotIndex.Value].Timestamp : null)
                .Where(ts => ts.HasValue)
                .Select(ts => ts!.Value)
                .DefaultIfEmpty()
                .Max();

            result.Add(new SaveSlotSummary(n, name, difficulty, isExpedition, lastModified, variants));
        }

        return result;
    }

    private static (string Name, string Difficulty, bool IsExpedition) ReadFileSummary(string filePath)
    {
        string name = SaveFileManager.DetectSaveNameFast(filePath);
        string difficulty = GameModeToString(SaveFileManager.DetectGameModeFast(filePath));
        SaveFileManager.DetectActiveContextFast(filePath, out bool expedition);
        return (name, difficulty, expedition);
    }

    private static DateTimeOffset? ReadNewestLastModified(IEnumerable<string> paths)
    {
        DateTimeOffset? newest = null;
        foreach (var path in paths)
        {
            if (!File.Exists(path)) continue;
            DateTimeOffset stamp = File.GetLastWriteTimeUtc(path);
            if (!newest.HasValue || stamp > newest.Value) newest = stamp;
        }

        return newest;
    }

    private static string GameModeToString(int mode)
    {
        return mode switch
        {
            1 => "NORMAL",
            2 => "SURVIVAL",
            3 => "PERMADEATH",
            4 => "CREATIVE",
            5 => "CUSTOM",
            6 => "SEASONAL",
            7 => "RELAXED",
            8 => "HARDCORE",
            _ => mode > 0 ? $"MODE {mode}" : "",
        };
    }
}
