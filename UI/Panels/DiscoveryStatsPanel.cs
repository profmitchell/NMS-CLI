using NMSE.Core;
using NMSE.Data;
using NMSE.UI.Controls;

namespace NMSE.UI.Panels;

/// <summary>Discovery Stats sub-tab: DISC_* and FISH_* GLOBAL stat targets.</summary>
internal sealed class DiscoveryStatsPanel : CompletionGridPanel
{
    private readonly record struct StatRow(string Id, int Target);

    private IReadOnlyList<KeyValuePair<string, int>> _targets = [];

    public DiscoveryStatsPanel()
    {
        Grid.Columns.Add(CreateCheckColumn());
        Grid.Columns.Add(CreateTextColumn("Id", UiStrings.Get("discovery.col_id"), 30));
        var currentColumn = CreateTextColumn("Current", UiStrings.Get("discovery.col_progress"), 15);
        currentColumn.ReadOnly = false;
        Grid.Columns.Add(currentColumn);
        Grid.Columns.Add(CreateTextColumn("Target", UiStrings.Get("discovery.col_target"), 15));
        Grid.Columns.Add(CreateTextColumn("Status", UiStrings.Get("discovery.col_status"), 40));
    }

    /// <inheritdoc/>
    public override void ApplyUiLocalisation()
    {
        base.ApplyUiLocalisation();
        if (Grid.Columns["Id"] is DataGridViewColumn id) id.HeaderText = UiStrings.Get("discovery.col_id");
        if (Grid.Columns["Current"] is DataGridViewColumn current) current.HeaderText = UiStrings.Get("discovery.col_progress");
        if (Grid.Columns["Target"] is DataGridViewColumn target) target.HeaderText = UiStrings.Get("discovery.col_target");
        if (Grid.Columns["Status"] is DataGridViewColumn status) status.HeaderText = UiStrings.Get("discovery.col_status");
    }

    protected override void PopulateRows()
    {
        _targets = [];
        if (Catalogue == null || PlayerState == null) return;

        _targets = Catalogue.DiscoveryStats.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase).ToList();
        var globals = CatalogueCompletionLogic.GetGlobalStatsMap(PlayerState);

        foreach (var (id, target) in _targets)
        {
            int? value = globals.TryGetValue(id, out var stat) ? CatalogueCompletionLogic.GetGlobalInt(stat) : null;
            bool complete = CatalogueCompletionLogic.IsGlobalStatComplete(value, target);

            var row = new DataGridViewRow();
            row.CreateCells(Grid,
                complete,
                id,
                value is int v ? v.ToString(System.Globalization.CultureInfo.InvariantCulture) : "0",
                target,
                complete ? UiStrings.Get("discovery.status_complete") : UiStrings.Get("discovery.status_missing"));
            row.Tag = new StatRow(id, target);
            Grid.Rows.Add(row);
            ApplyProgressEditability(row);
        }
    }

    protected override (int Min, int Max)? GetProgressRange(DataGridViewRow row)
    {
        if (row.Tag is not StatRow info || PlayerState == null) return null;

        var globals = CatalogueCompletionLogic.GetGlobalStatsMap(PlayerState);
        int current = globals.TryGetValue(info.Id, out var stat)
            ? Math.Max(0, CatalogueCompletionLogic.GetGlobalInt(stat))
            : 0;
        return (0, Math.Max(info.Target, current));
    }

    protected override bool TryApplyProgress(DataGridViewRow row, int value)
    {
        if (row.Tag is not StatRow info || PlayerState == null) return false;
        return CatalogueCompletionLogic.SetGlobalStatValue(PlayerState, info.Id, value);
    }

    protected override (int Have, int Total) GetCompletion()
    {
        if (Catalogue == null || PlayerState == null) return (0, 0);
        return CatalogueCompletionLogic.GetGlobalStatsCompletion(PlayerState, Catalogue.DiscoveryStats);
    }

    protected override bool ToggleRow(DataGridViewRow row, bool complete)
    {
        if (row.Tag is not StatRow info || PlayerState == null) return false;

        return complete
            ? CatalogueCompletionLogic.SetGlobalStatAtLeast(PlayerState, info.Id, info.Target)
            : CatalogueCompletionLogic.ClearGlobalStat(PlayerState, info.Id);
    }

    protected override int CompleteAll()
    {
        if (PlayerState == null) return 0;

        int changed = 0;
        foreach (var (id, target) in _targets)
        {
            if (CatalogueCompletionLogic.SetGlobalStatAtLeast(PlayerState, id, target)) changed++;
        }
        return changed;
    }

    protected override int ClearAll()
    {
        if (PlayerState == null) return 0;

        int changed = 0;
        foreach (var (id, _) in _targets)
        {
            if (CatalogueCompletionLogic.ClearGlobalStat(PlayerState, id)) changed++;
        }
        return changed;
    }
}
