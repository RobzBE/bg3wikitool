namespace BG3ItemExplorer;

internal sealed class CharacterSheetPanel : UserControl
{
    private CharacterState _state;
    private readonly IReadOnlyList<CharacterState> _templates;
    private int _activeTemplateIndex;
    private readonly List<ItemRecord> _items;
    private readonly ComboBox _template = new();
    private readonly TextBox _characterName = new();
    private readonly TextBox _shareId = new();
    private readonly Button _copyShareLink = new();
    private readonly Button _importShareId = new();
    private readonly ComboBox _race = new();
    private readonly ComboBox _class = new();
    private readonly ComboBox _subclass = new();
    private readonly ComboBox _difficulty = new();
    private readonly NumericUpDown _level = new WheelSafeNumericUpDown();
    private readonly Dictionary<string, NumericUpDown> _abilities = [];
    private readonly Dictionary<string, Label> _abilityTotals = [];
    private readonly Dictionary<string, NumericUpDown> _classLevels = [];
    private readonly Dictionary<string, ComboBox> _multiclassSubclasses = [];
    private readonly Dictionary<string, ComboBox> _fightingStyles = [];
    private readonly List<FeatSlotControls> _featSlots = [];
    private readonly Label _title = new();
    private readonly Label _templateCaption = new();
    private readonly Label _characterNameCaption = new();
    private readonly Label _shareIdCaption = new();
    private readonly Label _identityCaption = new();
    private readonly Label _abilityCaption = new();
    private readonly Label _classLevelsCaption = new();
    private readonly Label _classLevelSummary = new();
    private readonly Label _classFeaturesCaption = new();
    private readonly Label _featsCaption = new();
    private readonly Label _featSummary = new();
    private readonly Label _activeBuffsCaption = new();
    private readonly Label _buffInfo = new();
    private readonly Label _permanentBonusesCaption = new();
    private readonly CheckedListBox _permanentBonuses = new();
    private readonly ComboBox _permanentBonusChoice = new();
    private readonly Label _permanentBonusInfo = new();
    private readonly Label _buildWarnings = new();
    private readonly Label _offenseCaption = new();
    private readonly Label _defenseCaption = new();
    private readonly Label _threatCaption = new();
    private readonly Label _equipmentCaption = new();
    private readonly Label _conditionsCaption = new();
    private readonly Label _raceCaption = new();
    private readonly Label _classCaption = new();
    private readonly Label _subclassCaption = new();
    private readonly Label _levelCaption = new();
    private readonly Label _difficultyCaption = new();
    private readonly Label _difficultyInfo = new();
    private readonly Label _acValue = new();
    private readonly Label _spellDcValue = new();
    private readonly Label _criticalValue = new();
    private readonly ToolTip _calculationToolTip = new()
    {
        InitialDelay = 250,
        ReshowDelay = 100,
        AutoPopDelay = 30000,
        ShowAlways = true
    };
    private Control? _acCard;
    private Control? _spellDcCard;
    private Control? _criticalCard;
    private readonly Label _offense = new();
    private readonly Label _defense = new();
    private readonly Label _saves = new();
    private readonly FlowLayoutPanel _enemyThreats = new();
    private readonly FlowLayoutPanel _characterThreats = new();
    private readonly Label _characterThreatCaption = new();
    private readonly ListBox _equipment = new();
    private readonly CheckedListBox _conditions = new();
    private readonly CheckedListBox _buffs = new();
    private readonly CheckedListBox _classOptions = new();
    private readonly Label _classOptionsCaption = new();
    private readonly Label _classOptionInfo = new();
    private readonly Label _benchmarkNote = new();
    private readonly TabControl _tabs = new();
    private readonly TabPage _buildTab = new();
    private readonly TabPage _statsTab = new();
    private bool _updating;

    public event EventHandler? StateChanged;
    public event Action<int>? ActiveTemplateChanged;

    public CharacterSheetPanel(IReadOnlyList<CharacterState> templates, int activeTemplateIndex, List<ItemRecord> items)
    {
        _templates = templates;
        _activeTemplateIndex = Math.Clamp(activeTemplateIndex, 0, templates.Count - 1);
        _state = templates[_activeTemplateIndex];
        _items = items;
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(248, 235, 207);
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScroll = true;
        Build();
        LoadStateIntoControls();
        WireEvents();
        RefreshCalculations();
    }

    public void RefreshFromEquipment()
    {
        _state.EquippedKeys = _items.Where(item => item.Equipped).Select(item => item.ProgressKey).OrderBy(value => value).ToList();
        _state.ClearImportedAbilityTotals();
        RefreshCalculations();
    }

    public void RefreshFromSaveImport()
    {
        LoadStateIntoControls();
        RefreshCalculations();
    }

    public CharacterState CurrentState => _state;
    public int ActiveTemplateIndex => _activeTemplateIndex;
    public void SelectBuildTab() => _tabs.SelectedTab = _buildTab;
    public bool CalculationToolTipsReady =>
        !string.IsNullOrWhiteSpace(_calculationToolTip.GetToolTip(_acValue))
        && !string.IsNullOrWhiteSpace(_calculationToolTip.GetToolTip(_spellDcValue))
        && !string.IsNullOrWhiteSpace(_calculationToolTip.GetToolTip(_criticalValue));

    public void SetLanguage()
    {
        _title.Text = Localization.T("CharacterSheet");
        _templateCaption.Text = Localization.T("CharacterTemplate");
        _characterNameCaption.Text = Localization.T("CharacterName");
        _shareIdCaption.Text = Localization.T("ShareId");
        _copyShareLink.Text = Localization.T("CopyLink");
        _importShareId.Text = Localization.T("Import");
        _identityCaption.Text = Localization.T("Identity");
        _abilityCaption.Text = Localization.T("BaseAbilities");
        _offenseCaption.Text = Localization.T("Offense");
        _defenseCaption.Text = Localization.T("Defense");
        _threatCaption.Text = Localization.T("EnemyHitChance");
        _equipmentCaption.Text = Localization.T("EquippedGear");
        _conditionsCaption.Text = Localization.T("ActiveConditions");
        _raceCaption.Text = Localization.T("Race");
        _classCaption.Text = Localization.T("StartClass");
        _subclassCaption.Text = Localization.T("MainSubclass");
        _levelCaption.Text = Localization.T("TotalLevel");
        _classLevelsCaption.Text = Localization.T("ClassLevels");
        _classFeaturesCaption.Text = Localization.T("ClassFeatures");
        _featsCaption.Text = Localization.T("Feats");
        _activeBuffsCaption.Text = Localization.T("ActiveSpellsConditions");
        _classOptionsCaption.Text = Localization.T("ClassSubclassOptions");
        _permanentBonusesCaption.Text = Localization.T("PermanentBonuses");
        _buildTab.Text = Localization.T("BuildTab");
        _statsTab.Text = Localization.T("StatsTab");
        _difficultyCaption.Text = Localization.T("Difficulty");
        _difficultyInfo.Text = Localization.T("Difficulty" + (_difficulty.SelectedItem as string ?? _state.Difficulty));
        _benchmarkNote.Text = Localization.T("BenchmarkNote");
        _characterThreatCaption.Text = Localization.T("CharacterHitChance");
        RefreshTemplateSelector();
        RefreshClassLevelControls();
        RefreshBuildOptionControls();
        RefreshCalculations();
    }

