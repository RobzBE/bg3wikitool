using System.Diagnostics;

namespace BG3ItemExplorer;

public sealed class MainForm : Form
{
    private readonly List<ItemRecord> _allItems;
    private List<ItemRecord> _visibleItems = [];
    private readonly BindingSource _gridSource = new();
    private readonly ProgressStore _progressStore = new();
    private readonly ItemImageRepository _imageRepository = new();
    private readonly SafeSplitContainer _mainSplit = new();
    private readonly SafeSplitContainer _contentCharacterSplit = new();
    private readonly SafeSplitContainer _rightSplit = new();
    private readonly Panel _headerPanel = new();
    private readonly Panel _footerPanel = new();
    private readonly TextBox _searchBox = new();
    private readonly ComboBox _languageBox = new();
    private readonly CheckedListBox _actList = new ModernCheckedListBox();
    private readonly CheckedListBox _rarityList = new ModernCheckedListBox();
    private readonly ComboBox _typeBox = new();
    private readonly ComboBox _placeBox = new();
    private readonly CheckBox _notesOnly = new ModernCheckBox();
    private readonly CheckBox _equippedOnly = new ModernCheckBox();
    private readonly ComboBox _foundBox = new();
    private readonly ComboBox _sortBox = new();
    private readonly ComboBox _directionBox = new();
    private readonly Label _resultLabel = new();
    private readonly Label _saveStatus = new();
    private readonly Button _linkNewestSave = new();
    private readonly Button _browseSave = new();
    private readonly Button _syncSave = new();
    private readonly Button _unlinkSave = new();
    private readonly ToolTip _saveToolTip = new() { AutoPopDelay = 30000, InitialDelay = 300, ShowAlways = true };
    private readonly System.Windows.Forms.Timer _saveDebounceTimer = new() { Interval = 1800 };
    private FileSystemWatcher? _saveWatcher;
    private readonly SaveLinkState _saveLink;
    private bool _saveSyncing;
    private bool _saveSyncPending;
    private readonly DataGridView _grid = new();
    private readonly Label _detailTitle = new();
    private readonly Label _detailMeta = new();
    private readonly RichTextBox _detailText = new();
    private readonly FlowLayoutPanel _linksPanel = new();
    private readonly PictureBox _itemPicture = new();
    private readonly List<CharacterState> _characters;
    private int _activeCharacterIndex;
    private readonly CharacterSheetPanel _characterSheet;
    private bool _applyingLanguage;
    private bool _alwaysMaximized;

    public MainForm(List<ItemRecord> items)
    {
        _allItems = items;
        var progress = _progressStore.LoadState();
        _characters = progress.Characters;
        _saveLink = progress.SaveLink;
        _activeCharacterIndex = Math.Clamp(progress.ActiveCharacterIndex, 0, _characters.Count - 1);
        var activeCharacter = _characters[_activeCharacterIndex];
        foreach (var item in _allItems)
        {
            item.Found = progress.FoundKeys.Contains(item.ProgressKey);
            item.Equipped = activeCharacter.EquippedKeys.Contains(item.ProgressKey, StringComparer.OrdinalIgnoreCase);
        }
        _characterSheet = new CharacterSheetPanel(_characters, _activeCharacterIndex, _allItems);
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
        ApplyHighContrastCursor(this);
        PopulateFilterChoices();
        WireEvents();
        ApplyLanguage();
        ApplyFilters();
        ConfigureSaveWatcher();
    }

    private static void ApplyHighContrastCursor(Control root)
    {
        if (root is not TextBoxBase)
            root.Cursor = HighContrastCursor.Current;
        foreach (Control child in root.Controls)
            ApplyHighContrastCursor(child);
    }

