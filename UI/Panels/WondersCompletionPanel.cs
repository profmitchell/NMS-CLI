using System.Globalization;
using NMSE.Core;
using NMSE.Data;
using NMSE.Models;
using NMSE.UI.Controls;

namespace NMSE.UI.Panels;

/// <summary>Wonders sub-tab: completes empty wonder slots from the verified packs.</summary>
internal sealed class WondersCompletionPanel : CompletionGridPanel
{
    private readonly record struct WonderRow(string Key, int Index);

    private static readonly Dictionary<string, string> GroupNames = new(StringComparer.Ordinal)
    {
        ["WonderPlanetRecords"] = "Planet",
        ["WonderCreatureRecords"] = "Creature",
        ["WonderFloraRecords"] = "Flora",
        ["WonderMineralRecords"] = "Mineral",
        ["WonderTreasureRecords"] = "Treasure",
        ["WonderWeirdBasePartRecords"] = "Weird Base Part",
    };

    // Pre-fill slot values recorded so a later untick can restore the exact original
    // shape (and the original array length for slots that were appended).
    private readonly Dictionary<(string Key, int Index), JsonObject?> _preFillSlots = [];
    private readonly Dictionary<string, int> _originalLengths = new(StringComparer.Ordinal);
    private JsonObject? _originalLengthsFor;

    public WondersCompletionPanel()
    {
        Grid.Columns.Add(CreateCheckColumn());
        Grid.Columns.Add(CreateTextColumn("Group", UiStrings.Get("discovery.col_category"), 14));
        Grid.Columns.Add(CreateTextColumn("Slot", UiStrings.Get("discovery.col_slot"), 14));
        Grid.Columns.Add(CreateTextColumn("Seed", UiStrings.Get("discovery.col_seed"), 38));
        Grid.Columns.Add(CreateTextColumn("Value", UiStrings.Get("discovery.col_value"), 12));
        Grid.Columns.Add(CreateTextColumn("Status", UiStrings.Get("discovery.col_status"), 22));
    }

    /// <inheritdoc/>
    public override void ApplyUiLocalisation()
    {
        base.ApplyUiLocalisation();
        if (Grid.Columns["Group"] is DataGridViewColumn group) group.HeaderText = UiStrings.Get("discovery.col_category");
        if (Grid.Columns["Slot"] is DataGridViewColumn slot) slot.HeaderText = UiStrings.Get("discovery.col_slot");
        if (Grid.Columns["Seed"] is DataGridViewColumn seed) seed.HeaderText = UiStrings.Get("discovery.col_seed");
        if (Grid.Columns["Value"] is DataGridViewColumn value) value.HeaderText = UiStrings.Get("discovery.col_value");
        if (Grid.Columns["Status"] is DataGridViewColumn status) status.HeaderText = UiStrings.Get("discovery.col_status");
    }

    /// <inheritdoc/>
    public override void Reload()
    {
        CaptureOriginalState();
        base.Reload();
    }

    /// <inheritdoc/>
    public override void PurgeData()
    {
        base.PurgeData();
        _preFillSlots.Clear();
        _originalLengths.Clear();
        _originalLengthsFor = null;
    }

    private void CaptureOriginalState()
    {
        if (PlayerState == null || ReferenceEquals(PlayerState, _originalLengthsFor)) return;

        _originalLengthsFor = PlayerState;
        _originalLengths.Clear();
        _preFillSlots.Clear();
        foreach (string key in CatalogueCompletionLogic.WonderKeys)
        {
            var slots = PlayerState.GetArray(key);
            if (slots != null) _originalLengths[key] = slots.Length;
        }
    }

    private IReadOnlyList<JsonObject>? PackRecords(string wonderKey) => wonderKey switch
    {
        "WonderTreasureRecords" => Catalogue?.WonderTreasureRecords,
        "WonderWeirdBasePartRecords" => Catalogue?.WonderWeirdBasePartRecords,
        _ => null,
    };