    private void Build()
    {
        AutoScroll = false;
        _tabs.Dock = DockStyle.Fill;
        _tabs.Font = Theme.Body(8.5f, FontStyle.Bold);
        _buildTab.BackColor = BackColor;
        _buildTab.AutoScroll = true;
        _statsTab.BackColor = BackColor;
        _statsTab.AutoScroll = true;
        var templateBar = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 100,
            ColumnCount = 2,
            RowCount = 4,
            Padding = new Padding(10, 4, 10, 4),
            BackColor = Theme.ParchmentAlt
        };
        templateBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        templateBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        templateBar.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
        templateBar.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
        templateBar.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
        templateBar.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
        ConfigureCaption(_templateCaption);
        ConfigureCaption(_characterNameCaption);
        ConfigureCaption(_shareIdCaption);
        ConfigureCombo(_template);
        _characterName.Dock = DockStyle.Top;
        _characterName.Font = Theme.Body(8.5f);
        _characterName.BackColor = Theme.Parchment;
        _characterName.MaxLength = 40;
        _shareId.Dock = DockStyle.Fill;
        _shareId.ReadOnly = true;
        _shareId.Font = Theme.Body(7.25f);
        _shareId.BackColor = Theme.Parchment;
        _shareId.WordWrap = false;
        var shareControls = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Margin = new Padding(2, 0, 0, 0) };
        shareControls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        shareControls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 23));
        shareControls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22));
        ConfigureSmallButton(_copyShareLink);
        ConfigureSmallButton(_importShareId);
        shareControls.Controls.Add(_shareId, 0, 0);
        shareControls.Controls.Add(_copyShareLink, 1, 0);
        shareControls.Controls.Add(_importShareId, 2, 0);
        templateBar.Controls.Add(_templateCaption, 0, 0);
        templateBar.Controls.Add(_characterNameCaption, 1, 0);
        templateBar.Controls.Add(_template, 0, 1);
        templateBar.Controls.Add(_characterName, 1, 1);
        templateBar.Controls.Add(_shareIdCaption, 0, 2);
        templateBar.SetColumnSpan(_shareIdCaption, 2);
        templateBar.Controls.Add(shareControls, 0, 3);
        templateBar.SetColumnSpan(shareControls, 2);
        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            Padding = new Padding(12, 10, 12, 16),
            BackColor = BackColor
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        ConfigureHeading(_title, 14f, Theme.Crimson, new Padding(0, 0, 0, 8));
        content.Controls.Add(_title);

        ConfigureSection(_identityCaption);
        content.Controls.Add(_identityCaption);
        var identity = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Margin = new Padding(0, 0, 0, 8) };
        identity.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));
        identity.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52));
        ConfigureCombo(_race);
        ConfigureCombo(_class);
        ConfigureCombo(_subclass);
        ConfigureCombo(_difficulty);
        _race.Items.AddRange(CharacterCalculator.Races);
        _class.Items.AddRange(CharacterCalculator.Classes);
        _difficulty.Items.AddRange(CharacterCalculator.Difficulties);
        ConfigureCaption(_raceCaption);
        ConfigureCaption(_classCaption);
        ConfigureCaption(_subclassCaption);
        ConfigureCaption(_levelCaption);
        ConfigureCaption(_difficultyCaption);
        identity.Controls.Add(_raceCaption, 0, 0);
        identity.Controls.Add(_classCaption, 1, 0);
        identity.Controls.Add(_race, 0, 1);
        identity.Controls.Add(_class, 1, 1);
        identity.Controls.Add(_levelCaption, 0, 2);
        _level.Minimum = 1;
        _level.Maximum = 12;
        _level.Increment = 0;
        _level.ReadOnly = true;
        _level.InterceptArrowKeys = false;
        _level.Dock = DockStyle.Top;
        _level.Font = Theme.Body(9f);
        identity.Controls.Add(_level, 0, 3);
        identity.Controls.Add(_difficultyCaption, 1, 2);
        identity.Controls.Add(_difficulty, 1, 3);
        ConfigureBody(_difficultyInfo, true);
        identity.Controls.Add(_difficultyInfo, 0, 4);
        identity.SetColumnSpan(_difficultyInfo, 2);
        identity.Controls.Add(_subclassCaption, 0, 5);
        identity.SetColumnSpan(_subclassCaption, 2);
        identity.Controls.Add(_subclass, 0, 6);
        identity.SetColumnSpan(_subclass, 2);
        content.Controls.Add(identity);

        ConfigureSection(_classLevelsCaption);
        content.Controls.Add(_classLevelsCaption);
        var classLevels = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Margin = new Padding(0, 0, 0, 4) };
        classLevels.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        classLevels.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        for (var index = 0; index < CharacterCalculator.Classes.Length; index++)
        {
            var className = CharacterCalculator.Classes[index];
            var box = new WheelSafeNumericUpDown
            {
                Minimum = 0,
                Maximum = 12,
                Dock = DockStyle.Top,
                Font = Theme.Body(8.5f),
                Tag = className,
                Margin = new Padding(2)
            };
            _classLevels[className] = box;
            var subclass = new ComboBox { Tag = className, Margin = new Padding(2), Visible = false };
            ConfigureCombo(subclass);
            _multiclassSubclasses[className] = subclass;
            var cell = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, Margin = new Padding(1) };
            cell.Controls.Add(Caption(className));
            cell.Controls.Add(box);
            cell.Controls.Add(subclass);
            classLevels.Controls.Add(cell, index % 2, index / 2);
        }
        content.Controls.Add(classLevels);
        ConfigureBody(_classLevelSummary, true);
        content.Controls.Add(_classLevelSummary);

        ConfigureSection(_abilityCaption);
        content.Controls.Add(_abilityCaption);
        var abilities = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3, Margin = new Padding(0, 0, 0, 8) };
        for (var column = 0; column < 3; column++)
            abilities.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        for (var index = 0; index < CharacterCalculator.AbilityNames.Length; index++)
        {
            var ability = CharacterCalculator.AbilityNames[index];
            var box = new WheelSafeNumericUpDown
            {
                Minimum = 3,
                Maximum = 20,
                Dock = DockStyle.Top,
                Font = Theme.Body(10f),
                Tag = ability,
                Margin = new Padding(2, 2, 2, 3),
                Height = 30
            };
            _abilities[ability] = box;
            var total = new Label
            {
                AutoSize = true,
                Dock = DockStyle.Top,
                ForeColor = Theme.Crimson,
                Font = Theme.Body(9f, FontStyle.Bold),
                Margin = new Padding(3, 1, 2, 5)
            };
            _abilityTotals[ability] = total;
            var cell = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, Margin = new Padding(1) };
            cell.Controls.Add(Caption(ability));
            cell.Controls.Add(box);
            cell.Controls.Add(total);
            abilities.Controls.Add(cell, index % 3, index / 3);
        }
        content.Controls.Add(abilities);

        ConfigureSection(_permanentBonusesCaption);
        content.Controls.Add(_permanentBonusesCaption);
        _permanentBonuses.Dock = DockStyle.Top;
        _permanentBonuses.Height = 190;
        _permanentBonuses.CheckOnClick = true;
        _permanentBonuses.Font = Theme.Body(9.5f);
        _permanentBonuses.BackColor = Theme.Parchment;
        content.Controls.Add(_permanentBonuses);
        ConfigureCombo(_permanentBonusChoice);
        _permanentBonusChoice.Enabled = false;
        content.Controls.Add(_permanentBonusChoice);
        ConfigureBody(_permanentBonusInfo, true);
        content.Controls.Add(_permanentBonusInfo);

        ConfigureSection(_classFeaturesCaption);
        content.Controls.Add(_classFeaturesCaption);
        var styles = new TableLayoutPanel { Dock = DockStyle.Top, Height = 152, AutoSize = false, ColumnCount = 2, RowCount = 4, Margin = new Padding(0, 0, 0, 4) };
        styles.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        styles.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
        for (var index = 0; index < BuildOptions.FightingStyleSlots.Length; index++)
        {
            var slot = BuildOptions.FightingStyleSlots[index];
            var combo = new ComboBox();
            ConfigureCombo(combo);
            _fightingStyles[slot.Key] = combo;
            styles.Controls.Add(Caption(slot.Label), 0, index);
            styles.Controls.Add(combo, 1, index);
        }
        content.Controls.Add(styles);

        ConfigureSection(_featsCaption);
        content.Controls.Add(_featsCaption);
        var feats = new TableLayoutPanel { Dock = DockStyle.Top, Height = 152, AutoSize = false, ColumnCount = 2, RowCount = 4, Margin = new Padding(0, 0, 0, 3) };
        feats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 59));
        feats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 41));
        for (var index = 0; index < 4; index++)
        {
            var featCombo = new ComboBox();
            var choiceCombo = new ComboBox();
            ConfigureCombo(featCombo);
            ConfigureCombo(choiceCombo);
            featCombo.Tag = index;
            choiceCombo.Tag = index;
            _featSlots.Add(new FeatSlotControls(featCombo, choiceCombo));
            feats.Controls.Add(featCombo, 0, index);
            feats.Controls.Add(choiceCombo, 1, index);
        }
        content.Controls.Add(feats);
        ConfigureBody(_featSummary, true);
        content.Controls.Add(_featSummary);

        ConfigureSection(_activeBuffsCaption);
        content.Controls.Add(_activeBuffsCaption);
        _buffs.Dock = DockStyle.Top;
        _buffs.Height = 185;
        _buffs.CheckOnClick = true;
        _buffs.Font = Theme.Body(9.5f);
        _buffs.BackColor = Theme.Parchment;
        content.Controls.Add(_buffs);
        ConfigureBody(_buffInfo, true);
        content.Controls.Add(_buffInfo);

        ConfigureSection(_classOptionsCaption);
        content.Controls.Add(_classOptionsCaption);
        _classOptions.Dock = DockStyle.Top;
        _classOptions.Height = 120;
        _classOptions.CheckOnClick = true;
        _classOptions.Font = Theme.Body(9.5f);
        _classOptions.BackColor = Theme.Parchment;
        content.Controls.Add(_classOptions);
        ConfigureBody(_classOptionInfo, true);
        content.Controls.Add(_classOptionInfo);
        ConfigureBody(_buildWarnings);
        _buildWarnings.ForeColor = Theme.Crimson;
        content.Controls.Add(_buildWarnings);

        var statsContent = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            Padding = new Padding(12, 10, 12, 16),
            BackColor = BackColor
        };
        statsContent.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var highlights = new TableLayoutPanel { Dock = DockStyle.Top, Height = 82, ColumnCount = 3, Margin = new Padding(0, 2, 0, 10) };
        highlights.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
        highlights.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
        highlights.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
        _acCard = HighlightCard("AC", _acValue);
        _spellDcCard = HighlightCard("SPELL DC", _spellDcValue);
        _criticalCard = HighlightCard("CRITICAL", _criticalValue);
        _criticalValue.Font = Theme.Heading(13f);
        highlights.Controls.Add(_acCard, 0, 0);
        highlights.Controls.Add(_spellDcCard, 1, 0);
        highlights.Controls.Add(_criticalCard, 2, 0);
        statsContent.Controls.Add(highlights);

        ConfigureSection(_offenseCaption);
        statsContent.Controls.Add(_offenseCaption);
        ConfigureBody(_offense);
        statsContent.Controls.Add(_offense);

        ConfigureSection(_defenseCaption);
        statsContent.Controls.Add(_defenseCaption);
        ConfigureBody(_defense);
        ConfigureBody(_saves);
        statsContent.Controls.Add(_defense);
        statsContent.Controls.Add(_saves);

        var threatHeadings = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Margin = new Padding(0, 4, 0, 2) };
        threatHeadings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        threatHeadings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        ConfigureSection(_threatCaption);
        ConfigureSection(_characterThreatCaption);
        threatHeadings.Controls.Add(_threatCaption, 0, 0);
        threatHeadings.Controls.Add(_characterThreatCaption, 1, 0);
        statsContent.Controls.Add(threatHeadings);
        var threatColumns = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Margin = new Padding(0, 0, 0, 3) };
        threatColumns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        threatColumns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        ConfigureThreatFlow(_enemyThreats);
        ConfigureThreatFlow(_characterThreats);
        threatColumns.Controls.Add(_enemyThreats, 0, 0);
        threatColumns.Controls.Add(_characterThreats, 1, 0);
        statsContent.Controls.Add(threatColumns);
        ConfigureBody(_benchmarkNote, true);
        statsContent.Controls.Add(_benchmarkNote);

        ConfigureSection(_equipmentCaption);
        statsContent.Controls.Add(_equipmentCaption);
        _equipment.Dock = DockStyle.Top;
        _equipment.Height = 118;
        _equipment.Font = Theme.Body(8.25f);
        _equipment.BackColor = Theme.Parchment;
        statsContent.Controls.Add(_equipment);

        ConfigureSection(_conditionsCaption);
        statsContent.Controls.Add(_conditionsCaption);
        _conditions.Dock = DockStyle.Top;
        _conditions.Height = 86;
        _conditions.CheckOnClick = true;
        _conditions.Font = Theme.Body(9.5f);
        _conditions.BackColor = Theme.Parchment;
        statsContent.Controls.Add(_conditions);

        _buildTab.Controls.Add(content);
        _statsTab.Controls.Add(statsContent);
        _tabs.TabPages.Add(_statsTab);
        _tabs.TabPages.Add(_buildTab);
        Controls.Add(_tabs);
        Controls.Add(templateBar);
        SetLanguage();
    }

    private void RefreshTemplateSelector()
    {
        var previousUpdating = _updating;
        _updating = true;
        _template.BeginUpdate();
        while (_template.Items.Count < _templates.Count)
            _template.Items.Add("");
        while (_template.Items.Count > _templates.Count)
            _template.Items.RemoveAt(_template.Items.Count - 1);
        for (var index = 0; index < _templates.Count; index++)
        {
            var displayName = string.IsNullOrWhiteSpace(_templates[index].Name) ? $"Character {index + 1}" : _templates[index].Name;
            _template.Items[index] = $"{index + 1} · {displayName}";
        }
        _template.SelectedIndex = _activeTemplateIndex;
        _template.EndUpdate();
        _updating = previousUpdating;
    }

    private void SwitchTemplate(int index)
    {
        if (_updating || index < 0 || index >= _templates.Count || index == _activeTemplateIndex)
            return;
        _state.EquippedKeys = _items.Where(item => item.Equipped).Select(item => item.ProgressKey).OrderBy(value => value).ToList();
        _activeTemplateIndex = index;
        _state = _templates[index];
        foreach (var item in _items)
            item.Equipped = _state.EquippedKeys.Contains(item.ProgressKey, StringComparer.OrdinalIgnoreCase);
        LoadStateIntoControls();
        RefreshCalculations();
        ActiveTemplateChanged?.Invoke(index);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void LoadStateIntoControls()
    {
        _updating = true;
        _state.NormalizeClassLevels(!_state.Difficulty.Equals("Explorer", StringComparison.OrdinalIgnoreCase));
        _race.SelectedItem = CharacterCalculator.Races.Contains(_state.Race) ? _state.Race : "Human";
        _class.SelectedItem = CharacterCalculator.Classes.Contains(_state.ClassName) ? _state.ClassName : "Fighter";
        _difficulty.SelectedItem = CharacterCalculator.Difficulties.Contains(_state.Difficulty) ? _state.Difficulty : "Balanced";
        RefreshClassLevelControls();
        RefreshSubclassControl();
        foreach (var ability in CharacterCalculator.AbilityNames)
            _abilities[ability].Value = Math.Clamp(_state.GetAbility(ability), 3, 20);
        _difficultyInfo.Text = Localization.T("Difficulty" + (_difficulty.SelectedItem as string ?? "Balanced"));
        _characterName.Text = _state.Name;
        RefreshTemplateSelector();
        RefreshBuildOptionControls();
        _updating = false;
    }

    private void WireEvents()
    {
        _template.SelectedIndexChanged += (_, _) => SwitchTemplate(_template.SelectedIndex);
        _characterName.TextChanged += (_, _) =>
        {
            if (_updating)
                return;
            _state.Name = string.IsNullOrWhiteSpace(_characterName.Text) ? $"Character {_activeTemplateIndex + 1}" : _characterName.Text.Trim();
            RefreshTemplateSelector();
            RefreshShareId();
            StateChanged?.Invoke(this, EventArgs.Empty);
        };
        _copyShareLink.Click += (_, _) => CopyShareLink();
        _importShareId.Click += (_, _) => ImportShareId();
        _race.SelectedIndexChanged += (_, _) => UpdateStateFromControls();
        _class.SelectedIndexChanged += (_, _) => ChangeStartingClass();
        _subclass.SelectedIndexChanged += (_, _) => UpdateStateFromControls();
        foreach (var combo in _multiclassSubclasses.Values)
            combo.SelectedIndexChanged += (_, _) => UpdateStateFromControls();
        _difficulty.SelectedIndexChanged += (_, _) => UpdateStateFromControls();
        foreach (var box in _classLevels.Values)
            box.ValueChanged += (_, _) => UpdateStateFromControls();
        foreach (var box in _abilities.Values)
            box.ValueChanged += (_, _) => UpdateStateFromControls();
        foreach (var combo in _fightingStyles.Values)
            combo.SelectedIndexChanged += (_, _) => UpdateStateFromControls();
        foreach (var slot in _featSlots)
        {
            slot.Feat.SelectedIndexChanged += (_, _) =>
            {
                if (_updating) return;
                var index = (int)(slot.Feat.Tag ?? 0);
                RefreshFeatChoice(index, preserveChoice: false);
                UpdateStateFromControls();
                var definition = BuildOptions.FindFeat(slot.Feat.SelectedItem as string ?? "");
                if (definition is not null)
                    _featSummary.Text = Localization.Format("FeatSlots", BuildOptions.FeatSlotCount(_state)) + Environment.NewLine + definition.Description;
            };
            slot.Choice.SelectedIndexChanged += (_, _) => UpdateStateFromControls();
        }
        _buffs.SelectedIndexChanged += (_, _) =>
        {
            if (_buffs.SelectedItem is BuffOption option)
                _buffInfo.Text = option.Definition.Description + (option.Definition.Concentration ? "  [Concentration]" : "");
        };
        _buffs.ItemCheck += (_, eventArgs) => HandleBuffCheck(_buffs, eventArgs);
        _classOptions.SelectedIndexChanged += (_, _) =>
        {
            if (_classOptions.SelectedItem is BuffOption option)
                _classOptionInfo.Text = option.Definition.Description;
        };
        _classOptions.ItemCheck += (_, eventArgs) => HandleBuffCheck(_classOptions, eventArgs);
        _permanentBonuses.SelectedIndexChanged += (_, _) => RefreshPermanentBonusSelection();
        _permanentBonuses.ItemCheck += (_, eventArgs) => HandlePermanentBonusCheck(eventArgs);
        _permanentBonusChoice.SelectedIndexChanged += (_, _) => UpdatePermanentBonusChoice();
        _conditions.ItemCheck += (_, eventArgs) =>
        {
            if (_updating || _conditions.Items[eventArgs.Index] is not ConditionalEffectOption option)
                return;
            BeginInvoke(() =>
            {
                _state.SetEffectActive(option.Effect.Id, _conditions.GetItemChecked(eventArgs.Index));
                RefreshCalculations(rebuildConditions: false);
                StateChanged?.Invoke(this, EventArgs.Empty);
            });
        };
    }

    private void UpdateStateFromControls()
    {
        if (_updating)
            return;
        _state.Race = _race.SelectedItem as string ?? "Human";
        _state.ClassName = _class.SelectedItem as string ?? "Fighter";
        _state.Difficulty = _difficulty.SelectedItem as string ?? "Balanced";
        _difficultyInfo.Text = Localization.T("Difficulty" + _state.Difficulty);
        foreach (var pair in _classLevels)
            _state.ClassLevels[pair.Key] = (int)pair.Value.Value;
        _state.SetSubclass(_state.ClassName, _subclass.Enabled ? _subclass.SelectedItem as string : "");
        foreach (var pair in _multiclassSubclasses.Where(pair => !pair.Key.Equals(_state.ClassName, StringComparison.OrdinalIgnoreCase)))
            _state.SetSubclass(pair.Key, pair.Value.Enabled ? pair.Value.SelectedItem as string : "");
        _state.NormalizeClassLevels(!_state.Difficulty.Equals("Explorer", StringComparison.OrdinalIgnoreCase));
        RefreshClassLevelControls();
        var featSlotCount = BuildOptions.FeatSlotCount(_state);
        foreach (var pair in _fightingStyles)
            pair.Value.Enabled = BuildOptions.FightingStyleAvailable(_state, pair.Key);
        for (var index = 0; index < _featSlots.Count; index++)
        {
            _featSlots[index].Feat.Enabled = index < featSlotCount;
            _featSlots[index].Choice.Enabled = index < featSlotCount && BuildOptions.FindFeat(_featSlots[index].Feat.SelectedItem as string ?? "")?.Choices.Length > 0;
        }
        foreach (var pair in _abilities)
            _state.SetAbility(pair.Key, (int)pair.Value.Value);
        _state.ClearImportedAbilityTotals();
        foreach (var pair in _fightingStyles)
        {
            var style = pair.Value.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(style) || style == BuildOptions.None)
                _state.FightingStyles.Remove(pair.Key);
            else
                _state.FightingStyles[pair.Key] = style;
        }
        _state.Feats = _featSlots.Where(slot => slot.Feat.Enabled && slot.Feat.SelectedItem is string name && name != BuildOptions.None)
            .Select(slot => new FeatSelection
            {
                Name = slot.Feat.SelectedItem as string ?? "",
                Choice = slot.Choice.SelectedItem as string is { } choice && choice != "—" ? choice : ""
            }).ToList();
        _featSummary.Text = Localization.Format("FeatSlots", featSlotCount);
        RefreshBuildOptionControls();
        RefreshCalculations();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ChangeStartingClass()
    {
        if (_updating)
            return;
        var newClass = _class.SelectedItem as string ?? "Fighter";
        var previousClass = _state.ClassName;
        if (!newClass.Equals(previousClass, StringComparison.OrdinalIgnoreCase) && _state.GetClassLevel(newClass) == 0)
        {
            var previousLevel = _state.GetClassLevel(previousClass);
            if (previousLevel > 0)
                _state.ClassLevels[previousClass] = previousLevel - 1;
            _state.ClassLevels[newClass] = 1;
        }
        var newSubclass = _state.Subclasses.GetValueOrDefault(newClass, "");
        _state.ClassName = newClass;
        _state.SubclassName = newSubclass;
        _state.NormalizeClassLevels(!_state.Difficulty.Equals("Explorer", StringComparison.OrdinalIgnoreCase));
        RefreshClassLevelControls();
        UpdateStateFromControls();
    }

    private void RefreshClassLevelControls()
    {
        var previousUpdating = _updating;
        _updating = true;
        var multiclassAllowed = !_state.Difficulty.Equals("Explorer", StringComparison.OrdinalIgnoreCase);
        foreach (var pair in _classLevels)
        {
            pair.Value.Value = Math.Clamp(_state.GetClassLevel(pair.Key), 0, 12);
            pair.Value.Enabled = multiclassAllowed || pair.Key.Equals(_state.ClassName, StringComparison.OrdinalIgnoreCase);
        }
        _level.Value = Math.Clamp(_state.TotalLevel, 1, 12);
        _classLevelSummary.Text = Localization.Format("ClassLevelSummary", _state.TotalLevel, 12)
                                  + (multiclassAllowed ? "" : Environment.NewLine + Localization.T("ExplorerMulticlassDisabled"));
        RefreshSubclassControl();
        _updating = previousUpdating;
    }

    private void RefreshSubclassControl()
    {
        var previousUpdating = _updating;
        _updating = true;
        var className = _class.SelectedItem as string ?? _state.ClassName;
        var classLevel = _state.GetClassLevel(className);
        var requiredLevel = BuildOptions.SubclassLevel(className);
        var subclasses = BuildOptions.SubclassesByClass.GetValueOrDefault(className, []);
        _subclass.BeginUpdate();
        _subclass.Items.Clear();
        if (classLevel < requiredLevel)
            _subclass.Items.Add(Localization.Format("SubclassAtLevel", requiredLevel));
        else
            _subclass.Items.AddRange(subclasses);
        var selectedSubclass = _state.GetSubclass(className);
        var selected = classLevel >= requiredLevel && subclasses.Contains(selectedSubclass, StringComparer.OrdinalIgnoreCase)
            ? selectedSubclass
            : _subclass.Items[0] as string;
        _subclass.SelectedItem = selected;
        _subclass.Enabled = classLevel >= requiredLevel;
        _subclass.EndUpdate();
        foreach (var pair in _multiclassSubclasses)
        {
            var secondaryClass = pair.Key;
            var combo = pair.Value;
            var secondaryLevel = _state.GetClassLevel(secondaryClass);
            var secondaryRequiredLevel = BuildOptions.SubclassLevel(secondaryClass);
            var secondaryChoices = BuildOptions.SubclassesByClass.GetValueOrDefault(secondaryClass, []);
            combo.BeginUpdate();
            combo.Items.Clear();
            if (secondaryLevel < secondaryRequiredLevel)
                combo.Items.Add(Localization.Format("SubclassAtLevel", secondaryRequiredLevel));
            else
                combo.Items.AddRange(secondaryChoices);
            var secondarySelected = _state.GetSubclass(secondaryClass);
            combo.SelectedItem = secondaryLevel >= secondaryRequiredLevel
                                 && secondaryChoices.Contains(secondarySelected, StringComparer.OrdinalIgnoreCase)
                ? secondarySelected
                : combo.Items[0] as string;
            combo.Visible = secondaryLevel > 0 && !secondaryClass.Equals(className, StringComparison.OrdinalIgnoreCase);
            combo.Enabled = combo.Visible && secondaryLevel >= secondaryRequiredLevel;
            combo.EndUpdate();
        }
        _updating = previousUpdating;
    }

    private void RefreshBuildOptionControls()
    {
        var previousUpdating = _updating;
        _updating = true;
        foreach (var pair in _fightingStyles)
        {
            var selected = _state.FightingStyles.GetValueOrDefault(pair.Key, BuildOptions.None);
            pair.Value.BeginUpdate();
            pair.Value.Items.Clear();
            pair.Value.Items.Add(BuildOptions.None);
            pair.Value.Items.AddRange(BuildOptions.FightingStyleChoices(pair.Key));
            pair.Value.SelectedItem = pair.Value.Items.Contains(selected) ? selected : BuildOptions.None;
            pair.Value.Enabled = BuildOptions.FightingStyleAvailable(_state, pair.Key);
            pair.Value.EndUpdate();
        }

        var slotCount = BuildOptions.FeatSlotCount(_state);
        while (_state.Feats.Count > slotCount)
            _state.Feats.RemoveAt(_state.Feats.Count - 1);
        for (var index = 0; index < _featSlots.Count; index++)
        {
            var slot = _featSlots[index];
            var available = index < slotCount;
            var selected = available && index < _state.Feats.Count ? _state.Feats[index].Name : BuildOptions.None;
            slot.Feat.BeginUpdate();
            slot.Feat.Items.Clear();
            slot.Feat.Items.Add(BuildOptions.None);
            slot.Feat.Items.AddRange(BuildOptions.Feats.Select(feat => feat.Name).ToArray());
            slot.Feat.SelectedItem = slot.Feat.Items.Contains(selected) ? selected : BuildOptions.None;
            slot.Feat.Enabled = available;
            slot.Feat.EndUpdate();
            RefreshFeatChoice(index, preserveChoice: true);
        }
        _featSummary.Text = Localization.Format("FeatSlots", slotCount);

        _buffs.BeginUpdate();
        _buffs.Items.Clear();
        foreach (var buff in BuildOptions.Buffs.Where(buff => !BuildOptions.IsClassOption(buff.Name)))
            _buffs.Items.Add(new BuffOption(buff), _state.HasBuff(buff.Name));
        _buffs.EndUpdate();

        _classOptions.BeginUpdate();
        _classOptions.Items.Clear();
        foreach (var option in BuildOptions.AvailableClassOptions(_state))
        {
            var buff = BuildOptions.FindBuff(option.BuffName)!;
            _classOptions.Items.Add(new BuffOption(buff), _state.HasBuff(buff.Name));
        }
        if (_classOptions.Items.Count == 0)
            _classOptions.Items.Add(Localization.T("NoClassOptions"), false);
        _classOptions.EndUpdate();

        var selectedPermanentName = (_permanentBonuses.SelectedItem as PermanentBonusOption)?.Definition.Name;
        _permanentBonuses.BeginUpdate();
        _permanentBonuses.Items.Clear();
        foreach (var bonus in PermanentBonusCatalog.All)
            _permanentBonuses.Items.Add(new PermanentBonusOption(bonus), _state.HasPermanentBonus(bonus.Name));
        if (!string.IsNullOrWhiteSpace(selectedPermanentName))
        {
            var selectedIndex = _permanentBonuses.Items.Cast<PermanentBonusOption>()
                .ToList().FindIndex(option => option.Definition.Name.Equals(selectedPermanentName, StringComparison.OrdinalIgnoreCase));
            if (selectedIndex >= 0)
                _permanentBonuses.SelectedIndex = selectedIndex;
        }
        if (_permanentBonuses.SelectedIndex < 0 && _permanentBonuses.Items.Count > 0)
            _permanentBonuses.SelectedIndex = 0;
        _permanentBonuses.EndUpdate();
        RefreshPermanentBonusSelection();
        _updating = previousUpdating;
    }

    private void HandlePermanentBonusCheck(ItemCheckEventArgs eventArgs)
    {
        if (_updating || _permanentBonuses.Items[eventArgs.Index] is not PermanentBonusOption option)
            return;
        var enabled = eventArgs.NewValue == CheckState.Checked;
        BeginInvoke(() =>
        {
            _state.PermanentBonuses.RemoveAll(selection => selection.Name.Equals(option.Definition.Name, StringComparison.OrdinalIgnoreCase));
            if (enabled)
            {
                _state.PermanentBonuses.Add(new PermanentBonusSelection
                {
                    Name = option.Definition.Name,
                    Choice = option.Definition.Choices.FirstOrDefault() ?? ""
                });
            }
            RefreshPermanentBonusSelection();
            RefreshCalculations(rebuildConditions: false);
            StateChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    private void RefreshPermanentBonusSelection()
    {
        if (_permanentBonuses.SelectedItem is not PermanentBonusOption option)
            return;
        var wasUpdating = _updating;
        _updating = true;
        _permanentBonusInfo.Text = $"{option.Definition.Act} · {option.Definition.Description}";
        _permanentBonusChoice.BeginUpdate();
        _permanentBonusChoice.Items.Clear();
        if (option.Definition.Choices.Length == 0)
            _permanentBonusChoice.Items.Add("—");
        else
            _permanentBonusChoice.Items.AddRange(option.Definition.Choices);
        var selectedChoice = _state.PermanentBonusChoice(option.Definition.Name);
        _permanentBonusChoice.SelectedItem = _permanentBonusChoice.Items.Contains(selectedChoice)
            ? selectedChoice
            : _permanentBonusChoice.Items[0];
        _permanentBonusChoice.Enabled = _state.HasPermanentBonus(option.Definition.Name) && option.Definition.Choices.Length > 0;
        _permanentBonusChoice.EndUpdate();
        _updating = wasUpdating;
    }

    private void UpdatePermanentBonusChoice()
    {
        if (_updating || _permanentBonuses.SelectedItem is not PermanentBonusOption option || !_state.HasPermanentBonus(option.Definition.Name))
            return;
        var selection = _state.PermanentBonuses.First(bonus => bonus.Name.Equals(option.Definition.Name, StringComparison.OrdinalIgnoreCase));
        selection.Choice = _permanentBonusChoice.SelectedItem as string ?? "";
        RefreshCalculations(rebuildConditions: false);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RefreshFeatChoice(int index, bool preserveChoice)
    {
        if (index < 0 || index >= _featSlots.Count)
            return;
        var slot = _featSlots[index];
        var definition = BuildOptions.FindFeat(slot.Feat.SelectedItem as string ?? BuildOptions.None);
        var previous = preserveChoice && index < _state.Feats.Count ? _state.Feats[index].Choice : "";
        var wasUpdating = _updating;
        _updating = true;
        slot.Choice.BeginUpdate();
        slot.Choice.Items.Clear();
        if (definition is null || definition.Choices.Length == 0)
            slot.Choice.Items.Add("—");
        else
        {
            if (string.IsNullOrWhiteSpace(previous))
                slot.Choice.Items.Add("—");
            slot.Choice.Items.AddRange(definition.Choices);
        }
        slot.Choice.SelectedItem = slot.Choice.Items.Contains(previous) ? previous : slot.Choice.Items[0];
        slot.Choice.EndUpdate();
        slot.Choice.Enabled = slot.Feat.Enabled && definition?.Choices.Length > 0;
        _updating = wasUpdating;
        if (definition is not null)
            _featSummary.Text = definition.Description;
    }

    private void HandleBuffCheck(CheckedListBox source, ItemCheckEventArgs eventArgs)
    {
        if (_updating || source.Items[eventArgs.Index] is not BuffOption option)
            return;
        var enabled = eventArgs.NewValue == CheckState.Checked;
        BeginInvoke(() =>
        {
            _updating = true;
            if (enabled)
            {
                if (option.Definition.Concentration)
                    _state.ActiveBuffs.RemoveAll(value => BuildOptions.FindBuff(value)?.Concentration == true);
                if (option.Definition.Name.StartsWith("Aid (", StringComparison.OrdinalIgnoreCase))
                    _state.ActiveBuffs.RemoveAll(value => value.StartsWith("Aid (", StringComparison.OrdinalIgnoreCase));
                if (option.Definition.Name.StartsWith("Magic Weapon +", StringComparison.OrdinalIgnoreCase))
                    _state.ActiveBuffs.RemoveAll(value => value.StartsWith("Magic Weapon +", StringComparison.OrdinalIgnoreCase));
                if (option.Definition.Name.StartsWith("Mirror Image (", StringComparison.OrdinalIgnoreCase))
                    _state.ActiveBuffs.RemoveAll(value => value.StartsWith("Mirror Image (", StringComparison.OrdinalIgnoreCase));
                if (!_state.HasBuff(option.Definition.Name))
                    _state.ActiveBuffs.Add(option.Definition.Name);
            }
            else
                _state.ActiveBuffs.RemoveAll(value => value.Equals(option.Definition.Name, StringComparison.OrdinalIgnoreCase));
            _updating = false;
            RefreshBuildOptionControls();
            RefreshCalculations(rebuildConditions: false);
            StateChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    private void RefreshCalculations(bool rebuildConditions = true)
    {
        RefreshShareId();
        var stats = CharacterCalculator.Calculate(_state, _items);
        foreach (var ability in CharacterCalculator.AbilityNames)
            _abilityTotals[ability].Text = Localization.Format("AbilityTotal", stats.Abilities[ability]);
        _acValue.Text = stats.ArmourClass.ToString();
        _spellDcValue.Text = stats.SpellSaveDc.ToString();
        _criticalValue.Text = stats.CriticalThreshold == stats.SpellCriticalThreshold
            ? $"{stats.CriticalThreshold}+"
            : $"W{stats.CriticalThreshold}+  S{stats.SpellCriticalThreshold}+";
        SetCalculationToolTip(_acCard, stats.ArmourClassBreakdown);
        SetCalculationToolTip(_spellDcCard, stats.SpellSaveDcBreakdown);
        SetCalculationToolTip(_criticalCard, stats.CriticalBreakdown);
        var advantage = stats.AttackRollAdvantage == stats.AttackRollDisadvantage
            ? ""
            : stats.AttackRollAdvantage ? " • ADV" : " • DIS";
        var enemySaves = stats.EnemySavingThrowDisadvantage ? " • Enemy saves: DIS" : "";
        var blessAttack = FormatD4Bonus(stats.AttackBonusD4Count);
        _offense.Text = Localization.Format("OffenseLine", Signed(stats.WeaponAttack) + blessAttack, stats.AttackAbility, Signed(stats.SpellAttack) + blessAttack, stats.SpellAbility, stats.SpellClass, stats.Proficiency) + advantage + enemySaves;
        var defenseExtras = new List<string>();
        if (stats.CriticalHitImmune) defenseExtras.Add(Localization.T("NoCriticalHits"));
        if (stats.DamageReduction > 0) defenseExtras.Add(Localization.Format("DamageReduction", stats.DamageReduction));
        if (stats.Resistances.Count > 0) defenseExtras.Add(Localization.Format("Resistances", string.Join(", ", stats.Resistances)));
        if (stats.TemporaryHitPoints > 0) defenseExtras.Add(Localization.Format("TemporaryHitPoints", stats.TemporaryHitPoints));
        if (stats.NonProficientGear.Count > 0) defenseExtras.Add(Localization.Format("NonProficientGear", string.Join(", ", stats.NonProficientGear)));
        if (stats.BuildWarnings.Count > 0) defenseExtras.Add(Localization.Format("UnmetRequirements", stats.BuildWarnings.Count));
        _defense.Text = Localization.Format("DefenseLine", stats.HitPoints, Signed(stats.Initiative), stats.Movement) + (defenseExtras.Count == 0 ? "" : Environment.NewLine + string.Join(" • ", defenseExtras));
        var blessSave = FormatD4Bonus(stats.SavingThrowBonusD4Count);
        _saves.Text = Localization.T("SavingThrows") + ": " + string.Join("  ", CharacterCalculator.AbilityNames.Select(ability => $"{ability} {Signed(stats.Saves[ability])}{blessSave}"));
        _buildWarnings.Text = stats.BuildWarnings.Count == 0
            ? Localization.T("AllRequirementsMet")
            : Localization.T("UnmetRequirementsTitle") + Environment.NewLine + string.Join(Environment.NewLine, stats.BuildWarnings.Select(warning => "• " + warning));

        _enemyThreats.SuspendLayout();
        _characterThreats.SuspendLayout();
        _enemyThreats.Controls.Clear();
        _characterThreats.Controls.Clear();
        foreach (var threat in stats.Threats)
        {
            _enemyThreats.Controls.Add(CreateThreatCard(threat, characterChance: false));
            _characterThreats.Controls.Add(CreateThreatCard(threat, characterChance: true));
        }
        _enemyThreats.ResumeLayout();
        _characterThreats.ResumeLayout();

        _equipment.Items.Clear();
        foreach (var item in _items.Where(item => item.Equipped).OrderBy(GearRules.SlotFor).ThenBy(item => item.Name))
            _equipment.Items.Add($"{GearRules.SlotFor(item)}: {item.Name}");
        if (_equipment.Items.Count == 0)
            _equipment.Items.Add(Localization.T("NoGearEquipped"));

        if (rebuildConditions)
            RebuildConditionalEffects();
    }

    private void RebuildConditionalEffects()
    {
        var effects = ItemEffectParser.ParseEquipped(_items).Where(effect => effect.Conditional).ToList();
        _updating = true;
        _conditions.Items.Clear();
        foreach (var effect in effects)
        {
            var option = new ConditionalEffectOption(effect);
            _conditions.Items.Add(option, _state.IsEffectActive(effect));
        }
        if (effects.Count == 0)
            _conditions.Items.Add(Localization.T("NoConditionalEffects"), false);
        _updating = false;
    }

    private Control CreateThreatCard(ActThreat threat, bool characterChance)
    {
        var panel = new Panel
        {
            Width = 206,
            Height = 132,
            BackColor = Color.FromArgb(238, 220, 184),
            Margin = new Padding(0, 0, 0, 4),
            Padding = new Padding(7, 4, 7, 3)
        };
        var title = new Label
        {
            Dock = DockStyle.Top,
            Height = 20,
            Text = $"{threat.Act}  —  {Localization.T(threat.Benchmark)}",
            Font = Theme.Body(8.5f, FontStyle.Bold),
            ForeColor = Theme.Crimson
        };
        var values = new Label
        {
            Dock = DockStyle.Fill,
            Text = characterChance
                ? Localization.Format(
                    "CharacterThreatLine",
                    FormatChance(threat.CharacterWeaponHitChance), threat.TargetEnemy, threat.TargetArmourClass,
                    FormatChance(threat.CharacterSpellAttackHitChance), threat.TargetArmourClass,
                    threat.CharacterSpellSaveDc,
                    FormatSaveChances(threat.CharacterSpellEffectChances))
                : Localization.Format(
                    "EnemyThreatLine",
                    FormatChance(threat.AttackHitChance), threat.AttackEnemy, threat.AttackBonus,
                    FormatChance(threat.SpellAttackHitChance), threat.SpellEnemy, threat.SpellAttackBonus,
                    threat.SpellDc, FormatSaveChances(threat.SpellEffectChances)),
            Font = Theme.Body(7.35f),
            ForeColor = Theme.Ink
        };
        panel.Controls.Add(values);
        panel.Controls.Add(title);
        return panel;
    }

    private static string FormatSaveChances(IReadOnlyDictionary<string, double> chances) =>
        string.Join("  ", CharacterCalculator.AbilityNames.Select(ability => $"{ability} {FormatChance(chances[ability])}%"));

    private static void ConfigureThreatFlow(FlowLayoutPanel flow)
    {
        flow.Dock = DockStyle.Top;
        flow.AutoSize = true;
        flow.FlowDirection = FlowDirection.TopDown;
        flow.WrapContents = false;
        flow.Margin = new Padding(0);
    }

    private static string FormatChance(double chance)
    {
        var culture = Localization.Current == UiLanguage.Dutch
            ? System.Globalization.CultureInfo.GetCultureInfo("nl-BE")
            : System.Globalization.CultureInfo.InvariantCulture;
        return chance.ToString("0.##", culture);
    }

    private static Control HighlightCard(string title, Label value)
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.CrimsonDark, Margin = new Padding(2), Padding = new Padding(4) };
        var caption = new Label { Text = title, Dock = DockStyle.Top, Height = 23, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Theme.GoldLight, Font = Theme.Body(7.5f, FontStyle.Bold) };
        value.Dock = DockStyle.Fill;
        value.TextAlign = ContentAlignment.MiddleCenter;
        value.ForeColor = Color.White;
        value.Font = Theme.Heading(23f);
        panel.Controls.Add(value);
        panel.Controls.Add(caption);
        return panel;
    }

    private static Label Caption(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Top,
        AutoSize = true,
        ForeColor = Theme.Muted,
        Font = Theme.Body(7.5f, FontStyle.Bold),
        Margin = new Padding(2, 2, 2, 1)
    };

    private static void ConfigureCaption(Label label)
    {
        label.Dock = DockStyle.Top;
        label.AutoSize = true;
        label.ForeColor = Theme.Muted;
        label.Font = Theme.Body(7.5f, FontStyle.Bold);
        label.Margin = new Padding(2, 2, 2, 1);
    }

    private static void ConfigureCombo(ComboBox combo)
    {
        combo.Dock = DockStyle.Top;
        combo.DropDownStyle = ComboBoxStyle.DropDownList;
        combo.Margin = new Padding(2, 2, 2, 4);
        Theme.ConfigureModernCombo(combo, 9.5f);
    }

    private static void ConfigureSmallButton(Button button)
    {
        button.Dock = DockStyle.Fill;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = Theme.Gold;
        button.BackColor = Theme.CrimsonDark;
        button.ForeColor = Theme.GoldLight;
        button.Font = Theme.Body(7.25f, FontStyle.Bold);
        button.Margin = new Padding(2, 0, 0, 0);
    }

    private static void ConfigureHeading(Label label, float size, Color color, Padding margin)
    {
        label.AutoSize = true;
        label.ForeColor = color;
        label.Font = Theme.Heading(size);
        label.Margin = margin;
    }

    private static void ConfigureSection(Label label)
    {
        label.AutoSize = true;
        label.ForeColor = Theme.Crimson;
        label.Font = Theme.Body(9f, FontStyle.Bold);
        label.Margin = new Padding(0, 8, 0, 3);
    }

    private static void ConfigureBody(Label label, bool italic = false)
    {
        label.AutoSize = true;
        label.MaximumSize = new Size(310, 0);
        label.ForeColor = Theme.Ink;
        label.Font = Theme.Body(8f, italic ? FontStyle.Italic : FontStyle.Regular);
        label.Margin = new Padding(0, 0, 0, 3);
    }

    private static string Signed(int value) => value >= 0 ? $"+{value}" : value.ToString();

    private static string FormatD4Bonus(int count) => count switch
    {
        <= 0 => "",
        1 => " + 1d4",
        _ => $" + {count}d4"
    };

    private void RefreshShareId()
    {
        try
        {
            _shareId.Text = TemplateShareService.ExportId(_state);
        }
        catch
        {
            _shareId.Text = _state.TemplateId;
        }
    }

    private void CopyShareLink()
    {
        try
        {
            Clipboard.SetText(TemplateShareService.ExportLink(_state));
            _copyShareLink.Text = Localization.T("Copied");
            var resetTimer = new System.Windows.Forms.Timer { Interval = 1400 };
            resetTimer.Tick += (_, _) =>
            {
                resetTimer.Stop();
                resetTimer.Dispose();
                if (!IsDisposed)
                    _copyShareLink.Text = Localization.T("CopyLink");
            };
            resetTimer.Start();
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, Localization.Format("ShareCopyError", exception.Message), Localization.T("CharacterTemplate"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ImportShareId()
    {
        var input = PromptForShareId();
        if (string.IsNullOrWhiteSpace(input))
            return;
        try
        {
            var imported = TemplateShareService.Import(input);
            TemplateShareService.CopyInto(_state, imported);
            var equipped = _state.EquippedKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var item in _items)
                item.Equipped = equipped.Contains(item.ProgressKey);
            LoadStateIntoControls();
            RefreshCalculations();
            StateChanged?.Invoke(this, EventArgs.Empty);
            MessageBox.Show(this, Localization.T("ImportSuccess"), Localization.T("ImportTemplate"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, Localization.Format("ImportError", exception.Message), Localization.T("ImportTemplate"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private string? PromptForShareId()
    {
        using var dialog = new Form
        {
            Text = Localization.T("ImportTemplate"),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ClientSize = new Size(620, 145),
            BackColor = Theme.ParchmentAlt,
            Font = Theme.Body(9f)
        };
        var prompt = new Label
        {
            Text = Localization.T("PasteShareId"),
            AutoSize = true,
            Location = new Point(12, 12),
            ForeColor = Theme.Ink
        };
        var input = new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Location = new Point(12, 36),
            Size = new Size(596, 62),
            BackColor = Theme.Parchment
        };
        var import = new Button
        {
            Text = Localization.T("Import"),
            DialogResult = DialogResult.OK,
            Location = new Point(430, 108),
            Size = new Size(86, 27)
        };
        var cancel = new Button
        {
            Text = Localization.T("Cancel"),
            DialogResult = DialogResult.Cancel,
            Location = new Point(522, 108),
            Size = new Size(86, 27)
        };
        dialog.Controls.AddRange([prompt, input, import, cancel]);
        dialog.AcceptButton = import;
        dialog.CancelButton = cancel;
        return dialog.ShowDialog(this) == DialogResult.OK ? input.Text : null;
    }

    private void SetCalculationToolTip(Control? root, string text)
    {
        if (root is null)
            return;
        _calculationToolTip.SetToolTip(root, text);
        foreach (Control child in root.Controls)
            SetCalculationToolTip(child, text);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _calculationToolTip.Dispose();
        base.Dispose(disposing);
    }

    private sealed record ConditionalEffectOption(ItemEffect Effect)
    {
        public override string ToString() => $"{Effect.ItemName}: {Effect.Summary}";
    }

    private sealed record FeatSlotControls(ComboBox Feat, ComboBox Choice);

    private sealed record PermanentBonusOption(PermanentBonusDefinition Definition)
    {
        public override string ToString() => $"{Definition.Act} - {Definition.Name}";
    }

    private sealed record BuffOption(BuffDefinition Definition)
    {
        public override string ToString() => Definition.Name + (Definition.Concentration ? "  [C]" : "");
    }
}
