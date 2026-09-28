#nullable enable
using NMSE.Core;
using NMSE.UI.Util;

namespace NMSE.UI.Panels;

partial class DatabaseSearchPanel
{
    private System.ComponentModel.IContainer? components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        this.SuspendLayout();
        // 
        // DatabaseSearchPanel
        // 
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.DoubleBuffered = true;
        this.ResumeLayout(false);
    }

    private void SetupLayout()
    {
        SuspendLayout();

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(8, 8, 8, 4)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // toolbar
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // hint
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // content
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // status

        // -- Toolbar --
        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight
        };

        _searchBox = new TextBox
        {
            Width = 280,
            PlaceholderText = "Search all items...",
            Margin = new Padding(0, 3, 3, 3)
        };
        _searchBox.TextChanged += (_, _) => ApplyFilter();

        _clearSearchButton = new Button
        {
            Text = "X",
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(30, 0),
            Margin = new Padding(0, 3, 3, 3)
        };
        _clearSearchButton.Click += (_, _) =>
        {
            _searchBox.Text = "";
            _searchBox.Focus();
        };

        _sourceLabel = new Label
        {
            Text = "Source:",
            AutoSize = true,
            Margin = new Padding(10, 6, 3, 0)
        };

        _sourceFilter = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 180,
            Margin = new Padding(0, 3, 3, 3)
        };
        _sourceFilter.SelectedIndexChanged += OnSourceFilterChanged;

        _countLabel = new Label
        {
            Text = "",
            AutoSize = true,
            Margin = new Padding(12, 6, 0, 0)
        };

        toolbar.Controls.AddRange([_searchBox, _clearSearchButton, _sourceLabel, _sourceFilter, _countLabel]);

        // -- Hint --
        _hintLabel = new Label
        {
            Text = "",
            AutoSize = true,
            Padding = new Padding(0, 0, 0, 4)
        };

        // -- Results grid --
        _resultsGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            VirtualMode = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            ReadOnly = true,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowTemplate = { Height = 28 }
        };
        _resultsGrid.Columns.Add(new DataGridViewImageColumn
        {
            Name = "Icon",
            HeaderText = "",
            Width = 36,
            ImageLayout = DataGridViewImageCellLayout.Zoom,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            ReadOnly = true
        });
        _resultsGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "Name", FillWeight = 40 });
        _resultsGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Category", HeaderText = "Category", FillWeight = 20 });
        _resultsGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ID", HeaderText = "ID", FillWeight = 20 });
        _resultsGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Source", HeaderText = "Source", FillWeight = 20 });
        _resultsGrid.CellValueNeeded += OnCellValueNeeded;
        _resultsGrid.SelectionChanged += OnResultSelectionChanged;

        // -- Details --
        _detailIcon = new PictureBox
        {
            Size = new Size(72, 72),
            SizeMode = PictureBoxSizeMode.Zoom,
            Margin = new Padding(0, 0, 8, 0)
        };

        _detailName = new Label
        {
            Text = "",
            AutoSize = true,
            Font = FontManager.CreateFont(12F, FontStyle.Bold),
            Margin = new Padding(0, 4, 0, 0)
        };

        _detailId = new Label
        {
            Text = "",
            AutoSize = true,
            ForeColor = ThemeManager.Effective == AppTheme.Dark
                ? ThemeColors.Dark.SecondaryText
                : Color.Gray,
            Margin = new Padding(0, 2, 0, 0)
        };

        var namePanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = false,
            FlowDirection = FlowDirection.TopDown,
            Margin = new Padding(0)
        };
        namePanel.Controls.AddRange([_detailName, _detailId]);

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 4)
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.Controls.Add(_detailIcon, 0, 0);
        header.Controls.Add(namePanel, 1, 0);

        _fieldsGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            ReadOnly = true,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.CellSelect,
            MultiSelect = true,
            AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
            Margin = new Padding(0)
        };
        _fieldsGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Field",
            HeaderText = "",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
            ReadOnly = true,
            DefaultCellStyle = { ForeColor = ThemeManager.Effective == AppTheme.Dark ? ThemeColors.Dark.SecondaryText : Color.Gray }
        });
        _fieldsGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Value",
            HeaderText = "",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            ReadOnly = true,
            DefaultCellStyle = { WrapMode = DataGridViewTriState.True }
        });

        _descriptionCaption = new Label
        {
            Text = "Description",
            AutoSize = true,
            Font = FontManager.CreateFont(9F, FontStyle.Bold),
            Margin = new Padding(0, 6, 0, 2)
        };

        _descriptionBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Height = 120,
            Margin = new Padding(0)
        };

        var detailPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(8, 4, 0, 0)
        };
        detailPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // header
        detailPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // fields
        detailPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // description caption
        detailPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 130));  // description
        detailPanel.Controls.Add(header, 0, 0);
        detailPanel.Controls.Add(_fieldsGrid, 0, 1);
        detailPanel.Controls.Add(_descriptionCaption, 0, 2);
        detailPanel.Controls.Add(_descriptionBox, 0, 3);

        _split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterWidth = 6
        };
        _split.Panel1.Controls.Add(_resultsGrid);
        _split.Panel2.Controls.Add(detailPanel);

        // -- Status --
        _statusLabel = new Label
        {
            Text = "",
            AutoSize = true,
            Padding = new Padding(0, 4, 0, 0)
        };

        root.Controls.Add(toolbar, 0, 0);
        root.Controls.Add(_hintLabel, 0, 1);
        root.Controls.Add(_split, 0, 2);
        root.Controls.Add(_statusLabel, 0, 3);

        Controls.Add(root);
        ResumeLayout(false);
        PerformLayout();
    }

    private TextBox _searchBox = null!;
    private Button _clearSearchButton = null!;
    private Label _sourceLabel = null!;
    private ComboBox _sourceFilter = null!;
    private Label _countLabel = null!;
    private Label _hintLabel = null!;
    private DataGridView _resultsGrid = null!;
    private PictureBox _detailIcon = null!;
    private Label _detailName = null!;
    private Label _detailId = null!;
    private DataGridView _fieldsGrid = null!;
    private Label _descriptionCaption = null!;
    private TextBox _descriptionBox = null!;
    private SplitContainer _split = null!;
    private Label _statusLabel = null!;
}