    /// <summary>
    /// Injects the DiscoveryManager records, PLANET_STATS rows and discovery owners
    /// required by filled discovery slots. Called before the save is written.
    /// </summary>
    internal int InjectWonderDependencies(JsonObject saveRoot)
    {
        if (Catalogue == null || PlayerState == null) return 0;
        return CatalogueCompletionLogic.InjectWonderDependencies(saveRoot, PlayerState, Catalogue.WonderFallbackSlots);
    }

    protected override void PopulateRows()
    {
        if (Catalogue == null || PlayerState == null) return;

        foreach (string key in CatalogueCompletionLogic.WonderKeys)
        {
            var packRecords = PackRecords(key);
            var live = PlayerState.GetArray(key);
            var (_, total) = CatalogueCompletionLogic.GetWonderCompletion(PlayerState, key, packRecords, Catalogue.WonderFallbackSlots);

            for (int i = 0; i < total; i++)
            {
                var record = live != null && i < live.Length ? live.GetObject(i) : null;
                bool filled = CatalogueCompletionLogic.IsWonderSlotFilled(record);
                var (seed, value) = FormatEntry(record);

                var row = new DataGridViewRow();
                row.CreateCells(Grid,
                    filled,
                    GroupNames.GetValueOrDefault(key, key),
                    UiStrings.Format("discovery.wonders_slot", i + 1),
                    seed,
                    value,
                    filled ? UiStrings.Get("discovery.status_complete") : UiStrings.Get("discovery.status_missing"));
                row.Tag = new WonderRow(key, i);
                Grid.Rows.Add(row);
            }
        }
    }

    private static (string Seed, string Value) FormatEntry(JsonObject? record)
    {
        if (record == null || !CatalogueCompletionLogic.IsWonderSlotFilled(record))
            return ("-", "-");

        var gid = record.GetArray("GenerationID");
        string seed = gid is { Length: >= 2 }
            ? $"{FormatSeed(gid.Get(0))} / {FormatSeed(gid.Get(1))}"
            : "-";
        string value = record.Get("WonderStatValue") is { } stat ? FormatStat(stat) : "-";
        return (seed, value);
    }

    private static string FormatSeed(object? value) => value switch
    {
        null => "-",
        string s => string.IsNullOrEmpty(s) ? "-" : s,
        int i => i.ToString(CultureInfo.InvariantCulture),
        long l => l.ToString(CultureInfo.InvariantCulture),
        double d => d.ToString("0.###", CultureInfo.InvariantCulture),
        float f => f.ToString("0.###", CultureInfo.InvariantCulture),
        RawDouble rd => rd.Value.ToString("0.###", CultureInfo.InvariantCulture),
        _ => "-",
    };

    private static string FormatStat(object? value) => value switch
    {
        null => "-",
        string s => s,
        int i => i.ToString(CultureInfo.InvariantCulture),
        long l => l.ToString(CultureInfo.InvariantCulture),
        double d => d.ToString("0.###", CultureInfo.InvariantCulture),
        float f => f.ToString("0.###", CultureInfo.InvariantCulture),
        RawDouble rd => rd.Value.ToString("0.###", CultureInfo.InvariantCulture),
        _ => "-",
    };

    protected override (int Have, int Total) GetCompletion()
    {
        if (Catalogue == null || PlayerState == null) return (0, 0);

        int have = 0, total = 0;
        foreach (string key in CatalogueCompletionLogic.WonderKeys)
        {
            var (keyHave, keyTotal) = CatalogueCompletionLogic.GetWonderCompletion(
                PlayerState, key, PackRecords(key), Catalogue.WonderFallbackSlots);
            have += keyHave;
            total += keyTotal;
        }
        return (have, total);
    }

