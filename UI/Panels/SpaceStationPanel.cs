using NMSE.Core;
using NMSE.Core.Utilities;
using NMSE.Data;
using NMSE.Models;
using NMSE.UI.Controls;
using NMSE.UI.Util;

using System.Globalization;

namespace NMSE.UI.Panels;

/// <summary>
/// Space Station sub-panel: displays the Cosmos v7.0 space station system stats with
/// a left list of system addresses and a right-hand editor for their seven stats,
/// glyphs, packed SpacePoiDiscoveries data and the claimable values button.
/// </summary>
internal class SpaceStationSubPanel : UserControl
{
    public event EventHandler? DataModified;

    /// <summary>Raised when the user requests navigation to a JSON path in the Raw JSON Editor.</summary>
    internal event EventHandler<GoToJsonEventArgs>? GoToJsonRequested;

    /// <summary>Raised when the user requests navigation to a station base in the Bases tab.</summary>
    internal event EventHandler<int>? GoToBaseRequested;

    // Systems section
    private ListBox _systemList = null!;
    private TextBox _addressText = null!;
    private TextBox _portalCodeText = null!;
    private FlowLayoutPanel _glyphPanel = null!;
    private TableLayoutPanel _systemsDetails = null!;
    private DoubleBufferedTabControl _innerTabs = null!;
    private TabPage _stationTab = null!;
    private TabPage _poiTab = null!;
    private TableLayoutPanel _poiDetails = null!;
    private Label _poiWarningLabel = null!;
    private Label _poiLinkHint = null!;
    private Label _poiMissingNote = null!;
    private Label _poiInferenceNote = null!;
    private readonly InvariantNumericTextBox[] _statFields = new InvariantNumericTextBox[SpaceStationLogic.DisplayStatOrder.Length];
    private TextBox _packedData0Box = null!;
    private TextBox _packedData1Box = null!;
    private Label _packedData0TypeLabel = null!;
    private Label _packedData1TypeLabel = null!;
    private bool _packedData0Hex;
    private bool _packedData1Hex;
    private string _packedData0Last = "";
    private string _packedData1Last = "";
    private readonly List<PackedSlotCombo> _packedSlotCombos = new();
    private FlowLayoutPanel _packedSlotGroups = null!;
    private bool _suppressPackedSlotEvents;

    /// <summary>Number of slot combos per row in a POI type group.</summary>
    private const int PackedSlotColumns = 4;

    /// <summary>Fixed width of one slot cell (number label plus combo) in a POI type group.</summary>
    private const int PackedSlotCellWidth = 158;
    private readonly ToolTip _packedDataToolTip = new();
    private Button _setClaimableBtn = null!;
    private Button _goToBaseBtn = null!;
    private Button _gotoStatsBtn = null!;
    private Button _gotoPackedBtn = null!;

    // Cosmos Missions section
    private readonly List<MissionComboItem> _missionControls = new();

    // Labels for localisation
    private readonly List<(string key, Label control)> _labels = new();

    // State
    private bool _loading;
    private JsonObject? _saveData;
    private JsonObject? _playerState;
    private JsonArray? _spacePoiDiscoveries;
    private readonly List<SpaceStationLogic.StationSystemEntry> _systems = new();
    private SpaceStationLogic.StationSystemEntry? _selectedSystem;
    private (int Index, JsonObject Data)? _selectedPoiEntry;
    private (int DataIndex, JsonObject Data)? _selectedStationBase;
    private bool _abandonedSystems;

