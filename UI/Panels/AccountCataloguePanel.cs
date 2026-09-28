using NMSE.Core;
using NMSE.Data;
using NMSE.Models;
using NMSE.UI.Controls;

namespace NMSE.UI.Panels;

/// <summary>
/// Account Catalogue sub-tab: completes account-level Seen* arrays (fossil
/// SeenProducts or raw material SeenSubstances) from the catalogue database.
/// </summary>
internal sealed class AccountCataloguePanel : CompletionGridPanel
{
    private readonly record struct SeenRow(string Id);

    private readonly Label _messageLabel;
    private readonly bool _fossils;
    private JsonObject? _userSettings;

    /// <summary>Creates the Fossils or Raw Materials account catalogue panel.</summary>
    /// <param name="fossils">True for fossil SeenProducts, false for SeenSubstances.</param>
    public AccountCataloguePanel(bool fossils)
    {
        _fossils = fossils;

        Grid.Columns.Add(CreateCheckColumn());
        var iconColumn = new DataGridViewImageColumn
        {
            Name = "Icon",
            HeaderText = string.Empty,
            Width = 36,
            ImageLayout = DataGridViewImageCellLayout.Zoom,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter },
        };
        Grid.Columns.Add(iconColumn);
        Grid.Columns.Add(CreateTextColumn("Name", UiStrings.Get("discovery.col_name"), 50));
        Grid.Columns.Add(CreateTextColumn("Id", UiStrings.Get("discovery.col_id"), 24));
        Grid.Columns.Add(CreateTextColumn("Status", UiStrings.Get("discovery.col_status"), 26));

        _messageLabel = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = 30,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(6, 0, 6, 0),
            Visible = false,
        };
        Controls.Add(_messageLabel);
    }

    private string ArrayName => _fossils ? "SeenProducts" : "SeenSubstances";

    private IReadOnlyList<string> PackIds => _fossils
        ? Catalogue?.Fossils ?? []
        : Catalogue?.SeenSubstances ?? [];

    /// <summary>Sets the loaded account data (UserSettingsData is located automatically).</summary>
    public void SetAccountData(JsonObject? accountData)
    {
        _userSettings = accountData?.GetObject("UserSettingsData") ?? accountData;
        Reload();
    }

    /// <inheritdoc/>
    public override void ApplyUiLocalisation()
    {
        base.ApplyUiLocalisation();
        _messageLabel.Text = UiStrings.Get("discovery.account_unavailable");
        if (Grid.Columns["Name"] is DataGridViewColumn name) name.HeaderText = UiStrings.Get("discovery.col_name");
        if (Grid.Columns["Id"] is DataGridViewColumn id) id.HeaderText = UiStrings.Get("discovery.col_id");
        if (Grid.Columns["Status"] is DataGridViewColumn status) status.HeaderText = UiStrings.Get("discovery.col_status");
    }

    /// <inheritdoc/>
    public override void Reload()
    {
        bool available = _userSettings != null && Catalogue != null;
        _messageLabel.Visible = !available;
        Grid.Visible = available;
        base.Reload();
    }

    protected override void PopulateRows()
    {
        if (_userSettings == null || Catalogue == null) return;

        var current = CatalogueCompletionLogic.GetSeenIdSet(_userSettings, ArrayName);
        foreach (string id in PackIds)
        {
            bool complete = current.Contains(id);

            var row = new DataGridViewRow();
            row.CreateCells(Grid,
                complete,
                GetItemIcon(id),
                Database?.GetItem(id)?.Name ?? id,
                id,
                complete ? UiStrings.Get("discovery.status_complete") : UiStrings.Get("discovery.status_missing"));
            row.Tag = new SeenRow(id);
            Grid.Rows.Add(row);
        }
    }

    protected override void RefreshCounter()
    {
        if (_userSettings == null)
        {
            CounterLabel.Text = string.Empty;
            CompleteAllButton.Enabled = false;
            ClearAllButton.Enabled = false;
            return;
        }
        base.RefreshCounter();
    }

    protected override (int Have, int Total) GetCompletion()
    {
        if (_userSettings == null || Catalogue == null) return (0, 0);
        return CatalogueCompletionLogic.GetSeenCompletion(_userSettings, ArrayName, PackIds);
    }

    protected override bool ToggleRow(DataGridViewRow row, bool complete)
    {
        if (row.Tag is not SeenRow info || _userSettings == null) return false;

        var ids = new[] { info.Id };
        return complete
            ? CatalogueCompletionLogic.AddMissingSeen(_userSettings, ArrayName, ids) > 0
            : CatalogueCompletionLogic.RemoveSeen(_userSettings, ArrayName, ids) > 0;
    }

    protected override int CompleteAll()
    {
        if (_userSettings == null || Catalogue == null) return 0;
        return CatalogueCompletionLogic.AddMissingSeen(_userSettings, ArrayName, PackIds);
    }

    protected override int ClearAll()
    {
        if (_userSettings == null || Catalogue == null) return 0;
        return CatalogueCompletionLogic.RemoveSeen(_userSettings, ArrayName, PackIds);
    }
}