    protected override bool ToggleRow(DataGridViewRow row, bool complete)
    {
        if (row.Tag is not WonderRow info || PlayerState == null || Catalogue == null || SaveRoot == null) return false;

        if (complete)
        {
            var recordKey = (info.Key, info.Index);
            JsonObject? original = null;
            if (!_preFillSlots.ContainsKey(recordKey))
            {
                var slots = PlayerState.GetArray(info.Key);
                if (slots != null && info.Index < slots.Length)
                    original = slots.GetObject(info.Index)?.DeepClone();
            }

            bool filled = CatalogueCompletionLogic.FillWonderSlot(
                SaveRoot, PlayerState, info.Key, info.Index, PackRecords(info.Key), Catalogue.WonderFallbackSlots);
            if (filled && !_preFillSlots.ContainsKey(recordKey))
                _preFillSlots[recordKey] = original;
            return filled;
        }

        var restoreKey = (info.Key, info.Index);
        bool changed = _preFillSlots.Remove(restoreKey, out var preFill)
            ? RestoreSlot(info.Key, info.Index, preFill)
            : CatalogueCompletionLogic.ClearWonderSlot(PlayerState, info.Key, info.Index);
        TrimTrailingPlaceholders(info.Key);
        return changed;
    }

    protected override int CompleteAll()
    {
        if (Catalogue == null || PlayerState == null || SaveRoot == null) return 0;

        // Record the pre-fill values so individual untick can restore them exactly.
        foreach (string key in CatalogueCompletionLogic.WonderKeys)
        {
            var slots = PlayerState.GetArray(key);
            var packRecords = PackRecords(key);
            var (_, total) = CatalogueCompletionLogic.GetWonderCompletion(PlayerState, key, packRecords, Catalogue.WonderFallbackSlots);
            for (int i = 0; i < total; i++)
            {
                if (slots != null && i < slots.Length && CatalogueCompletionLogic.IsWonderSlotFilled(slots.GetObject(i))) continue;
                var recordKey = (key, i);
                if (_preFillSlots.ContainsKey(recordKey)) continue;
                _preFillSlots[recordKey] = slots != null && i < slots.Length ? slots.GetObject(i)?.DeepClone() : null;
            }
        }

        int changed = 0;
        foreach (string key in CatalogueCompletionLogic.WonderKeys)
        {
            changed += CatalogueCompletionLogic.FillWonderSlots(
                SaveRoot, PlayerState, key, PackRecords(key), Catalogue.WonderFallbackSlots);
        }
        return changed;
    }

    protected override int ClearAll()
    {
        if (PlayerState == null) return 0;

        int changed = 0;
        foreach (var ((key, index), original) in _preFillSlots.ToList())
        {
            _preFillSlots.Remove((key, index));
            if (RestoreSlot(key, index, original)) changed++;
        }

        foreach (string key in CatalogueCompletionLogic.WonderKeys)
        {
            var slots = PlayerState.GetArray(key);
            if (slots == null) continue;
            for (int i = 0; i < slots.Length; i++)
            {
                if (CatalogueCompletionLogic.IsWonderSlotFilled(slots.GetObject(i))
                    && CatalogueCompletionLogic.ClearWonderSlot(PlayerState, key, i))
                {
                    changed++;
                }
            }
            TrimTrailingPlaceholders(key);
        }
        return changed;
    }

    private bool RestoreSlot(string wonderKey, int index, JsonObject? original)
    {
        var slots = PlayerState!.GetArray(wonderKey);
        if (slots == null || index < 0 || index >= slots.Length) return false;

        if (original != null)
        {
            slots.Set(index, original.DeepClone());
            return true;
        }

        return CatalogueCompletionLogic.ClearWonderSlot(PlayerState, wonderKey, index);
    }

    /// <summary>
    /// Removes empty slots appended when a fill extended the array beyond its
    /// original length, so a fill/clear round trip restores the exact save shape.
    /// </summary>
    private void TrimTrailingPlaceholders(string wonderKey)
    {
        var slots = PlayerState?.GetArray(wonderKey);
        if (slots == null) return;

        _originalLengths.TryGetValue(wonderKey, out int originalLength);
        while (slots.Length > originalLength)
        {
            if (CatalogueCompletionLogic.IsWonderSlotFilled(slots.GetObject(slots.Length - 1))) break;
            slots.RemoveAt(slots.Length - 1);
        }

        if (!_originalLengths.ContainsKey(wonderKey) && slots.Length == 0)
            PlayerState!.Remove(wonderKey);
    }
}