    public void RenderPreview(string path, int? width = null, int? height = null)
    {
        if (width.HasValue && height.HasValue)
            Size = new Size(Math.Max(MinimumSize.Width, width.Value), Math.Max(MinimumSize.Height, height.Value));
        PerformLayout();
        _mainSplit.PerformLayout();
        _rightSplit.PerformLayout();
        _grid.Refresh();
        using var bitmap = new Bitmap(ClientSize.Width, ClientSize.Height);
        DrawToBitmap(bitmap, new Rectangle(Point.Empty, ClientSize));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }

    public void EnableAlwaysMaximized()
    {
        _alwaysMaximized = true;
        WindowState = FormWindowState.Maximized;
    }

    public void RunHeaderVisibilityTest(string reportPath)
    {
        var results = new List<object>();
        foreach (var requestedWidth in new[] { 1100, 1200, 1480 })
        {
            Size = new Size(requestedWidth, 700);
            PerformLayout();
            Application.DoEvents();
            var headerBounds = _headerPanel.RectangleToScreen(_headerPanel.ClientRectangle);
            var controls = new Control[] { _saveStatus, _linkNewestSave, _browseSave, _syncSave, _unlinkSave };
            var controlResults = controls.Select(control => new
            {
                Name = control.Text,
                control.Visible,
                Bounds = control.RectangleToScreen(control.ClientRectangle),
                FullyInsideHeader = control.Visible && headerBounds.Contains(control.RectangleToScreen(control.ClientRectangle))
            }).ToList();
            results.Add(new
            {
                RequestedWidth = requestedWidth,
                ActualClientSize = ClientSize,
                Passed = controlResults.All(result => result.FullyInsideHeader),
                Controls = controlResults
            });
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllText(reportPath, System.Text.Json.JsonSerializer.Serialize(results, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }

    public void RunFilterStressTest(string reportPath)
    {
        var iterations = 0;
        string? failure = null;
        var equippedFilterApplied = false;
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

            _equippedOnly.Checked = true;
            Application.DoEvents();
            equippedFilterApplied = _visibleItems.All(item =>
                _characters[_activeCharacterIndex].EquippedKeys.Contains(item.ProgressKey, StringComparer.OrdinalIgnoreCase));
            _equippedOnly.Checked = false;
            Application.DoEvents();
            iterations += 2;

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
            Passed = failure is null && equippedFilterApplied && _characterSheet.CalculationToolTipsReady,
            Iterations = iterations,
            FinalVisibleItems = _visibleItems.Count,
            CalculationToolTipsReady = _characterSheet.CalculationToolTipsReady,
            EquippedFilterApplied = equippedFilterApplied,
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
        _headerPanel.Height = 88;
        _headerPanel.BackColor = Theme.CrimsonDark;
        _headerPanel.Padding = new Padding(20, 8, 20, 8);
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 57));

        var identity = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) };
        var title = new Label
        {
            Text = "BALDUR'S GATE 3  •  ITEM EXPLORER",
            Dock = DockStyle.Top,
            Height = 38,
            ForeColor = Theme.GoldLight,
            Font = Theme.Heading(19f),
            TextAlign = ContentAlignment.MiddleLeft
        };
        var subtitle = new Label
        {
            Text = "Act 1, 2 & 3 • offline • volledige iteminformatie • data & afbeeldingen: bg3.wiki",
            Dock = DockStyle.Bottom,
            Height = 24,
            ForeColor = Color.FromArgb(223, 205, 170),
            Font = Theme.Body(9f, FontStyle.Italic),
            TextAlign = ContentAlignment.MiddleLeft
        };
        subtitle.Text = Localization.T("Subtitle");
        subtitle.Tag = "i18n:Subtitle";
        identity.Controls.Add(title);
        identity.Controls.Add(subtitle);

