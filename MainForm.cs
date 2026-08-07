using System.Diagnostics;

namespace BG3ItemExplorer;

public sealed class MainForm : Form
{
    private readonly List<ItemRecord> _allItems;
    private List<ItemRecord> _visibleItems = [];
    private readonly BindingSource _gridSource = new();
    private readonly ProgressStore _progressStore = new();
    private readonly ItemImageRepository _imageRepository = new();
    private readonly SplitContainer _mainSplit = new();
    private readonly SplitContainer _contentCharacterSplit = new();
    private readonly SplitContainer _rightSplit = new();
    private readonly Panel _headerPanel = new();
    private readonly Panel _footerPanel = new();
    private readonly TextBox _searchBox = new();
    private readonly ComboBox _languageBox = new();
    private readonly CheckedListBox _actList = new();
    private readonly CheckedListBox _rarityList = new();
    private readonly ComboBox _typeBox = new();
    private readonly ComboBox _placeBox = new();
    private readonly CheckBox _notesOnly = new();
    private readonly ComboBox _foundBox = new();
    private readonly ComboBox _sortBox = new();
    private readonly ComboBox _directionBox = new();
    private readonly Label _resultLabel = new();
    private readonly DataGridView _grid = new();
    private readonly Label _detailTitle = new();
    private readonly Label _detailMeta = new();
    private readonly RichTextBox _detailText = new();
    private readonly FlowLayoutPanel _linksPanel = new();
    private readonly PictureBox _itemPicture = new();
    private readonly CharacterState _characterState;
    private readonly CharacterSheetPanel _characterSheet;
    private bool _resizingSplit;
    private bool _applyingLanguage;