    public SpaceStationSubPanel()
    {
        DoubleBuffered = true;
        SuspendLayout();
        ThemeManager.ThemeChanged += OnThemeChanged;

        // --- Header strip: GOTO JSON buttons top-right ---
        var headerStrip = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
            RowCount = 1
        };
        headerStrip.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        headerStrip.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        headerStrip.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var gotoPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
        };
        _gotoStatsBtn = BuildGoToJsonButton();
        _gotoStatsBtn.Enabled = false;
        _gotoStatsBtn.Click += (_, _) =>
        {
            if (_selectedSystem != null)
                GoToJsonRequested?.Invoke(this, new GoToJsonEventArgs("PlayerStateData", "Stats", $"[{_selectedSystem.StatsIndex}]", "Stats", "[0]"));
        };
        _gotoPackedBtn = BuildGoToJsonButton();
        _gotoPackedBtn.Enabled = false;
        _gotoPackedBtn.Click += (_, _) =>
        {
            if (_selectedPoiEntry is (int poiIndex, _))
                GoToJsonRequested?.Invoke(this, new GoToJsonEventArgs("PlayerStateData", "SpacePoiDiscoveries", $"[{poiIndex}]", "PackedData0"));
        };
        gotoPanel.Controls.Add(_gotoStatsBtn);
        gotoPanel.Controls.Add(_gotoPackedBtn);
        headerStrip.Controls.Add(gotoPanel, 2, 0);

        // --- Content: system list on the left, inner tabs on the right ---
        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(0)
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));       // left: system list
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));   // right: inner tabs
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        content.Controls.Add(BuildSystemListPanel(), 0, 0);
        content.Controls.Add(BuildInnerTabs(), 1, 0);

        // --- Outer layout: header strip on top, content below ---
        var outerLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(10)
        };
        outerLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        outerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        outerLayout.Controls.Add(headerStrip, 0, 0);
        outerLayout.Controls.Add(content, 0, 1);

        Controls.Add(outerLayout);
        ResumeLayout(false);
        PerformLayout();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            ThemeManager.ThemeChanged -= OnThemeChanged;
        base.Dispose(disposing);
    }

    /// <summary>
    /// Builds the left-hand system list: heading and address list. The list drives the
    /// two inner tabs on its right.
    /// </summary>
    private Control BuildSystemListPanel()
    {
        var leftLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            MinimumSize = new Size(320, 0),
            Padding = new Padding(0, 0, 12, 0)
        };
        leftLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        leftLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var title = new Label
        {
            Text = UiStrings.Get("base.station.systems_header"),
            AutoSize = true,
            Padding = new Padding(0, 0, 0, 4)
        };
        FontManager.ApplyHeadingFont(title, 11);
        _labels.Add(("base.station.systems_header", title));
        leftLayout.Controls.Add(title, 0, 0);

        _systemList = new ListBox
        {
            Dock = DockStyle.Fill,
            SelectionMode = SelectionMode.One,
            IntegralHeight = false
        };
        _systemList.SelectedIndexChanged += OnSystemSelected;
        leftLayout.Controls.Add(_systemList, 0, 1);

        return leftLayout;
    }

    /// <summary>
    /// Builds the two inner tabs shown to the right of the system list: the station
    /// stats and missions, and the space POI discovery slots.
    /// </summary>
    private Control BuildInnerTabs()
    {
        _innerTabs = new DoubleBufferedTabControl { Dock = DockStyle.Fill };
        _stationTab = new TabPage(UiStrings.Get("base.station.new_keys_header"));
        _poiTab = new TabPage(UiStrings.Get("base.station.tab_space_pois"));
        _stationTab.Controls.Add(BuildStationTabContent());
        _poiTab.Controls.Add(BuildSpacePoisTabContent());
        _innerTabs.TabPages.Add(_stationTab);
        _innerTabs.TabPages.Add(_poiTab);
        return _innerTabs;
    }

    /// <summary>
    /// Builds the Space Stations tab: the address, portal code, glyphs and stat fields
    /// with the station missions column beside them.
    /// </summary>
    private Control BuildStationTabContent()
    {
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };

        var layout = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 1,
            Location = new Point(0, 0),
            Padding = new Padding(0)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _systemsDetails = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            Padding = new Padding(0)
        };
        var details = _systemsDetails;
        details.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        details.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        int row = 0;

        _addressText = new TextBox { Width = 190, ReadOnly = true, TabStop = false, Anchor = AnchorStyles.Left };
        ApplyReadOnlyGreyStyle(_addressText);
        AddDetailRow(details, "base.station.address", _addressText, row);
        row++;

        _portalCodeText = new TextBox { Width = 120, ReadOnly = true, TabStop = false, Anchor = AnchorStyles.Left };
        ApplyReadOnlyGreyStyle(_portalCodeText);
        AddDetailRow(details, "base.station.portal_code", _portalCodeText, row);
        row++;

        var glyphLabel = new Label
        {
            Text = UiStrings.Get("base.station.glyphs"),
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Padding = new Padding(0, 5, 10, 0)
        };
        _labels.Add(("base.station.glyphs", glyphLabel));
        _glyphPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0)
        };
        details.Controls.Add(glyphLabel, 0, row);
        details.Controls.Add(_glyphPanel, 1, row);
        row++;

        for (int i = 0; i < SpaceStationLogic.DisplayStatOrder.Length; i++)
        {
            string statId = SpaceStationLogic.DisplayStatOrder[i];
            var field = new InvariantNumericTextBox { Width = 140, Anchor = AnchorStyles.Left, Minimum = 0 };
            int captured = i;
            field.NumericValueChanged += (s, e) => OnStatChanged(captured);
            _statFields[i] = field;
            AddDetailRow(details, GetStatLocKey(statId), field, row);
            row++;
        }

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 8, 0, 0)
        };
        _setClaimableBtn = new Button
        {
            Text = UiStrings.Get("base.station.set_claimable_values"),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(160, 0),
            Enabled = false
        };
        _setClaimableBtn.Click += OnSetClaimableValues;
        _goToBaseBtn = new Button
        {
            Text = UiStrings.Get("base.station.go_to_base"),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(140, 0),
            Enabled = false
        };
        _goToBaseBtn.Click += OnGoToBase;
        buttonPanel.Controls.Add(_setClaimableBtn);
        buttonPanel.Controls.Add(_goToBaseBtn);
        details.Controls.Add(buttonPanel, 0, row);
        details.SetColumnSpan(buttonPanel, 2);
        row++;

        layout.Controls.Add(details, 0, 0);
        layout.Controls.Add(BuildMissionsColumn(), 1, 0);
        scroll.Controls.Add(layout);
        return scroll;
    }

    /// <summary>
    /// Builds the Space POIs tab: explanation, raw packed data editors and the
    /// inferred per-slot type groups.
    /// </summary>
    private Control BuildSpacePoisTabContent()
    {
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };

        _poiDetails = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            Location = new Point(0, 0),
            Padding = new Padding(0)
        };
        _poiDetails.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _poiDetails.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        int row = 0;

        var poiHeading = new Label
        {
            Text = UiStrings.Get("base.station.poi_discoveries_header"),
            AutoSize = true,
            Padding = new Padding(0, 4, 0, 4)
        };
        FontManager.ApplyHeadingFont(poiHeading, 11);
        _labels.Add(("base.station.poi_discoveries_header", poiHeading));
        _poiDetails.Controls.Add(poiHeading, 0, row);
        _poiDetails.SetColumnSpan(poiHeading, 2);
        row++;

        // Shown instead of the editors when the selected system has no POI data.
        _poiMissingNote = new Label
        {
            Text = UiStrings.Get("base.station.packed_data_missing_note"),
            AutoSize = true,
            Visible = false,
            Font = FontManager.CreateFont(9F, FontStyle.Bold),
            Padding = new Padding(0, 0, 0, 6)
        };
        _labels.Add(("base.station.packed_data_missing_note", _poiMissingNote));
        _poiDetails.Controls.Add(_poiMissingNote, 0, row);
        _poiDetails.SetColumnSpan(_poiMissingNote, 2);
        row++;

        _poiWarningLabel = new Label
        {
            Text = UiStrings.Get("base.station.packed_data_warning"),
            AutoSize = true,
            Font = FontManager.CreateFont(9F, FontStyle.Bold),
            Padding = new Padding(0, 0, 0, 4)
        };
        _labels.Add(("base.station.packed_data_warning", _poiWarningLabel));
        _poiDetails.Controls.Add(_poiWarningLabel, 0, row);
        _poiDetails.SetColumnSpan(_poiWarningLabel, 2);
        row++;

        _poiInferenceNote = new Label
        {
            Text = UiStrings.Get("base.station.packed_data_unidentified_note"),
            AutoSize = true,
            Visible = false,
            Padding = new Padding(0, 2, 0, 6)
        };
        _labels.Add(("base.station.packed_data_unidentified_note", _poiInferenceNote));
        _poiDetails.Controls.Add(_poiInferenceNote, 0, row);
        _poiDetails.SetColumnSpan(_poiInferenceNote, 2);
        row++;

        _packedData0Box = CreatePackedDataTextBox(0, out _packedData0TypeLabel);
        AddDetailRow(_poiDetails, "base.station.packed_data0", BuildPackedDataField(_packedData0Box, _packedData0TypeLabel), row);
        row++;

        _packedData1Box = CreatePackedDataTextBox(1, out _packedData1TypeLabel);
        AddDetailRow(_poiDetails, "base.station.packed_data1", BuildPackedDataField(_packedData1Box, _packedData1TypeLabel), row);
        row++;

        // Explain that the raw values and the slot states below are two views of the
        // same data, so users understand edits in either place update the other.
        _poiLinkHint = new Label
        {
            Text = UiStrings.Get("base.station.packed_data_link_hint"),
            AutoSize = true,
            Padding = new Padding(0, 2, 0, 6)
        };
        _labels.Add(("base.station.packed_data_link_hint", _poiLinkHint));
        _poiDetails.Controls.Add(_poiLinkHint, 0, row);
        _poiDetails.SetColumnSpan(_poiLinkHint, 2);
        row++;

        BuildPackedSlotCombos();
        _packedSlotGroups = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(0, 4, 0, 0),
            Padding = new Padding(0)
        };
        _poiDetails.Controls.Add(_packedSlotGroups, 0, row);
        _poiDetails.SetColumnSpan(_packedSlotGroups, 2);
        row++;

        scroll.Controls.Add(_poiDetails);
        return scroll;
    }

    /// <summary>
    /// Builds the station missions column: a heading and one stacked label/combo
    /// row per station-claim mission. The column sizes to its content so it sits
    /// directly beside the stats details, and the rows stay at content height so
    /// each label remains next to its combo.
    /// </summary>
    private Control BuildMissionsColumn()
    {
        var column = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            RowCount = 1 + SpaceStationLogic.CosmosMissionIds.Length,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(16, 0, 0, 0)
        };
        column.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        column.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var heading = new Label
        {
            Text = UiStrings.Get("base.station.cosmos_missions_header"),
            AutoSize = true,
            Font = FontManager.CreateFont(9F, FontStyle.Bold),
            Padding = new Padding(0, 0, 0, 4)
        };
        _labels.Add(("base.station.cosmos_missions_header", heading));
        column.Controls.Add(heading, 0, 0);
        column.SetColumnSpan(heading, 2);

        int row = 1;
        foreach (string missionId in SpaceStationLogic.CosmosMissionIds)
        {
            string labelKey = SpaceStationLogic.GetCosmosMissionLabelKey(missionId) ?? missionId;
            var label = new Label
            {
                Text = UiStrings.Get(labelKey),
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Padding = new Padding(0, 5, 10, 0)
            };
            _labels.Add((labelKey, label));
            column.Controls.Add(label, 0, row);

            var combo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 140,
                Anchor = AnchorStyles.Left | AnchorStyles.Top
            };
            combo.Items.AddRange(new object[]
            {
                UiStrings.Get("base.station.mission_not_started"),
                UiStrings.Get("base.station.mission_active"),
                UiStrings.Get("base.station.mission_completed")
            });
            combo.SelectedIndex = 0;
            combo.SelectedIndexChanged += OnMissionStateChanged;
            _missionControls.Add(new MissionComboItem(missionId, combo));
            column.Controls.Add(combo, 1, row);
            row++;
        }

        return column;
    }

    /// <summary>Writes a mission state change through to MissionProgress.</summary>
    private void OnMissionStateChanged(object? sender, EventArgs e)
    {
        if (_loading) return;
        if (_playerState == null) return;
        if (sender is not ComboBox combo) return;

        var item = _missionControls.FirstOrDefault(x => x.Combo == combo);
        if (item == null || combo.SelectedIndex < 0) return;

        SpaceStationLogic.ApplyMissionState(_playerState, item.MissionId, (SpaceStationLogic.MissionState)combo.SelectedIndex);
        DataModified?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Loads the mission combo states from the save data.</summary>
    private void LoadMissionControls()
    {
        if (_playerState == null) return;
        foreach (var item in _missionControls)
            item.Combo.SelectedIndex = (int)SpaceStationLogic.GetMissionState(_playerState, item.MissionId);
    }

    /// <summary>Resets all mission combos to the not-started state.</summary>
    private void ResetMissionControls()
    {
        foreach (var item in _missionControls)
            item.Combo.SelectedIndex = (int)SpaceStationLogic.MissionState.NotStarted;
    }

    /// <summary>A Cosmos mission combo binding.</summary>
    private sealed class MissionComboItem
    {
        public string MissionId { get; }
        public ComboBox Combo { get; }

        public MissionComboItem(string missionId, ComboBox combo)
        {
            MissionId = missionId;
            Combo = combo;
        }
    }

    /// <summary>Adds a label/field row to the stats details grid.</summary>
    private void AddDetailRow(TableLayoutPanel details, string key, Control field, int row)
    {
        var label = new Label
        {
            Text = UiStrings.Get(key),
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Padding = new Padding(0, 5, 10, 0)
        };
        _labels.Add((key, label));
        details.Controls.Add(label, 0, row);
        details.Controls.Add(field, 1, row);
    }

    /// <summary>Builds a GOTO JSON button matching the style used across the other panels.</summary>
    private static Button BuildGoToJsonButton()
    {
        return new Button
        {
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderColor = ThemeManager.Effective == AppTheme.Dark ? Color.FromArgb(100, 100, 100) : SystemColors.ControlDark, BorderSize = 1 },
            Font = new Font("Segoe UI Emoji", 9F, FontStyle.Regular, GraphicsUnit.Point),
            Size = new Size(28, 24),
            Text = "\U0001F4D1",
            Margin = new Padding(1, 1, 1, 1),
            Cursor = Cursors.Hand,
        };
    }

    /// <summary>
    /// Creates a packed data editor. Values are entered as decimal or 0x hex and
    /// committed on Enter or focus loss.
    /// </summary>
    private TextBox CreatePackedDataTextBox(int index, out Label typeLabel)
    {
        var box = new TextBox
        {
            Width = 170,
            Anchor = AnchorStyles.Left,
            Margin = Padding.Empty,
            Enabled = false,
        };
        box.Validated += (_, _) => CommitPackedData(index);
        box.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            CommitPackedData(index);
        };
        _packedDataToolTip.SetToolTip(box, UiStrings.Get("base.station.packed_data_hint"));

        typeLabel = new Label
        {
            Text = UiStrings.Get("base.station.packed_data_type"),
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = Padding.Empty,
            Padding = new Padding(8, 5, 0, 0)
        };
        return box;
    }

    /// <summary>
    /// Builds the field cell for a packed data value: the editor with its
    /// data-type note nested directly against it. Margins are zeroed so the field
    /// and note labels share the same vertical line as the row label.
    /// </summary>
    private static Control BuildPackedDataField(Control field, Label typeLabel)
    {
        field.Margin = Padding.Empty;

        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0)
        };
        panel.Controls.Add(field);
        panel.Controls.Add(typeLabel);
        return panel;
    }

    /// <summary>
    /// Creates the 32 slot combos once. They are re-parented into type group rows
    /// whenever a system is selected.
    /// </summary>
    private void BuildPackedSlotCombos()
    {
        var levelItems = PackedLevelItems();
        for (int slot = 0; slot < SpaceStationLogic.PackedDataSlotCount; slot++)
        {
            var combo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 124,
                Enabled = false,
                Margin = new Padding(0, 1, 10, 1)
            };
            combo.Items.AddRange(levelItems);
            combo.SelectedIndex = 0;
            combo.SelectedIndexChanged += OnPackedSlotChanged;
            _packedDataToolTip.SetToolTip(combo, UiStrings.Format(
                "base.station.packed_data_slot_tooltip",
                (slot + 1).ToString(CultureInfo.InvariantCulture)));
            _packedSlotCombos.Add(new PackedSlotCombo(slot, combo));
        }
    }

    /// <summary>
    /// Rebuilds the slot groups for the selected system. When the POI layout can be
    /// inferred, each type gets its own row of slot combos; otherwise a single
    /// "POI slots" row shows all 32. Layouts inferred once are remembered so the labels
    /// survive later edits to the packed values.
    /// </summary>
    private void RebuildPackedSlotGroups(JsonObject? entry)
    {
        _packedSlotGroups.SuspendLayout();
        try
        {
            _packedSlotGroups.Controls.Clear();

            // With no entry there is nothing to edit: show the "not unpacked" note and
            // hide the editors that explain the slot data.
            bool hasEntry = entry != null;
            _poiMissingNote.Visible = !hasEntry;
            _poiWarningLabel.Visible = hasEntry;
            _poiLinkHint.Visible = hasEntry;
            _poiInferenceNote.Visible = false;
            _packedSlotGroups.Visible = hasEntry;

            if (!hasEntry)
                return;

            string[]? tokens = BuildSlotTokens(entry!);
            ulong ua = GetSelectedUa();

            if (tokens != null && ua != 0)
            {
                tokens = SpacePoiSlotTokens.Merge(tokens, SpacePoiLayoutCache.Get(ua));
                SpacePoiLayoutCache.Remember(ua, tokens);
                SpacePoiLayoutCache.SaveIfDirty();
            }
            else if (tokens == null && ua != 0)
            {
                // No live entry (or no inference): use a remembered layout if one exists.
                tokens = SpacePoiLayoutCache.Get(ua);
            }

            if (tokens == null)
            {
                _poiInferenceNote.Visible = false;
                AddPackedSlotRow(UiStrings.Get("base.station.packed_data_all_slots"), Enumerable.Range(0, SpaceStationLogic.PackedDataSlotCount));
                return;
            }

            var knownTypes = new HashSet<string>(SpacePoiTableDatabase.Types.Select(t => t.Type), StringComparer.Ordinal);

            // One row per type, in generation order.
            for (int typeIndex = 0; typeIndex < SpacePoiTableDatabase.Types.Count; typeIndex++)
            {
                var type = SpacePoiTableDatabase.Types[typeIndex];
                var slots = new List<int>();
                for (int slot = 0; slot < tokens.Length; slot++)
                    if (string.Equals(tokens[slot], type.Type, StringComparison.Ordinal))
                        slots.Add(slot);

                if (slots.Count == 0) continue;
                string name = string.IsNullOrEmpty(type.DisplayName) ? type.Name : type.DisplayName;
                string header = slots.Count == 1
                    ? UiStrings.Format("base.station.packed_data_group_slot", name, (slots[0] + 1).ToString(CultureInfo.InvariantCulture))
                    : UiStrings.Format("base.station.packed_data_group_slots", name,
                        (slots[0] + 1).ToString(CultureInfo.InvariantCulture),
                        (slots[^1] + 1).ToString(CultureInfo.InvariantCulture));
                AddPackedSlotRow(header, slots);
            }

            // Slots with differing types across layouts (or cached names no longer in the table).
            var unidentified = new List<int>();
            var unallocated = new List<int>();
            for (int slot = 0; slot < tokens.Length; slot++)
            {
                string token = tokens[slot];
                if (token == SpacePoiSlotTokens.Unallocated)
                    unallocated.Add(slot);
                else if (!knownTypes.Contains(token))
                    unidentified.Add(slot);
            }

            if (unidentified.Count > 0)
                AddPackedSlotRow(UiStrings.Get("base.station.packed_data_unidentified"), unidentified);

            if (unallocated.Count > 0)
                AddPackedSlotRow(UiStrings.Get("base.station.packed_data_unallocated"), unallocated);

            // Explain why some types are missing, since edited slot values can no
            // longer distinguish the layout.
            _poiInferenceNote.Visible = unidentified.Count > 0;
        }
        finally
        {
            _packedSlotGroups.ResumeLayout(true);
        }
    }

    /// <summary>
    /// Builds the 32 layout tokens for one entry from the current inference result.
    /// Returns null when the table or packed values are unavailable.
    /// </summary>
    private string[]? BuildSlotTokens(JsonObject entry)
    {
        if (SpacePoiTableDatabase.Types.Count == 0) return null;
        if (!SpaceStationLogic.TryGetPackedData(entry, "PackedData0", out ulong packed0, out _)) return null;
        if (!SpaceStationLogic.TryGetPackedData(entry, "PackedData1", out ulong packed1, out _)) return null;

        // Abandoned systems follow restricted spawn rules (no Outposts). Prefer the
        // rules that match the save's difficulty, then fall back to the other set for
        // systems that turn out to follow the opposite rules.
        var result = SpacePoiSlotInference.Infer(packed0, packed1, SpacePoiTableDatabase.Types, _abandonedSystems);
        if (result == null)
            result = SpacePoiSlotInference.Infer(packed0, packed1, SpacePoiTableDatabase.Types, !_abandonedSystems);

        var tokens = new string[SpaceStationLogic.PackedDataSlotCount];
        if (result == null)
        {
            Array.Fill(tokens, SpacePoiSlotTokens.Unknown);
            return tokens;
        }

        for (int slot = 0; slot < tokens.Length; slot++)
        {
            var info = result.Slots[slot];
            tokens[slot] = info.TypeIsUnique
                ? SpacePoiTableDatabase.Types[info.TypeIndex].Type
                : info.CoveredByLayouts ? SpacePoiSlotTokens.Unknown : SpacePoiSlotTokens.Unallocated;
        }
        return tokens;
    }

    /// <summary>
    /// True when the save's difficulty sets NPC population to Abandoned, which makes
    /// systems follow the abandoned spawn rules (for example no Outposts spawn).
    /// </summary>
    private static bool IsAbandonedSystems(JsonObject? playerState)
    {
        var settings = playerState?.GetObject("DifficultyState")?.GetObject("Settings");
        var npcPopulation = settings?.GetObject("NPCPopulation");
        return string.Equals(npcPopulation?.GetString("NPCPopulationDifficulty"), "Abandoned", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>UA of the selected system, or 0 when none is selected.</summary>
    private ulong GetSelectedUa() =>
        _selectedSystem?.Address is long address ? unchecked((ulong)address) : 0;

    /// <summary>
    /// Learns the POI layouts of every listed system that still holds inferable values,
    /// so opening an older save populates the layout memory for later sessions.
    /// </summary>
    private void LearnPoiLayouts()
    {
        if (SpacePoiTableDatabase.Types.Count == 0 || _spacePoiDiscoveries == null) return;

        bool learned = false;
        int processed = 0;
        foreach (var system in _systems)
        {
            if (processed++ >= 256 || system.Address is not long address) continue;
            ulong ua = unchecked((ulong)address);
            if (ua == 0) continue;
            if (SpaceStationLogic.FindSpacePoiEntry(_spacePoiDiscoveries, system.Address) is not (_, JsonObject entry)) continue;

            var tokens = BuildSlotTokens(entry);
            if (tokens == null) continue;
            SpacePoiLayoutCache.Remember(ua, tokens);
            learned = true;
        }

        if (learned)
            SpacePoiLayoutCache.SaveIfDirty();
    }

    /// <summary>
    /// Adds one group row: a bold header followed by each slot's number label and
    /// combo box in slot order.
    /// </summary>
    private void AddPackedSlotRow(string headerText, IEnumerable<int> slots)
    {
        var slotList = slots as IReadOnlyList<int> ?? slots.ToList();
        int rows = 1 + (slotList.Count + PackedSlotColumns - 1) / PackedSlotColumns;

        var group = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = PackedSlotColumns,
            RowCount = rows,
            Margin = new Padding(0, 0, 0, 6),
            Padding = new Padding(0)
        };
        for (int column = 0; column < PackedSlotColumns; column++)
            group.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, PackedSlotCellWidth));
        for (int gridRow = 0; gridRow < rows; gridRow++)
            group.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var header = new Label
        {
            Text = headerText,
            AutoSize = true,
            Font = FontManager.CreateFont(9F, FontStyle.Bold),
            Margin = new Padding(0, 4, 8, 2)
        };
        _packedDataToolTip.SetToolTip(header, UiStrings.Get("base.station.packed_data_inferred_hint"));
        group.Controls.Add(header, 0, 0);
        group.SetColumnSpan(header, PackedSlotColumns);

        for (int i = 0; i < slotList.Count; i++)
        {
            int gridRow = 1 + i / PackedSlotColumns;
            int column = i % PackedSlotColumns;

            var cell = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            var slotLabel = new Label
            {
                Text = (slotList[i] + 1).ToString(CultureInfo.InvariantCulture),
                AutoSize = true,
                Margin = new Padding(0, 5, 3, 0)
            };
            cell.Controls.Add(slotLabel);
            cell.Controls.Add(_packedSlotCombos[slotList[i]].Combo);
            group.Controls.Add(cell, column, gridRow);
        }

        _packedSlotGroups.Controls.Add(group);
    }

    /// <summary>Localised items for the four packed slot discovery levels.</summary>
    private static object[] PackedLevelItems() => new object[]
    {
        UiStrings.Get("base.station.packed_data_level_hidden"),
        UiStrings.Get("base.station.packed_data_level_undiscovered"),
        UiStrings.Get("base.station.packed_data_level_discovered"),
        UiStrings.Get("base.station.packed_data_level_completed")
    };

    /// <summary>
    /// Styles a read-only TextBox to look disabled (greyed out) while remaining
    /// selectable and copyable. Uses the theme's disabled cell colours.
    /// </summary>
    private static void ApplyReadOnlyGreyStyle(TextBox box)
    {
        var palette = ThemeColors.Get(ThemeManager.Effective == AppTheme.Dark ? "Dark" : "Light");
        box.BackColor = palette.GridCellDisabledBackground;
        box.ForeColor = palette.GridCellDisabledForeground;
    }

    /// <summary>Re-applies theme-dependent colours when the theme changes.</summary>
    private void OnThemeChanged()
    {
        ApplyReadOnlyGreyStyle(_addressText);
        ApplyReadOnlyGreyStyle(_portalCodeText);
    }

    /// <summary>Returns the localisation key for a station stat Id.</summary>
    private static string GetStatLocKey(string statId)
    {
        switch (statId)
        {
            case SpaceStationLogic.StatWarStanding: return "base.station.stat_war_standing";
            case SpaceStationLogic.StatSpPoiMissions: return "base.station.stat_sp_poi_missions";
            case SpaceStationLogic.StatWGuildStand: return "base.station.stat_wguild_stand";
            case SpaceStationLogic.StatEGuildStand: return "base.station.stat_eguild_stand";
            case SpaceStationLogic.StatTraStanding: return "base.station.stat_tra_standing";
            case SpaceStationLogic.StatExpStanding: return "base.station.stat_exp_standing";
            case SpaceStationLogic.StatTGuildStand: return "base.station.stat_tguild_stand";
            default: return "base.station.stat_war_standing";
        }
    }

    public void LoadData(JsonObject saveData)
    {
        _loading = true;
        SuspendLayout();
        try
        {
            ClearAllFields();

            _saveData = saveData;
        _playerState = saveData.GetObject("PlayerStateData");
        _spacePoiDiscoveries = _playerState?.GetArray("SpacePoiDiscoveries");
        _abandonedSystems = IsAbandonedSystems(_playerState);
        if (_playerState != null)
        {
            LoadSystems();
            LoadMissionControls();
            LearnPoiLayouts();
        }

            if (_systems.Count > 0)
                _systemList.SelectedIndex = 0;
        }
        finally
        {
            ResumeLayout(true);
            _loading = false;
        }
    }

    public void SaveData(JsonObject saveData)
    {
        // All edits are written through to the JSON tree immediately on change.
    }

    /// <summary>Resets all fields to their empty state.</summary>
    private void ClearAllFields()
    {
        _saveData = null;
        _playerState = null;
        _spacePoiDiscoveries = null;
        _systemList.Items.Clear();
        _systems.Clear();
        _selectedSystem = null;
        _selectedPoiEntry = null;
        _selectedStationBase = null;
        _addressText.Text = "";
        _portalCodeText.Text = "";
        CoordinateHelper.UpdateGlyphPanel(_glyphPanel, "");
        foreach (var field in _statFields) field.NumericValue = null;
        ClearPackedDataFields();
        _setClaimableBtn.Enabled = false;
        _goToBaseBtn.Enabled = false;
        _gotoStatsBtn.Enabled = false;
        _gotoPackedBtn.Enabled = false;
        ResetMissionControls();
    }

    /// <summary>
    /// Populates the left system list from PlayerStateData.Stats.
    /// Entries are ordered by resolvable name: station base names first, then
    /// discovery names, then those falling back to the portal code.
    /// </summary>
    private void LoadSystems()
    {
        if (_playerState == null) return;
        var found = SpaceStationLogic.FindStationSystems(_playerState);

        var ranked = new List<(SpaceStationLogic.StationSystemEntry System, int Rank)>(found.Count);
        foreach (var system in found)
        {
            string portalCode = SpaceStationLogic.AddressToPortalCode(system.Address);
            string? baseName = _saveData != null
                ? SpaceStationLogic.ResolveStationBaseName(_playerState, system.Address)
                : null;
            string? discoveryName = baseName == null && _saveData != null
                ? SpaceStationLogic.ResolveSystemName(_saveData, system.Address)
                : null;

            system.DisplayName = baseName
                                 ?? discoveryName
                                 ?? UiStrings.Format("base.station.portal_code_fallback", portalCode);
            ranked.Add((system, baseName != null ? 0 : discoveryName != null ? 1 : 2));
        }

        _systemList.BeginUpdate();
        try
        {
            foreach (var entry in ranked.OrderBy(x => x.Rank).ThenBy(x => x.System.StatsIndex))
            {
                _systems.Add(entry.System);
                _systemList.Items.Add(entry.System);
            }
        }
        finally
        {
            _systemList.EndUpdate();
        }
    }

    private void OnSystemSelected(object? sender, EventArgs e)
    {
        if (_systemList.SelectedItem is not SpaceStationLogic.StationSystemEntry system)
        {
            _selectedSystem = null;
            _selectedPoiEntry = null;
            _selectedStationBase = null;
            ClearSystemDetail();
            return;
        }

        _selectedSystem = system;
        _addressText.Text = system.Address?.ToString() ?? "";
        _portalCodeText.Text = SpaceStationLogic.AddressToPortalCode(system.Address);
        CoordinateHelper.UpdateGlyphPanel(_glyphPanel, _portalCodeText.Text);

        for (int i = 0; i < SpaceStationLogic.DisplayStatOrder.Length; i++)
            _statFields[i].NumericValue = SpaceStationLogic.GetStatValue(system.Entry, SpaceStationLogic.DisplayStatOrder[i]);

        _selectedPoiEntry = SpaceStationLogic.FindSpacePoiEntry(_spacePoiDiscoveries, system.Address);
        if (_selectedPoiEntry is (_, JsonObject poiEntry))
        {
            RebuildPackedSlotGroups(poiEntry);
            RefreshPackedDisplay(0, poiEntry);
            RefreshPackedDisplay(1, poiEntry);
            RefreshPackedSlots(0, poiEntry);
            RefreshPackedSlots(1, poiEntry);
            _packedData0Box.Enabled = true;
            _packedData1Box.Enabled = true;
        }
        else
        {
            ClearPackedDataFields();
        }

        _selectedStationBase = SpaceStationLogic.FindStationBase(_playerState?.GetArray("PersistentPlayerBases"), system.Address);
        _goToBaseBtn.Enabled = _selectedStationBase.HasValue;
        _setClaimableBtn.Enabled = true;
        _gotoStatsBtn.Enabled = true;
        _gotoPackedBtn.Enabled = _selectedPoiEntry.HasValue;
    }

    /// <summary>Clears the right-hand system detail fields.</summary>
    private void ClearSystemDetail()
    {
        _addressText.Text = "";
        _portalCodeText.Text = "";
        CoordinateHelper.UpdateGlyphPanel(_glyphPanel, "");
        foreach (var field in _statFields) field.NumericValue = null;
        ClearPackedDataFields();
        _setClaimableBtn.Enabled = false;
        _goToBaseBtn.Enabled = false;
        _gotoStatsBtn.Enabled = false;
        _gotoPackedBtn.Enabled = false;
    }

    /// <summary>Clears both packed data editors and re-enables the empty state.</summary>
    private void ClearPackedDataFields()
    {
        _packedData0Hex = false;
        _packedData1Hex = false;
        _packedData0Last = "";
        _packedData1Last = "";
        _packedData0Box.Text = "";
        _packedData1Box.Text = "";
        _packedData0Box.Enabled = false;
        _packedData1Box.Enabled = false;
        ClearPackedSlotCombos();
        RebuildPackedSlotGroups(null);
        UpdatePackedDataTypeLabels();
    }

    /// <summary>
    /// Re-reads a packed data field from the entry and refreshes its editor text,
    /// stored representation flag and type note.
    /// </summary>
    private void RefreshPackedDisplay(int index, JsonObject entry)
    {
        string key = index == 0 ? "PackedData0" : "PackedData1";
        bool has = SpaceStationLogic.TryGetPackedData(entry, key, out ulong value, out bool isHex);
        string text = has ? SpaceStationLogic.FormatPackedData(value, isHex) : "";

        if (index == 0)
        {
            _packedData0Hex = isHex;
            _packedData0Last = text;
            _packedData0Box.Text = text;
        }
        else
        {
            _packedData1Hex = isHex;
            _packedData1Last = text;
            _packedData1Box.Text = text;
        }
        UpdatePackedDataTypeLabels();
    }

    /// <summary>Commits an edited packed data field back to the save data.</summary>
    private void CommitPackedData(int index)
    {
        if (_loading) return;
        if (_selectedPoiEntry is not (_, JsonObject entry)) return;

        var box = index == 0 ? _packedData0Box : _packedData1Box;
        string key = index == 0 ? "PackedData0" : "PackedData1";
        bool preferHex = index == 0 ? _packedData0Hex : _packedData1Hex;
        string previous = index == 0 ? _packedData0Last : _packedData1Last;

        if (!SpaceStationLogic.TryParsePackedData(box.Text, out ulong value))
        {
            box.Text = previous;
            return;
        }

        bool changed = SpaceStationLogic.SetPackedData(entry, key, value, preferHex);
        RefreshPackedDisplay(index, entry);
        RefreshPackedSlots(index, entry);
        RebuildPackedSlotGroups(entry);
        if (changed)
            DataModified?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Updates the storage-type notes beside both packed data editors.</summary>
    private void UpdatePackedDataTypeLabels()
    {
        _packedData0TypeLabel.Text = UiStrings.Get(_packedData0Hex
            ? "base.station.packed_data_type_hex"
            : "base.station.packed_data_type");
        _packedData1TypeLabel.Text = UiStrings.Get(_packedData1Hex
            ? "base.station.packed_data_type_hex"
            : "base.station.packed_data_type");
    }

    /// <summary>
    /// Refreshes the 16 slot combos of one packed data dword from the entry value,
    /// or disables them when the entry has no value.
    /// </summary>
    private void RefreshPackedSlots(int index, JsonObject entry)
    {
        string key = index == 0 ? "PackedData0" : "PackedData1";
        bool has = SpaceStationLogic.TryGetPackedData(entry, key, out ulong value, out _);

        _suppressPackedSlotEvents = true;
        try
        {
            foreach (var item in _packedSlotCombos)
            {
                if (item.Slot / 16 != index) continue;
                item.Combo.SelectedIndex = has
                    ? (int)SpaceStationLogic.GetPackedDataSlot(value, item.Slot % 16)
                    : 0;
                item.Combo.Enabled = has;
            }
        }
        finally
        {
            _suppressPackedSlotEvents = false;
        }
    }

    /// <summary>Resets every slot combo to Hidden and disables them.</summary>
    private void ClearPackedSlotCombos()
    {
        _suppressPackedSlotEvents = true;
        try
        {
            foreach (var item in _packedSlotCombos)
            {
                item.Combo.SelectedIndex = 0;
                item.Combo.Enabled = false;
            }
        }
        finally
        {
            _suppressPackedSlotEvents = false;
        }
    }

    /// <summary>Writes a slot combo change through to the packed value.</summary>
    private void OnPackedSlotChanged(object? sender, EventArgs e)
    {
        if (_loading || _suppressPackedSlotEvents) return;
        if (_selectedPoiEntry is not (_, JsonObject entry)) return;
        if (sender is not ComboBox combo) return;

        var item = _packedSlotCombos.FirstOrDefault(x => x.Combo == combo);
        if (item == null || combo.SelectedIndex < 0) return;

        int index = item.Slot / 16;
        string key = index == 0 ? "PackedData0" : "PackedData1";
        bool preferHex = index == 0 ? _packedData0Hex : _packedData1Hex;

        if (!SpaceStationLogic.TryGetPackedData(entry, key, out ulong value, out _))
            return;

        ulong updated = SpaceStationLogic.SetPackedDataSlot(
            value, item.Slot % 16,
            (SpaceStationLogic.SpacePoiDiscoveryLevel)combo.SelectedIndex);
        bool changed = SpaceStationLogic.SetPackedData(entry, key, updated, preferHex);
        RefreshPackedDisplay(index, entry);
        if (changed)
            DataModified?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>A packed data slot combo binding (0-based slot index).</summary>
    private sealed class PackedSlotCombo
    {
        public int Slot { get; }
        public ComboBox Combo { get; }

        public PackedSlotCombo(int slot, ComboBox combo)
        {
            Slot = slot;
            Combo = combo;
        }
    }

    private void OnStatChanged(int index)
    {
        if (_loading) return;
        if (_selectedSystem == null) return;
        if (_statFields[index].NumericValue is not double value) return;
        if (value < 0 || value > int.MaxValue) return;

        SpaceStationLogic.SetStatValue(_selectedSystem.Entry, SpaceStationLogic.DisplayStatOrder[index], (int)value);
        DataModified?.Invoke(this, EventArgs.Empty);
    }

    private void OnSetClaimableValues(object? sender, EventArgs e)
    {
        if (_selectedSystem == null) return;

        var result = MessageBox.Show(this,
            UiStrings.Get("base.station.set_claimable_confirm"),
            UiStrings.Get("base.station.set_claimable_title"),
            MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (result != DialogResult.Yes) return;

        bool changed = SpaceStationLogic.ApplyClaimableMinimums(_selectedSystem.Entry);
        for (int i = 0; i < SpaceStationLogic.DisplayStatOrder.Length; i++)
            _statFields[i].NumericValue = SpaceStationLogic.GetStatValue(_selectedSystem.Entry, SpaceStationLogic.DisplayStatOrder[i]);

        if (changed)
        {
            DataModified?.Invoke(this, EventArgs.Empty);
            MessageBox.Show(this,
                UiStrings.Get("base.station.set_claimable_success"),
                UiStrings.Get("base.station.set_claimable_title"),
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        else
        {
            MessageBox.Show(this,
                UiStrings.Get("base.station.set_claimable_already_high"),
                UiStrings.Get("base.station.set_claimable_title"),
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void OnGoToBase(object? sender, EventArgs e)
    {
        if (_selectedStationBase is not (int dataIndex, _)) return;
        GoToBaseRequested?.Invoke(this, dataIndex);
    }

    public void ApplyUiLocalisation()
    {
        foreach (var (key, label) in _labels)
            label.Text = UiStrings.Get(key);
        _stationTab.Text = UiStrings.Get("base.station.new_keys_header");
        _poiTab.Text = UiStrings.Get("base.station.tab_space_pois");
        _setClaimableBtn.Text = UiStrings.Get("base.station.set_claimable_values");
        _goToBaseBtn.Text = UiStrings.Get("base.station.go_to_base");
        new ToolTip().SetToolTip(_gotoStatsBtn, UiStrings.Format("goto_json.tooltip_section", UiStrings.Get("base.station.systems_header")));
        new ToolTip().SetToolTip(_gotoPackedBtn, UiStrings.Format("goto_json.tooltip_section", UiStrings.Get("base.station.poi_discoveries_header")));

        // Packed data editors: storage-type notes and input hint.
        UpdatePackedDataTypeLabels();
        _packedDataToolTip.SetToolTip(_packedData0Box, UiStrings.Get("base.station.packed_data_hint"));
        _packedDataToolTip.SetToolTip(_packedData1Box, UiStrings.Get("base.station.packed_data_hint"));

        // Refresh mission combo items with the localised state strings.
        var stateItems = new object[]
        {
            UiStrings.Get("base.station.mission_not_started"),
            UiStrings.Get("base.station.mission_active"),
            UiStrings.Get("base.station.mission_completed")
        };
        foreach (var item in _missionControls)
        {
            int selection = item.Combo.SelectedIndex;
            item.Combo.BeginUpdate();
            item.Combo.Items.Clear();
            item.Combo.Items.AddRange(stateItems);
            if (selection >= 0 && selection < stateItems.Length)
                item.Combo.SelectedIndex = selection;
            item.Combo.EndUpdate();
        }

        // Refresh packed slot combo items with the localised discovery levels and
        // update the per-slot tooltips.
        var levelItems = PackedLevelItems();
        foreach (var item in _packedSlotCombos)
        {
            int selection = item.Combo.SelectedIndex;
            item.Combo.BeginUpdate();
            item.Combo.Items.Clear();
            item.Combo.Items.AddRange(levelItems);
            if (selection >= 0 && selection < levelItems.Length)
                item.Combo.SelectedIndex = selection;
            item.Combo.EndUpdate();
            _packedDataToolTip.SetToolTip(item.Combo, UiStrings.Format(
                "base.station.packed_data_slot_tooltip",
                (item.Slot + 1).ToString(CultureInfo.InvariantCulture)));
        }

        // Rebuild the slot groups so the headers pick up the localised POI type names.
        RebuildPackedSlotGroups(_selectedPoiEntry is (_, JsonObject selectedPoi) ? selectedPoi : null);

        int selectedSystem = _systemList.SelectedIndex;
        if (selectedSystem >= 0)
        {
            _systemList.BeginUpdate();
            _systemList.Items.Clear();
            foreach (var system in _systems)
                _systemList.Items.Add(system);
            _systemList.SelectedIndex = selectedSystem;
            _systemList.EndUpdate();
        }
    }
}