        var saveArea = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(8, 0, 0, 0),
            Padding = new Padding(0),
            ColumnCount = 1,
            RowCount = 2
        };
        saveArea.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        saveArea.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        saveArea.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _saveStatus.Dock = DockStyle.Fill;
        _saveStatus.Margin = new Padding(0);
        _saveStatus.TextAlign = ContentAlignment.MiddleRight;
        _saveStatus.ForeColor = Color.FromArgb(223, 205, 170);
        _saveStatus.Font = Theme.Body(8.5f, FontStyle.Bold);
        _saveStatus.AutoEllipsis = true;
        ConfigureHeaderButton(_unlinkSave, "UnlinkSave", 74);
        ConfigureHeaderButton(_browseSave, "BrowseSave", 74);
        ConfigureHeaderButton(_linkNewestSave, "LinkNewestSave", 112);
        ConfigureHeaderButton(_syncSave, "SyncSave", 70);
        var saveButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
            Padding = new Padding(0, 3, 0, 0),
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        saveButtons.Controls.AddRange([_unlinkSave, _browseSave, _linkNewestSave, _syncSave]);
        saveArea.Controls.Add(_saveStatus, 0, 0);
        saveArea.Controls.Add(saveButtons, 0, 1);
        layout.Controls.Add(identity, 0, 0);
        layout.Controls.Add(saveArea, 1, 0);
        _headerPanel.Controls.Add(layout);
        Controls.Add(_headerPanel);
        RefreshSaveStatus();
    }

    private static void ConfigureHeaderButton(Button button, string localizationKey, int width)
    {
        button.Text = Localization.T(localizationKey);
        button.Tag = "i18n:" + localizationKey;
        button.Size = new Size(width, 25);
        button.Margin = new Padding(5, 1, 0, 0);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = Theme.Gold;
        button.BackColor = Theme.CrimsonDark;
        button.ForeColor = Theme.GoldLight;
        button.Font = Theme.Body(7.5f, FontStyle.Bold);
        button.Cursor = Cursors.Hand;
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
        _mainSplit.SplitterDistance = Math.Clamp((int)(_mainSplit.Width * 0.20), 250, 360);

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
            RowCount = 22,
            BackColor = Theme.ParchmentAlt
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var index = 0; index < 21; index++)
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
        Theme.ConfigureInput(_searchBox, 10f);
        _searchBox.Margin = new Padding(0, 0, 0, 10);
        layout.Controls.Add(_searchBox);

        AddLocalizedCaption(layout, "Acts");
        _actList.Dock = DockStyle.Top;
        _actList.Height = 76;
        _actList.CheckOnClick = true;
        _actList.Font = Theme.Body(9.5f);
        _actList.BackColor = Theme.Parchment;
        _actList.BorderStyle = BorderStyle.FixedSingle;
        layout.Controls.Add(_actList);

        AddLocalizedCaption(layout, "Rarity");
        _rarityList.Dock = DockStyle.Top;
        _rarityList.Height = 100;
        _rarityList.CheckOnClick = true;
        _rarityList.Font = Theme.Body(9.5f);
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

        _equippedOnly.Text = Localization.T("EquippedOnly");
        _equippedOnly.Tag = "i18n:EquippedOnly";
        _equippedOnly.AutoSize = true;
        _equippedOnly.ForeColor = Theme.Ink;
        _equippedOnly.Margin = new Padding(0, 0, 0, 8);
        layout.Controls.Add(_equippedOnly);

        AddLocalizedCaption(layout, "Progress");
        ConfigureFilterCombo(_foundBox);
        layout.Controls.Add(_foundBox);

        AddLocalizedCaption(layout, "Sorting");
        var sortPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 38,
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
        _actList.Font = Theme.Body(9.5f);
        _actList.BackColor = Theme.Parchment;
        _actList.BorderStyle = BorderStyle.FixedSingle;
        layout.Controls.Add(_actList);

        AddCaption(layout, "Zeldzaamheid");
        _rarityList.Dock = DockStyle.Top;
        _rarityList.Height = 92;
        _rarityList.CheckOnClick = true;
        _rarityList.Font = Theme.Body(9.5f);
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
        combo.Margin = new Padding(0, 0, 0, 8);
        Theme.ConfigureModernCombo(combo, 9.5f);
    }

    private void BuildRightPanel(Control host)
    {
        _contentCharacterSplit.Dock = DockStyle.Fill;
        _contentCharacterSplit.Orientation = Orientation.Vertical;
        _contentCharacterSplit.SplitterWidth = 5;
        _contentCharacterSplit.BackColor = Theme.Gold;
        host.Controls.Add(_contentCharacterSplit);
        if (_contentCharacterSplit.Width > 850)
            _contentCharacterSplit.SplitterDistance = Math.Clamp((int)(_contentCharacterSplit.Width * 0.625), 520, _contentCharacterSplit.Width - 420);

        _rightSplit.Dock = DockStyle.Fill;
        _rightSplit.Orientation = Orientation.Horizontal;
        _rightSplit.SplitterWidth = 5;
        _rightSplit.BackColor = Theme.Gold;
        _contentCharacterSplit.Panel1.Controls.Add(_rightSplit);
        _contentCharacterSplit.Panel2.Controls.Add(_characterSheet);
        if (_rightSplit.Height >= 600)
            _rightSplit.SplitterDistance = Math.Clamp((int)(_rightSplit.Height * 0.65), 360, _rightSplit.Height - 200);

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
            ToolTipText = "Gevonden / opgehaald",
            FlatStyle = FlatStyle.Flat
        });
        for (var characterIndex = 0; characterIndex < 4; characterIndex++)
        {
            _grid.Columns.Add(new DataGridViewCheckBoxColumn
            {
                HeaderText = $"⚔{characterIndex + 1}",
                Name = $"Equipped{characterIndex + 1}",
                Width = 42,
                MinimumWidth = 42,
                Frozen = true,
                ReadOnly = true,
                FlatStyle = FlatStyle.Flat
            });
        }
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
            UpdateEquipmentColumnHeaders();
            _grid.Columns[nameof(ItemRecord.Name)]!.HeaderText = Localization.T("GridName");
            _grid.Columns[nameof(ItemRecord.Rarity)]!.HeaderText = Localization.T("GridRarity");
            _grid.Columns[nameof(ItemRecord.Type)]!.HeaderText = Localization.T("GridType");
            _grid.Columns[nameof(ItemRecord.Properties)]!.HeaderText = Localization.T("GridProperties");
            _grid.Columns[nameof(ItemRecord.ActArea)]!.HeaderText = Localization.T("GridActArea");
            _grid.Columns[nameof(ItemRecord.Location)]!.HeaderText = Localization.T("GridLocation");
            _grid.Columns[nameof(ItemRecord.Description)]!.HeaderText = Localization.T("GridDescription");
            _grid.Columns[nameof(ItemRecord.NotesText)]!.HeaderText = Localization.T("GridNotes");
            _characterSheet.SetLanguage();
            RefreshSaveStatus();
        }
        finally
        {
            _applyingLanguage = false;
        }

        ApplyFilters();
    }

    private void UpdateEquipmentColumnHeaders()
    {
        for (var index = 0; index < 4; index++)
        {
            var column = _grid.Columns[$"Equipped{index + 1}"];
            if (column is null)
                continue;
            column.HeaderText = $"⚔{index + 1}";
            column.ToolTipText = Localization.Format("EquippedTemplateTooltip", index + 1, _characters[index].Name);
        }
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
        _equippedOnly.CheckedChanged += (_, _) => ApplyFilters();
        _foundBox.SelectedIndexChanged += (_, _) => ApplyFilters();
        _sortBox.SelectedIndexChanged += (_, _) => ApplyFilters();
        _directionBox.SelectedIndexChanged += (_, _) => ApplyFilters();
        _linkNewestSave.Click += async (_, _) => await LinkNewestSaveAsync();
        _browseSave.Click += async (_, _) => await BrowseAndLinkSaveAsync();
        _syncSave.Click += async (_, _) => await SyncLinkedSaveAsync(force: true, showErrors: true);
        _unlinkSave.Click += (_, _) => UnlinkSave();
        _saveDebounceTimer.Tick += async (_, _) =>
        {
            _saveDebounceTimer.Stop();
            await SyncLinkedSaveAsync(force: true, showErrors: false);
        };
        _characterSheet.StateChanged += (_, _) =>
        {
            UpdateEquipmentColumnHeaders();
            SaveProgressWithWarning();
        };
        _characterSheet.ActiveTemplateChanged += index =>
        {
            _activeCharacterIndex = index;
            UpdateEquipmentColumnHeaders();
            ApplyFilters();
        };
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
            if (_alwaysMaximized && Visible && WindowState == FormWindowState.Normal)
                BeginInvoke(() => WindowState = FormWindowState.Maximized);
        };
        DpiChanged += (_, _) => BeginInvoke(() =>
        {
            LayoutContentArea();
            _mainSplit.PerformLayout();
            _contentCharacterSplit.PerformLayout();
            _rightSplit.PerformLayout();
        });
        Shown += async (_, _) =>
        {
            LayoutContentArea();
            if (!string.IsNullOrWhiteSpace(_saveLink.LinkedSavePath))
                await SyncLinkedSaveAsync(force: false, showErrors: false);
        };
        FormClosed += (_, _) =>
        {
            _itemPicture.Image?.Dispose();
            _imageRepository.Dispose();
            _saveWatcher?.Dispose();
            _saveDebounceTimer.Dispose();
            _saveToolTip.Dispose();
        };
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
            (!_equippedOnly.Checked || _characters[_activeCharacterIndex].EquippedKeys.Contains(item.ProgressKey, StringComparer.OrdinalIgnoreCase)) &&
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
        var columnName = _grid.Columns[eventArgs.ColumnIndex].Name;
        if (columnName.StartsWith("Equipped", StringComparison.Ordinal)
            && int.TryParse(columnName["Equipped".Length..], out var characterNumber)
            && characterNumber is >= 1 and <= 4)
        {
            eventArgs.Value = _characters[characterNumber - 1].EquippedKeys.Contains(item.ProgressKey, StringComparer.OrdinalIgnoreCase);
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
        _equippedOnly.Checked = false;
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
        else
        {
            var columnName = _grid.Columns[eventArgs.ColumnIndex].Name;
            if (columnName.StartsWith("Equipped", StringComparison.Ordinal)
                && int.TryParse(columnName["Equipped".Length..], out var characterNumber)
                && characterNumber is >= 1 and <= 4)
                ToggleEquipped(item, characterNumber - 1);
        }
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
            _progressStore.Save(_allItems, _characters, _activeCharacterIndex, _saveLink);
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

    private void ToggleEquipped(ItemRecord item, int characterIndex)
    {
        var character = _characters[characterIndex];
        character.EquippedKeys ??= [];
        if (character.EquippedKeys.Contains(item.ProgressKey, StringComparer.OrdinalIgnoreCase))
            character.EquippedKeys.RemoveAll(key => key.Equals(item.ProgressKey, StringComparison.OrdinalIgnoreCase));
        else
            GearRules.EquipForCharacter(_allItems, character, item);

        if (characterIndex == _activeCharacterIndex)
        {
            foreach (var candidate in _allItems)
                candidate.Equipped = character.EquippedKeys.Contains(candidate.ProgressKey, StringComparer.OrdinalIgnoreCase);
            _characterSheet.RefreshFromEquipment();
        }
        SaveProgressWithWarning();
        if (_equippedOnly.Checked && characterIndex == _activeCharacterIndex)
            ApplyFilters();
        else
        {
            _grid.Invalidate();
            ShowSelectedItem();
        }
    }

    private void SaveProgressWithWarning()
    {
        try
        {
            _progressStore.Save(_allItems, _characters, _activeCharacterIndex, _saveLink);
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

    private async Task LinkNewestSaveAsync()
    {
        var root = SaveGameService.DefaultStoryDirectory;
        var newest = SaveGameService.FindNewestSupportedSave(root);
        if (newest is null)
        {
            var answer = MessageBox.Show(
                this,
                Localization.Format("DefaultSaveMissing", root),
                Localization.T("SaveGameLink"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);
            if (answer == DialogResult.Yes)
                await BrowseAndLinkSaveAsync();
            return;
        }
        await LinkSaveAsync(newest, root);
    }

    private async Task BrowseAndLinkSaveAsync()
    {
        using var dialog = new OpenFileDialog
        {
            Title = Localization.T("BrowseSaveTitle"),
            Filter = "Baldur's Gate 3 save (*.lsv)|*.lsv|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false,
            InitialDirectory = Directory.Exists(SaveGameService.DefaultStoryDirectory)
                ? SaveGameService.DefaultStoryDirectory
                : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            await LinkSaveAsync(dialog.FileName, SaveGameService.FindWatchDirectory(dialog.FileName));
    }

    private async Task LinkSaveAsync(string savePath, string watchDirectory)
    {
        _saveLink.LinkedSavePath = Path.GetFullPath(savePath);
        _saveLink.WatchDirectory = Path.GetFullPath(watchDirectory);
        _saveLink.AutoSync = true;
        _saveLink.LastImportedWriteUtc = null;
        ConfigureSaveWatcher();
        SaveProgressWithWarning();
        await SyncLinkedSaveAsync(force: true, showErrors: true);
    }

    private void UnlinkSave()
    {
        _saveDebounceTimer.Stop();
        _saveWatcher?.Dispose();
        _saveWatcher = null;
        _saveLink.LinkedSavePath = "";
        _saveLink.WatchDirectory = "";
        _saveLink.LastImportedWriteUtc = null;
        SaveProgressWithWarning();
        RefreshSaveStatus();
    }

    private void ConfigureSaveWatcher()
    {
        _saveWatcher?.Dispose();
        _saveWatcher = null;
        if (!_saveLink.AutoSync || string.IsNullOrWhiteSpace(_saveLink.WatchDirectory) || !Directory.Exists(_saveLink.WatchDirectory))
        {
            RefreshSaveStatus();
            return;
        }
        try
        {
            _saveWatcher = new FileSystemWatcher(_saveLink.WatchDirectory, "*.lsv")
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };
            _saveWatcher.Created += SaveFileChanged;
            _saveWatcher.Changed += SaveFileChanged;
            _saveWatcher.Renamed += SaveFileChanged;
            RefreshSaveStatus();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            _saveStatus.Text = Localization.T("SaveWatcherError");
            _saveToolTip.SetToolTip(_saveStatus, exception.Message);
        }
    }

    private void SaveFileChanged(object? sender, FileSystemEventArgs eventArgs)
    {
        if (IsDisposed || eventArgs.FullPath.Contains("AutoSave_", StringComparison.OrdinalIgnoreCase))
            return;
        try
        {
            BeginInvoke((Action)(() =>
            {
                _saveDebounceTimer.Stop();
                _saveDebounceTimer.Start();
                _saveStatus.Text = Localization.T("SaveWaiting");
            }));
        }
        catch (InvalidOperationException) { }
    }

    private async Task SyncLinkedSaveAsync(bool force, bool showErrors)
    {
        if (_saveSyncing)
        {
            _saveSyncPending = true;
            return;
        }
        if (string.IsNullOrWhiteSpace(_saveLink.LinkedSavePath))
        {
            if (showErrors)
                MessageBox.Show(this, Localization.T("NoLinkedSave"), Localization.T("SaveGameLink"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var newest = SaveGameService.FindNewestSupportedSave(_saveLink.WatchDirectory);
        if (newest is not null)
            _saveLink.LinkedSavePath = newest;
        if (!File.Exists(_saveLink.LinkedSavePath))
        {
            RefreshSaveStatus(Localization.T("SaveMissing"));
            return;
        }
        var writeUtc = File.GetLastWriteTimeUtc(_saveLink.LinkedSavePath);
        if (!force && _saveLink.LastImportedWriteUtc == writeUtc)
            return;

        _saveSyncing = true;
        _syncSave.Enabled = false;
        _saveStatus.Text = Localization.T("SaveSyncing");
        try
        {
            var imported = await SaveGameService.ImportAsync(_saveLink.LinkedSavePath, _allItems);
            ApplySaveImport(imported);
            _saveLink.LastImportedWriteUtc = imported.WriteUtc;
            SaveProgressWithWarning();
            var details = imported.Warnings.Count == 0
                ? Localization.Format("SaveImportDetails", imported.Characters.Count, imported.MatchedPresentItems)
                : Localization.Format("SaveImportDetails", imported.Characters.Count, imported.MatchedPresentItems) + Environment.NewLine + string.Join(Environment.NewLine, imported.Warnings);
            RefreshSaveStatus(Localization.Format("SaveSynced", SaveGameService.SaveKind(imported.SavePath), imported.WriteUtc.ToLocalTime().ToString("g")), details);
            if (showErrors && imported.Warnings.Count > 0)
                MessageBox.Show(this, details, Localization.T("SaveGameLink"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception exception)
        {
            RefreshSaveStatus(Localization.T("SaveSyncFailed"), exception.Message);
            if (showErrors)
                MessageBox.Show(this, Localization.Format("SaveImportError", exception.Message), Localization.T("SaveGameLink"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            _saveSyncing = false;
            _syncSave.Enabled = true;
            if (_saveSyncPending)
            {
                _saveSyncPending = false;
                _saveDebounceTimer.Stop();
                _saveDebounceTimer.Start();
            }
        }
    }

    private void ApplySaveImport(SaveImportResult imported)
    {
        for (var sourceIndex = 0; sourceIndex < Math.Min(4, imported.Characters.Count); sourceIndex++)
        {
            var source = imported.Characters[sourceIndex];
            var nameMatch = !string.IsNullOrWhiteSpace(source.Name)
                ? _characters.FindIndex(character => character.Name.Equals(source.Name, StringComparison.OrdinalIgnoreCase))
                : -1;
            var targetIndex = nameMatch >= 0 ? nameMatch : imported.Characters.Count == 1 ? _activeCharacterIndex : sourceIndex;
            SaveGameService.MergeInto(_characters[targetIndex], source);
        }

        foreach (var item in _allItems.Where(item => imported.PresentKeys.Contains(item.ProgressKey)))
            item.Found = true;
        var active = _characters[_activeCharacterIndex];
        foreach (var item in _allItems)
            item.Equipped = active.EquippedKeys.Contains(item.ProgressKey, StringComparer.OrdinalIgnoreCase);
        _characterSheet.RefreshFromSaveImport();
        UpdateEquipmentColumnHeaders();
        ApplyFilters();
    }

    private void RefreshSaveStatus(string? status = null, string? details = null)
    {
        var linked = !string.IsNullOrWhiteSpace(_saveLink.LinkedSavePath);
        _saveStatus.Text = status ?? (linked
            ? Localization.Format("SaveLinked", Path.GetFileNameWithoutExtension(_saveLink.LinkedSavePath))
            : Localization.T("SaveNotLinked"));
        _saveStatus.ForeColor = linked ? Theme.GoldLight : Color.FromArgb(223, 205, 170);
        _saveToolTip.SetToolTip(_saveStatus, details ?? (linked ? _saveLink.LinkedSavePath : Localization.T("SaveOptional")));
        _syncSave.Enabled = !_saveSyncing;
        _unlinkSave.Enabled = true;
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