    public MainForm(List<ItemRecord> items)
    {
        _allItems = items;
        var progress = _progressStore.LoadState();
        _characterState = progress.Character;
        foreach (var item in _allItems)
        {
            item.Found = progress.FoundKeys.Contains(item.ProgressKey);
            item.Equipped = _characterState.EquippedKeys.Contains(item.ProgressKey, StringComparer.OrdinalIgnoreCase);
        }
        _characterSheet = new CharacterSheetPanel(_characterState, _allItems);
        Text = "BG3 Item Explorer";
        try
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch
        {
            // The executable still carries the application icon if extraction is unavailable.
        }
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1100, 700);
        var workingArea = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1480, 900);
        ClientSize = new Size(
            Math.Clamp(workingArea.Width - 80, 1100, 1480),
            Math.Clamp(workingArea.Height - 80, 700, 900));
        BackColor = Theme.Parchment;
        Font = Theme.Body();
        AutoScaleMode = AutoScaleMode.Dpi;

        BuildHeader();
        BuildFooter();
        BuildLayout();
        PopulateFilterChoices();
        WireEvents();
        ApplyLanguage();
        ApplyFilters();
    }

    public void RenderPreview(string path)
    {
        MaintainMainSplitRatio();
        PerformLayout();
        _mainSplit.PerformLayout();
        _rightSplit.PerformLayout();
        _grid.Refresh();
        using var bitmap = new Bitmap(ClientSize.Width, ClientSize.Height);
        DrawToBitmap(bitmap, new Rectangle(Point.Empty, ClientSize));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }

    public void RunFilterStressTest(string reportPath)
    {
        var iterations = 0;
        string? failure = null;
        try
        {
            var searches = new[] { "", "a", "shield", "absolute", "lower city", "zzzz-no-result", "ring", "", "cloak", "act 3", "" };
            foreach (var search in searches)
            {
                if (_grid.Rows.Count > 301)
                    _grid.CurrentCell = _grid.Rows[301].Cells[0];
                _searchBox.Text = search;
                Application.DoEvents();
                iterations++;
            }

            for (var index = 0; index < _sortBox.Items.Count; index++)
            {
                _sortBox.SelectedIndex = index;
                _directionBox.SelectedIndex = index % 2;
                Application.DoEvents();
                iterations++;
            }

            for (var index = 0; index < _foundBox.Items.Count; index++)
            {
                _foundBox.SelectedIndex = index;
                Application.DoEvents();
                iterations++;
            }

            _foundBox.SelectedIndex = 0;
            for (var index = 0; index < _actList.Items.Count; index++)
            {
                _actList.SetItemChecked(index, false);
                Application.DoEvents();
                _actList.SetItemChecked(index, true);
                Application.DoEvents();
                iterations += 2;
            }
        }
        catch (Exception exception)
        {
            failure = exception.ToString();
        }
        finally
        {
            ResetFilters();
        }

        var report = new
        {
            Passed = failure is null,
            Iterations = iterations,
            FinalVisibleItems = _visibleItems.Count,
            Failure = failure
        };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllText(reportPath, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }

    private void BuildFooter()
    {
        _footerPanel.Dock = DockStyle.Bottom;
        _footerPanel.Height = 27;
        _footerPanel.BackColor = Theme.CrimsonDark;
        _footerPanel.Padding = new Padding(14, 4, 14, 3);
        var sourceLink = new LinkLabel
        {
            Text = "Data & afbeeldingen: bg3.wiki",
            AutoSize = true,
            Dock = DockStyle.Left,
            Font = Theme.Body(7.5f, FontStyle.Bold),
            LinkColor = Theme.GoldLight,
            ActiveLinkColor = Color.White,
            VisitedLinkColor = Theme.GoldLight,
            Cursor = Cursors.Hand
        };
        sourceLink.Text = Localization.T("Source");
        sourceLink.Tag = "i18n:Source";
        sourceLink.LinkClicked += (_, _) => OpenUrl("https://bg3.wiki/");
        var license = new Label
        {
            Text = "Niet-commerciële fan-tool • voortgang wordt naast de exe opgeslagen",
            AutoSize = true,
            Dock = DockStyle.Right,
            ForeColor = Color.FromArgb(216, 198, 163),
            Font = Theme.Body(7.5f, FontStyle.Italic)
        };
        license.Text = Localization.T("Footer");
        license.Tag = "i18n:Footer";
        _footerPanel.Controls.Add(license);
        _footerPanel.Controls.Add(sourceLink);
        Controls.Add(_footerPanel);
    }

    private void BuildHeader()
    {
        _headerPanel.Dock = DockStyle.Top;
        _headerPanel.Height = 72;
        _headerPanel.BackColor = Theme.CrimsonDark;
        _headerPanel.Padding = new Padding(20, 8, 20, 8);
        var title = new Label
        {
            Text = "BALDUR'S GATE 3  •  ITEM EXPLORER",
            Dock = DockStyle.Top,
            Height = 34,
            ForeColor = Theme.GoldLight,
            Font = Theme.Heading(19f),
            TextAlign = ContentAlignment.MiddleLeft
        };
        var subtitle = new Label
        {
            Text = "Act 1, 2 & 3 • offline • volledige iteminformatie • data & afbeeldingen: bg3.wiki",
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(223, 205, 170),
            Font = Theme.Body(9f, FontStyle.Italic),
            TextAlign = ContentAlignment.MiddleLeft
        };
        subtitle.Text = Localization.T("Subtitle");
        subtitle.Tag = "i18n:Subtitle";
        _headerPanel.Controls.Add(subtitle);
        _headerPanel.Controls.Add(title);
        Controls.Add(_headerPanel);
    }

    private void BuildLayout()
    {
        _mainSplit.Dock = DockStyle.None;
        _mainSplit.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _mainSplit.Orientation = Orientation.Vertical;
        _mainSplit.BackColor = Theme.Gold;
        _mainSplit.SplitterWidth = 5;
        Controls.Add(_mainSplit);
        LayoutContentArea();

        BuildFilterPanel(_mainSplit.Panel1);
        BuildRightPanel(_mainSplit.Panel2);
    }

    private void BuildFilterPanel(Control host)
    {
        host.BackColor = Theme.ParchmentAlt;
        host.Padding = new Padding(14, 12, 14, 10);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 21,
            BackColor = Theme.ParchmentAlt
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var index = 0; index < 20; index++)
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var filterTitle = new Label
        {
            Text = Localization.T("FiltersTitle"),
            Tag = "i18n:FiltersTitle",
            UseMnemonic = false,
            AutoSize = true,
            ForeColor = Theme.Crimson,
            Font = Theme.Heading(12f),
            Margin = new Padding(0, 0, 0, 10)
        };
        layout.Controls.Add(filterTitle);

        AddLocalizedCaption(layout, "Language");
        ConfigureFilterCombo(_languageBox);
        _languageBox.Items.AddRange(["English", "Nederlands"]);
        _languageBox.SelectedIndex = Localization.Current == UiLanguage.Dutch ? 1 : 0;
        layout.Controls.Add(_languageBox);

        AddLocalizedCaption(layout, "SearchAll");
        _searchBox.PlaceholderText = Localization.T("SearchPlaceholder");
        _searchBox.Dock = DockStyle.Top;
        _searchBox.Font = Theme.Body(10f);
        _searchBox.Margin = new Padding(0, 0, 0, 10);
        layout.Controls.Add(_searchBox);

        AddLocalizedCaption(layout, "Acts");
        _actList.Dock = DockStyle.Top;
        _actList.Height = 76;
        _actList.CheckOnClick = true;
        _actList.BackColor = Theme.Parchment;
        _actList.BorderStyle = BorderStyle.FixedSingle;
        layout.Controls.Add(_actList);

        AddLocalizedCaption(layout, "Rarity");
        _rarityList.Dock = DockStyle.Top;
        _rarityList.Height = 100;
        _rarityList.CheckOnClick = true;
        _rarityList.BackColor = Theme.Parchment;
        _rarityList.BorderStyle = BorderStyle.FixedSingle;
        layout.Controls.Add(_rarityList);

        AddLocalizedCaption(layout, "Type");
        ConfigureFilterCombo(_typeBox);
        layout.Controls.Add(_typeBox);

        AddLocalizedCaption(layout, "Place");
        ConfigureFilterCombo(_placeBox);
        layout.Controls.Add(_placeBox);

        _notesOnly.Text = Localization.T("NotesOnly");
        _notesOnly.Tag = "i18n:NotesOnly";
        _notesOnly.AutoSize = true;
        _notesOnly.ForeColor = Theme.Ink;
        _notesOnly.Margin = new Padding(0, 10, 0, 8);
        layout.Controls.Add(_notesOnly);

        AddLocalizedCaption(layout, "Progress");
        ConfigureFilterCombo(_foundBox);
        layout.Controls.Add(_foundBox);

        AddLocalizedCaption(layout, "Sorting");
        var sortPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 33,
            ColumnCount = 2,
            Margin = new Padding(0, 0, 0, 10)
        };
        sortPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
        sortPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
        ConfigureFilterCombo(_sortBox);
        ConfigureFilterCombo(_directionBox);
        _sortBox.Margin = new Padding(0, 0, 4, 0);
        _directionBox.Margin = new Padding(4, 0, 0, 0);
        sortPanel.Controls.Add(_sortBox, 0, 0);
        sortPanel.Controls.Add(_directionBox, 1, 0);
        layout.Controls.Add(sortPanel);

        var resetButton = new Button
        {
            Text = Localization.T("Reset"),
            Tag = "i18n:Reset",
            Dock = DockStyle.Top,
            Height = 34,
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.Crimson,
            ForeColor = Color.White,
            Font = Theme.Body(9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 0, 8)
        };
        resetButton.FlatAppearance.BorderColor = Theme.Gold;
        resetButton.Click += (_, _) => ResetFilters();
        layout.Controls.Add(resetButton);

        _resultLabel.AutoSize = true;
        _resultLabel.ForeColor = Theme.Muted;
        _resultLabel.Font = Theme.Body(8.5f, FontStyle.Italic);
        _resultLabel.Margin = new Padding(0, 6, 0, 0);
        layout.Controls.Add(_resultLabel);
        layout.Controls.Add(new Panel { Dock = DockStyle.Fill });
        host.Controls.Add(layout);
    }

    private void BuildFilterPanelLegacy(Control host)
    {
        host.BackColor = Theme.ParchmentAlt;
        host.Padding = new Padding(14, 12, 14, 10);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 17,
            BackColor = Theme.ParchmentAlt
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var i = 0; i < 16; i++)
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var filterTitle = new Label
        {
            Text = "ZOEKEN & FILTEREN",
            UseMnemonic = false,
            AutoSize = true,
            ForeColor = Theme.Crimson,
            Font = Theme.Heading(12f),
            Margin = new Padding(0, 0, 0, 10)
        };
        layout.Controls.Add(filterTitle);

        AddCaption(layout, "Zoek in alle velden");
        _searchBox.PlaceholderText = "Naam, effect, locatie...";
        _searchBox.Dock = DockStyle.Top;
        _searchBox.Font = Theme.Body(10f);
        _searchBox.Margin = new Padding(0, 0, 0, 10);
        layout.Controls.Add(_searchBox);

        AddCaption(layout, "Acts");
        _actList.Dock = DockStyle.Top;
        _actList.Height = 68;
        _actList.CheckOnClick = true;
        _actList.BackColor = Theme.Parchment;
        _actList.BorderStyle = BorderStyle.FixedSingle;
        layout.Controls.Add(_actList);

        AddCaption(layout, "Zeldzaamheid");
        _rarityList.Dock = DockStyle.Top;
        _rarityList.Height = 92;
        _rarityList.CheckOnClick = true;
        _rarityList.BackColor = Theme.Parchment;
        _rarityList.BorderStyle = BorderStyle.FixedSingle;
        layout.Controls.Add(_rarityList);

        AddCaption(layout, "Type");
        ConfigureFilterCombo(_typeBox);
        layout.Controls.Add(_typeBox);

        AddCaption(layout, "Gebied of locatie");
        ConfigureFilterCombo(_placeBox);
        layout.Controls.Add(_placeBox);

        _notesOnly.Text = "Alleen items met een notitie";
        _notesOnly.AutoSize = true;
        _notesOnly.ForeColor = Theme.Ink;
        _notesOnly.Margin = new Padding(0, 10, 0, 8);
        layout.Controls.Add(_notesOnly);

        AddCaption(layout, "Voortgang");
        ConfigureFilterCombo(_foundBox);
        layout.Controls.Add(_foundBox);

        AddCaption(layout, "Sorteren");
        var sortPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 33,
            ColumnCount = 2,
            Margin = new Padding(0, 0, 0, 10)
        };
        sortPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
        sortPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
        ConfigureFilterCombo(_sortBox);
        ConfigureFilterCombo(_directionBox);
        _sortBox.Margin = new Padding(0, 0, 4, 0);
        _directionBox.Margin = new Padding(4, 0, 0, 0);
        sortPanel.Controls.Add(_sortBox, 0, 0);
        sortPanel.Controls.Add(_directionBox, 1, 0);
        layout.Controls.Add(sortPanel);

        var resetButton = new Button
        {
            Text = "Filters wissen",
            Dock = DockStyle.Top,
            Height = 34,
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.Crimson,
            ForeColor = Color.White,
            Font = Theme.Body(9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 0, 8)
        };
        resetButton.FlatAppearance.BorderColor = Theme.Gold;
        resetButton.Click += (_, _) => ResetFilters();
        layout.Controls.Add(resetButton);

        _resultLabel.AutoSize = true;
        _resultLabel.ForeColor = Theme.Muted;
        _resultLabel.Font = Theme.Body(8.5f, FontStyle.Italic);
        _resultLabel.Margin = new Padding(0, 6, 0, 0);
        layout.Controls.Add(_resultLabel);
        host.Controls.Add(layout);
    }

    private static void AddCaption(TableLayoutPanel layout, string text)
    {
        layout.Controls.Add(new Label
        {
            Text = text,
            AutoSize = true,
            ForeColor = Theme.Muted,
            Font = Theme.Body(8.5f, FontStyle.Bold),
            Margin = new Padding(0, 5, 0, 3)
        });
    }

    private static void AddLocalizedCaption(TableLayoutPanel layout, string key)
    {
        layout.Controls.Add(new Label
        {
            Text = Localization.T(key),
            Tag = "i18n:" + key,
            AutoSize = true,
            ForeColor = Theme.Muted,
            Font = Theme.Body(8.5f, FontStyle.Bold),
            Margin = new Padding(0, 5, 0, 3)
        });
    }

    private static void ConfigureFilterCombo(ComboBox combo)
    {
        combo.Dock = DockStyle.Top;
        combo.DropDownStyle = ComboBoxStyle.DropDownList;
        combo.FlatStyle = FlatStyle.Flat;
        combo.BackColor = Theme.Parchment;
        combo.ForeColor = Theme.Ink;
        combo.Margin = new Padding(0, 0, 0, 8);
    }

    private void BuildRightPanel(Control host)
    {
        _contentCharacterSplit.Dock = DockStyle.Fill;
        _contentCharacterSplit.Orientation = Orientation.Vertical;
        _contentCharacterSplit.SplitterWidth = 5;
        _contentCharacterSplit.BackColor = Theme.Gold;
        host.Controls.Add(_contentCharacterSplit);

        _rightSplit.Dock = DockStyle.Fill;
        _rightSplit.Orientation = Orientation.Horizontal;
        _rightSplit.SplitterWidth = 5;
        _rightSplit.BackColor = Theme.Gold;
        _contentCharacterSplit.Panel1.Controls.Add(_rightSplit);
        _contentCharacterSplit.Panel2.Controls.Add(_characterSheet);

        BuildGrid(_rightSplit.Panel1);
        BuildDetails(_rightSplit.Panel2);
    }

    private void BuildGrid(Control host)
    {
        _grid.Dock = DockStyle.Fill;
        _grid.AutoGenerateColumns = false;
        _gridSource.DataSource = _visibleItems;
        _grid.DataSource = _gridSource;
        _grid.ReadOnly = false;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.MultiSelect = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.RowHeadersVisible = false;
        _grid.BackgroundColor = Theme.Parchment;
        _grid.BorderStyle = BorderStyle.None;
        _grid.GridColor = Theme.Grid;
        _grid.EnableHeadersVisualStyles = false;
        _grid.ColumnHeadersHeight = 40;
        _grid.RowTemplate.Height = 44;
        _grid.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Theme.Parchment,
            ForeColor = Theme.Ink,
            SelectionBackColor = Color.FromArgb(222, 202, 162),
            SelectionForeColor = Theme.Ink,
            Font = Theme.Body(8.5f),
            WrapMode = DataGridViewTriState.True,
            Padding = new Padding(4, 2, 4, 2)
        };
        _grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(255, 248, 233);
        _grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Theme.CrimsonDark,
            ForeColor = Color.White,
            SelectionBackColor = Theme.CrimsonDark,
            SelectionForeColor = Color.White,
            Font = Theme.Body(8.5f, FontStyle.Bold),
            Alignment = DataGridViewContentAlignment.MiddleLeft
        };

        _grid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            HeaderText = "✓",
            Name = "Found",
            Width = 44,
            MinimumWidth = 44,
            Frozen = true,
            ReadOnly = true,
            ToolTipText = "Gevonden / opgehaald"
        });
        _grid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            HeaderText = "⚔",
            Name = "Equipped",
            Width = 48,
            MinimumWidth = 48,
            Frozen = true,
            ReadOnly = true,
            ToolTipText = "Equipped on character"
        });
        AddColumn("Act", nameof(ItemRecord.Act), 62, frozen: true);
        AddColumn("Name", nameof(ItemRecord.Name), 185, frozen: true);
        AddColumn("Rarity", nameof(ItemRecord.Rarity), 85);
        AddColumn("Type", nameof(ItemRecord.Type), 110);
        AddColumn("Properties", nameof(ItemRecord.Properties), 170);
        AddColumn("Act Area", nameof(ItemRecord.ActArea), 130);
        AddColumn("Location", nameof(ItemRecord.Location), 235);
        AddColumn("Description", nameof(ItemRecord.Description), 520);
        AddColumn("Notes", nameof(ItemRecord.NotesText), 300);
        host.Controls.Add(_grid);
    }

    private void AddColumn(string title, string property, int width, bool frozen = false)
    {
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = title,
            DataPropertyName = property,
            Name = property,
            Width = width,
            MinimumWidth = Math.Min(width, 60),
            SortMode = DataGridViewColumnSortMode.NotSortable,
            Frozen = frozen,
            ReadOnly = true
        });
    }

    private void BuildDetails(Control host)
    {
        var detail = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.ParchmentAlt,
            Padding = new Padding(16, 10, 16, 10),
            ColumnCount = 2,
            RowCount = 1
        };
        detail.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 182));
        detail.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var imagePanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.ParchmentAlt, Padding = new Padding(2, 4, 14, 4) };
        _itemPicture.Dock = DockStyle.Top;
        _itemPicture.Height = 164;
        _itemPicture.SizeMode = PictureBoxSizeMode.Zoom;
        _itemPicture.BackColor = Color.FromArgb(238, 222, 190);
        _itemPicture.BorderStyle = BorderStyle.FixedSingle;
        imagePanel.Controls.Add(_itemPicture);

        var textPanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.ParchmentAlt };
        _detailTitle.Dock = DockStyle.Top;
        _detailTitle.Height = 30;
        _detailTitle.Font = Theme.Heading(15f);
        _detailTitle.ForeColor = Theme.Crimson;
        _detailTitle.TextAlign = ContentAlignment.MiddleLeft;

        _detailMeta.Dock = DockStyle.Top;
        _detailMeta.Height = 24;
        _detailMeta.Font = Theme.Body(9f, FontStyle.Italic);
        _detailMeta.ForeColor = Theme.Muted;

        _linksPanel.Dock = DockStyle.Top;
        _linksPanel.Height = 32;
        _linksPanel.FlowDirection = FlowDirection.LeftToRight;
        _linksPanel.WrapContents = false;
        _linksPanel.BackColor = Theme.ParchmentAlt;

        _detailText.Dock = DockStyle.Fill;
        _detailText.ReadOnly = true;
        _detailText.BorderStyle = BorderStyle.None;
        _detailText.BackColor = Theme.ParchmentAlt;
        _detailText.ForeColor = Theme.Ink;
        _detailText.Font = Theme.Body(10f);
        _detailText.DetectUrls = true;
        _detailText.LinkClicked += (_, eventArgs) =>
        {
            if (!string.IsNullOrWhiteSpace(eventArgs.LinkText))
                OpenUrl(eventArgs.LinkText);
        };

        textPanel.Controls.Add(_detailText);
        textPanel.Controls.Add(_linksPanel);
        textPanel.Controls.Add(_detailMeta);
        textPanel.Controls.Add(_detailTitle);
        detail.Controls.Add(imagePanel, 0, 0);
        detail.Controls.Add(textPanel, 1, 0);
        host.Controls.Add(detail);
    }

    private void PopulateFilterChoices()
    {
        foreach (var act in new[] { "ACT 1", "ACT 2", "ACT 3" })
            _actList.Items.Add(act, true);

        foreach (var rarity in new[] { "Uncommon", "Rare", "Very Rare", "Legendary" })
            _rarityList.Items.Add(rarity, true);

        _typeBox.Items.Add(Localization.T("AllTypes"));
        foreach (var type in _allItems.Select(item => item.Type).Where(value => value.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value))
            _typeBox.Items.Add(type);
        _typeBox.SelectedIndex = 0;

        _placeBox.Items.Add(Localization.T("AllPlaces"));
        foreach (var place in _allItems.SelectMany(item => new[] { item.ActArea, item.Location }).Where(value => value.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value))
            _placeBox.Items.Add(place);
        _placeBox.SelectedIndex = 0;

        RebuildLocalizedComboItems();
    }

    private void PopulateFilterChoicesLegacy()
    {
        foreach (var act in new[] { "ACT 1", "ACT 2", "ACT 3" })
            _actList.Items.Add(act, true);

        foreach (var rarity in new[] { "Uncommon", "Rare", "Very Rare", "Legendary" })
            _rarityList.Items.Add(rarity, true);

        _typeBox.Items.Add("Alle types");
        foreach (var type in _allItems.Select(item => item.Type).Where(value => value.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value))
            _typeBox.Items.Add(type);
        _typeBox.SelectedIndex = 0;

        _placeBox.Items.Add("Alle gebieden/locaties");
        foreach (var place in _allItems.SelectMany(item => new[] { item.ActArea, item.Location }).Where(value => value.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value))
            _placeBox.Items.Add(place);
        _placeBox.SelectedIndex = 0;

        _foundBox.Items.AddRange(["Alle items", "Nog zoeken", "Gevonden"]);
        _foundBox.SelectedIndex = 0;

        _sortBox.Items.AddRange(["Name", "Rarity", "Type", "Act", "Location", "Status"]);
        _sortBox.SelectedIndex = 0;
        _directionBox.Items.AddRange(["A → Z", "Z → A"]);
        _directionBox.SelectedIndex = 0;
    }

    private void RebuildLocalizedComboItems()
    {
        var foundIndex = Math.Max(0, _foundBox.SelectedIndex);
        var sortIndex = Math.Max(0, _sortBox.SelectedIndex);
        var directionIndex = Math.Max(0, _directionBox.SelectedIndex);

        _foundBox.Items.Clear();
        _foundBox.Items.AddRange([Localization.T("AllItems"), Localization.T("NotFound"), Localization.T("Found")]);
        _foundBox.SelectedIndex = Math.Min(foundIndex, _foundBox.Items.Count - 1);

        _sortBox.Items.Clear();
        _sortBox.Items.AddRange([
            Localization.T("SortName"),
            Localization.T("SortRarity"),
            Localization.T("SortType"),
            Localization.T("SortAct"),
            Localization.T("SortLocation"),
            Localization.T("SortStatus")]);
        _sortBox.SelectedIndex = Math.Min(sortIndex, _sortBox.Items.Count - 1);

        _directionBox.Items.Clear();
        _directionBox.Items.AddRange([Localization.T("Ascending"), Localization.T("Descending")]);
        _directionBox.SelectedIndex = Math.Min(directionIndex, _directionBox.Items.Count - 1);
    }

    private void ApplyLanguage()
    {
        if (_applyingLanguage)
            return;

        _applyingLanguage = true;
        try
        {
            UpdateLocalizedControls(this);
            _searchBox.PlaceholderText = Localization.T("SearchPlaceholder");
            if (_typeBox.Items.Count > 0)
                _typeBox.Items[0] = Localization.T("AllTypes");
            if (_placeBox.Items.Count > 0)
                _placeBox.Items[0] = Localization.T("AllPlaces");
            RebuildLocalizedComboItems();

            _grid.Columns["Found"]!.HeaderText = Localization.T("GridFound");
            _grid.Columns["Found"]!.ToolTipText = Localization.T("FoundTooltip");
            _grid.Columns["Equipped"]!.HeaderText = Localization.T("GridEquipped");
            _grid.Columns["Equipped"]!.ToolTipText = Localization.T("EquippedTooltip");
            _grid.Columns[nameof(ItemRecord.Name)]!.HeaderText = Localization.T("GridName");
            _grid.Columns[nameof(ItemRecord.Rarity)]!.HeaderText = Localization.T("GridRarity");
            _grid.Columns[nameof(ItemRecord.Type)]!.HeaderText = Localization.T("GridType");
            _grid.Columns[nameof(ItemRecord.Properties)]!.HeaderText = Localization.T("GridProperties");
            _grid.Columns[nameof(ItemRecord.ActArea)]!.HeaderText = Localization.T("GridActArea");
            _grid.Columns[nameof(ItemRecord.Location)]!.HeaderText = Localization.T("GridLocation");
            _grid.Columns[nameof(ItemRecord.Description)]!.HeaderText = Localization.T("GridDescription");
            _grid.Columns[nameof(ItemRecord.NotesText)]!.HeaderText = Localization.T("GridNotes");
            _characterSheet.SetLanguage();
        }
        finally
        {
            _applyingLanguage = false;
        }

        ApplyFilters();
    }

    private static void UpdateLocalizedControls(Control root)
    {
        foreach (Control control in root.Controls)
        {
            if (control.Tag is string tag && tag.StartsWith("i18n:", StringComparison.Ordinal))
                control.Text = Localization.T(tag[5..]);
            if (control.HasChildren)
                UpdateLocalizedControls(control);
        }
    }

    private void WireEvents()
    {
        _languageBox.SelectedIndexChanged += (_, _) =>
        {
            if (_applyingLanguage)
                return;
            Localization.Current = _languageBox.SelectedIndex == 1 ? UiLanguage.Dutch : UiLanguage.English;
            ApplyLanguage();
        };
        _searchBox.TextChanged += (_, _) => ApplyFilters();
        _actList.ItemCheck += (_, _) => BeginInvoke((Action)ApplyFilters);
        _rarityList.ItemCheck += (_, _) => BeginInvoke((Action)ApplyFilters);
        _typeBox.SelectedIndexChanged += (_, _) => ApplyFilters();
        _placeBox.SelectedIndexChanged += (_, _) => ApplyFilters();
        _notesOnly.CheckedChanged += (_, _) => ApplyFilters();
        _foundBox.SelectedIndexChanged += (_, _) => ApplyFilters();
        _sortBox.SelectedIndexChanged += (_, _) => ApplyFilters();
        _directionBox.SelectedIndexChanged += (_, _) => ApplyFilters();
        _characterSheet.StateChanged += (_, _) => SaveProgressWithWarning();
        _grid.SelectionChanged += (_, _) =>
        {
            ShowSelectedItem();
            _grid.Invalidate();
        };
        _grid.CellDoubleClick += (_, eventArgs) =>
        {
            if (eventArgs.RowIndex >= 0 && _grid.Rows[eventArgs.RowIndex].DataBoundItem is ItemRecord item)
                OpenBestLink(item);
        };
        _grid.CellFormatting += GridOnCellFormatting;
        _grid.RowPostPaint += GridOnRowPostPaint;
        _grid.CellContentClick += GridOnCellContentClick;
        _grid.KeyDown += GridOnKeyDown;
        Resize += (_, _) =>
        {
            LayoutContentArea();
            MaintainMainSplitRatio();
        };
        Shown += (_, _) =>
        {
            LayoutContentArea();
            MaintainMainSplitRatio();
        };
        FormClosed += (_, _) =>
        {
            _itemPicture.Image?.Dispose();
            _imageRepository.Dispose();
        };
    }

    private void MaintainMainSplitRatio()
    {
        if (_resizingSplit || _mainSplit.Width < 1000)
            return;
        try
        {
            _resizingSplit = true;
            _mainSplit.SplitterDistance = Math.Clamp((int)(_mainSplit.Width * 0.20), 250, 360);
            if (_contentCharacterSplit.Width > 850)
            {
                var maximum = _contentCharacterSplit.Width - 220 - _contentCharacterSplit.SplitterWidth;
                _contentCharacterSplit.SplitterDistance = Math.Clamp((int)(_contentCharacterSplit.Width * 0.75), 600, maximum);
            }
            _rightSplit.SplitterDistance = Math.Clamp((int)(_rightSplit.Height * 0.65), 360, _rightSplit.Height - 200);
        }
        finally
        {
            _resizingSplit = false;
        }
    }

    private void LayoutContentArea()
    {
        PerformLayout();
        var top = _headerPanel.Bottom;
        var bottom = _footerPanel.Top > top ? _footerPanel.Top : ClientSize.Height - _footerPanel.Height;
        _mainSplit.SetBounds(0, top, ClientSize.Width, Math.Max(100, bottom - top));
    }

    private void ApplyFilters()
    {
        var acts = _actList.CheckedItems.Cast<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rarities = _rarityList.CheckedItems.Cast<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var type = _typeBox.SelectedIndex > 0 ? _typeBox.SelectedItem as string : null;
        var place = _placeBox.SelectedIndex > 0 ? _placeBox.SelectedItem as string : null;
        var terms = _searchBox.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        IEnumerable<ItemRecord> query = _allItems.Where(item =>
            acts.Contains(item.Act) &&
            rarities.Contains(item.Rarity) &&
            (string.IsNullOrEmpty(type) || string.Equals(item.Type, type, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrEmpty(place) ||
                string.Equals(item.ActArea, place, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.Location, place, StringComparison.OrdinalIgnoreCase)) &&
            (!_notesOnly.Checked || item.Notes.Count > 0) &&
            (_foundBox.SelectedIndex == 0 ||
                (_foundBox.SelectedIndex == 2 && item.Found) ||
                (_foundBox.SelectedIndex == 1 && !item.Found)) &&
            terms.All(term => item.SearchText.Contains(term, StringComparison.OrdinalIgnoreCase)));

        query = _sortBox.SelectedIndex switch
        {
            1 => query.OrderBy(item => RarityRank(item.Rarity)).ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase),
            2 => query.OrderBy(item => item.Type, StringComparer.OrdinalIgnoreCase).ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase),
            3 => query.OrderBy(item => item.Act, StringComparer.OrdinalIgnoreCase).ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase),
            4 => query.OrderBy(item => item.Location, StringComparer.OrdinalIgnoreCase).ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase),
            5 => query.OrderByDescending(item => item.Found).ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase),
            _ => query.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
        };

        if (_directionBox.SelectedIndex == 1)
            query = query.Reverse();

        var selectedName = _grid.CurrentRow?.DataBoundItem is ItemRecord selected ? selected.Name : null;
        var filteredItems = query.ToList();

        // WinForms CurrencyManager retains the previous row position while a bound
        // BindingList is cleared. Detaching before swapping the completed list keeps
        // that transient position from indexing into an empty or shorter collection.
        _grid.SuspendLayout();
        try
        {
            _grid.DataSource = null;
            _gridSource.DataSource = null;
            _visibleItems = filteredItems;
            _gridSource.DataSource = _visibleItems;
            _grid.DataSource = _gridSource;
        }
        finally
        {
            _grid.ResumeLayout();
        }

        _resultLabel.Text = Localization.Format("ResultCount", _visibleItems.Count, _allItems.Count(item => item.Found), _allItems.Count);
        if (_visibleItems.Count == 0)
        {
            ClearDetails(Localization.T("NoItems"));
            return;
        }

        var rowToSelect = selectedName is null
            ? 0
            : Math.Max(0, _visibleItems.FindIndex(item => item.Name == selectedName));
        _grid.ClearSelection();
        if (rowToSelect < _grid.Rows.Count)
        {
            _grid.Rows[rowToSelect].Selected = true;
            _grid.CurrentCell = _grid.Rows[rowToSelect].Cells[0];
        }
        ShowSelectedItem();
    }

    private static int RarityRank(string rarity) => rarity switch
    {
        "Uncommon" => 1,
        "Rare" => 2,
        "Very Rare" => 3,
        "Legendary" => 4,
        _ => 0
    };

    private void GridOnCellFormatting(object? sender, DataGridViewCellFormattingEventArgs eventArgs)
    {
        if (eventArgs.RowIndex < 0 || _grid.Rows[eventArgs.RowIndex].DataBoundItem is not ItemRecord item)
            return;
        if (_grid.Columns[eventArgs.ColumnIndex].DataPropertyName == nameof(ItemRecord.Rarity))
        {
            eventArgs.CellStyle.BackColor = Theme.RarityBackground(item.Rarity);
            eventArgs.CellStyle.ForeColor = Theme.RarityForeground(item.Rarity);
        }
        if (_grid.Columns[eventArgs.ColumnIndex].Name == "Found")
        {
            eventArgs.Value = item.Found;
            eventArgs.FormattingApplied = true;
        }
        if (_grid.Columns[eventArgs.ColumnIndex].Name == "Equipped")
        {
            eventArgs.Value = item.Equipped;
            eventArgs.FormattingApplied = true;
        }

        // Preserve each column's semantic colours when the row is selected.
        // Selection is shown with a gold outline in RowPostPaint instead.
        eventArgs.CellStyle.SelectionBackColor = eventArgs.CellStyle.BackColor;
        eventArgs.CellStyle.SelectionForeColor = eventArgs.CellStyle.ForeColor;
    }

    private void GridOnRowPostPaint(object? sender, DataGridViewRowPostPaintEventArgs eventArgs)
    {
        if (!_grid.Rows[eventArgs.RowIndex].Selected)
            return;

        var bounds = eventArgs.RowBounds;
        bounds.Width = Math.Min(bounds.Width, _grid.ClientSize.Width - bounds.X - 1);
        bounds.Height = Math.Max(1, bounds.Height - 1);
        using var pen = new Pen(Theme.Gold, 2f);
        eventArgs.Graphics.DrawRectangle(pen, bounds);
    }

    private void ShowSelectedItem()
    {
        if (_grid.CurrentRow?.DataBoundItem is not ItemRecord item)
            return;

        _detailTitle.Text = item.Name;
        _detailMeta.Text = $"{item.Act}  •  {item.Rarity}  •  {item.Type}";
        _detailMeta.ForeColor = Theme.RarityForeground(item.Rarity);

        _detailText.Clear();
        AppendDetail(Localization.T("Properties"), item.Properties);
        if (!string.IsNullOrWhiteSpace(item.ActArea))
            AppendDetail(Localization.T("ActArea"), item.ActArea);
        AppendDetail(Localization.T("Location"), item.Location);
        AppendDetail(Localization.T("Description"), item.Description);
        if (item.Notes.Count > 0)
            AppendDetail(Localization.T("Notes"), item.NotesText, Theme.Crimson);

        var previousImage = _itemPicture.Image;
        _itemPicture.Image = _imageRepository.Load(item.ImageKey) ?? CreatePlaceholderImage(item.Name);
        previousImage?.Dispose();

        _linksPanel.SuspendLayout();
        _linksPanel.Controls.Clear();
        var foundButton = new Button
        {
            Text = item.Found ? Localization.T("FoundButton") : Localization.T("MarkFound"),
            AutoSize = true,
            Height = 27,
            FlatStyle = FlatStyle.Flat,
            BackColor = item.Found ? Color.FromArgb(75, 112, 66) : Theme.Crimson,
            ForeColor = Color.White,
            Font = Theme.Body(8f, FontStyle.Bold),
            Tag = item,
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 2, 16, 0)
        };
        foundButton.FlatAppearance.BorderColor = Theme.Gold;
        foundButton.Click += (_, _) => ToggleFound((ItemRecord)foundButton.Tag);
        _linksPanel.Controls.Add(foundButton);
        foreach (var link in item.Links.Where(pair => Uri.IsWellFormedUriString(pair.Value, UriKind.Absolute)))
        {
            var linkLabel = new LinkLabel
            {
                Text = Localization.Format("OpenLink", link.Key),
                AutoSize = true,
                Font = Theme.Body(8.5f, FontStyle.Bold),
                LinkColor = Theme.Crimson,
                ActiveLinkColor = Theme.Gold,
                VisitedLinkColor = Theme.CrimsonDark,
                Tag = link.Value,
                Margin = new Padding(0, 5, 18, 0),
                Cursor = Cursors.Hand
            };
            linkLabel.LinkClicked += (_, _) => OpenUrl((string)linkLabel.Tag);
            _linksPanel.Controls.Add(linkLabel);
        }
        if (item.Links.Count == 0)
        {
            _linksPanel.Controls.Add(new Label
            {
                Text = Localization.T("NoLink"),
                AutoSize = true,
                ForeColor = Theme.Muted,
                Font = Theme.Body(8f, FontStyle.Italic),
                Margin = new Padding(0, 6, 0, 0)
            });
        }
        _linksPanel.ResumeLayout();
    }

    private void AppendDetail(string heading, string value, Color? valueColor = null)
    {
        if (string.IsNullOrWhiteSpace(value))
            value = "—";
        _detailText.SelectionFont = Theme.Body(8.5f, FontStyle.Bold);
        _detailText.SelectionColor = Theme.Crimson;
        _detailText.AppendText(heading + Environment.NewLine);
        _detailText.SelectionFont = Theme.Body(9.5f);
        _detailText.SelectionColor = valueColor ?? Theme.Ink;
        _detailText.AppendText(value.Trim() + Environment.NewLine + Environment.NewLine);
    }

    private void ClearDetails(string title)
    {
        _detailTitle.Text = title;
        _detailMeta.Text = Localization.T("AdjustFilters");
        _detailText.Clear();
        _linksPanel.Controls.Clear();
    }

    private void ResetFilters()
    {
        _searchBox.Clear();
        for (var index = 0; index < _actList.Items.Count; index++)
            _actList.SetItemChecked(index, true);
        for (var index = 0; index < _rarityList.Items.Count; index++)
            _rarityList.SetItemChecked(index, true);
        _typeBox.SelectedIndex = 0;
        _placeBox.SelectedIndex = 0;
        _notesOnly.Checked = false;
        _foundBox.SelectedIndex = 0;
        _sortBox.SelectedIndex = 0;
        _directionBox.SelectedIndex = 0;
        ApplyFilters();
    }

    private void GridOnCellContentClick(object? sender, DataGridViewCellEventArgs eventArgs)
    {
        if (eventArgs.RowIndex < 0 || _grid.Rows[eventArgs.RowIndex].DataBoundItem is not ItemRecord item)
            return;
        if (_grid.Columns[eventArgs.ColumnIndex].Name == "Found")
            ToggleFound(item);
        else if (_grid.Columns[eventArgs.ColumnIndex].Name == "Equipped")
            ToggleEquipped(item);
    }

    private void GridOnKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.KeyCode == Keys.Space && _grid.CurrentRow?.DataBoundItem is ItemRecord item)
        {
            ToggleFound(item);
            eventArgs.Handled = true;
        }
    }

    private void ToggleFound(ItemRecord item)
    {
        item.Found = !item.Found;
        try
        {
            _progressStore.Save(_allItems, _characterState);
        }
        catch (Exception exception)
        {
            item.Found = !item.Found;
            MessageBox.Show(
                Localization.Format("ProgressError", exception.Message),
                Localization.T("WarningTitle"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }
        ApplyFilters();
    }

    private void ToggleEquipped(ItemRecord item)
    {
        if (item.Equipped)
            item.Equipped = false;
        else
            GearRules.Equip(_allItems, item);

        _characterSheet.RefreshFromEquipment();
        SaveProgressWithWarning();
        _grid.Invalidate();
        ShowSelectedItem();
    }

    private void SaveProgressWithWarning()
    {
        try
        {
            _progressStore.Save(_allItems, _characterState);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                Localization.Format("ProgressError", exception.Message),
                Localization.T("WarningTitle"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private static Image CreatePlaceholderImage(string name)
    {
        var bitmap = new Bitmap(384, 384);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Theme.CrimsonDark);
        using var goldPen = new Pen(Theme.Gold, 6);
        graphics.DrawEllipse(goldPen, 45, 45, 294, 294);
        using var titleFont = Theme.Heading(34f);
        using var nameFont = Theme.Body(14f, FontStyle.Bold);
        using var goldBrush = new SolidBrush(Theme.GoldLight);
        using var parchmentBrush = new SolidBrush(Theme.Parchment);
        graphics.DrawString("BG3", titleFont, goldBrush, new RectangleF(0, 110, 384, 55), new StringFormat { Alignment = StringAlignment.Center });
        graphics.DrawString(name, nameFont, parchmentBrush, new RectangleF(35, 190, 314, 90), new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
        return bitmap;
    }

    private static void OpenBestLink(ItemRecord item)
    {
        var url = item.Links.TryGetValue("Name", out var nameLink)
            ? nameLink
            : item.Links.Values.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(url))
            OpenUrl(url);
    }

    private static void OpenUrl(string url)
    {
        try
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
                return;
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            MessageBox.Show(Localization.Format("LinkError", exception.Message), Localization.T("WarningTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
