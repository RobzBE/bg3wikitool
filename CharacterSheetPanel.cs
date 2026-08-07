namespace BG3ItemExplorer;

internal sealed class CharacterSheetPanel : UserControl
{
    private CharacterState _state;
    private readonly IReadOnlyList<CharacterState> _templates;
    private int _activeTemplateIndex;
    private readonly List<ItemRecord> _items;
    private readonly ComboBox _template = new();
    private readonly TextBox _characterName = new();
    private readonly ComboBox _race = new();
    private readonly ComboBox _class = new();
    private readonly ComboBox _difficulty = new();
    private readonly NumericUpDown _level = new();
    private readonly Dictionary<string, NumericUpDown> _abilities = [];
    private readonly Dictionary<string, NumericUpDown> _classLevels = [];
    private readonly Dictionary<string, ComboBox> _fightingStyles = [];
    private readonly List<FeatSlotControls> _featSlots = [];
    private readonly Label _title = new();
    private readonly Label _templateCaption = new();
    private readonly Label _characterNameCaption = new();
    private readonly Label _identityCaption = new();
    private readonly Label _abilityCaption = new();
    private readonly Label _classLevelsCaption = new();
    private readonly Label _classLevelSummary = new();
    private readonly Label _classFeaturesCaption = new();
    private readonly Label _featsCaption = new();
    private readonly Label _featSummary = new();
    private readonly Label _activeBuffsCaption = new();
    private readonly Label _buffInfo = new();
    private readonly Label _buildWarnings = new();
    private readonly Label _offenseCaption = new();
    private readonly Label _defenseCaption = new();
    private readonly Label _threatCaption = new();
    private readonly Label _equipmentCaption = new();
    private readonly Label _conditionsCaption = new();
    private readonly Label _raceCaption = new();
    private readonly Label _classCaption = new();
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
    private readonly FlowLayoutPanel _threats = new();
    private readonly ListBox _equipment = new();
    private readonly CheckedListBox _conditions = new();
    private readonly CheckedListBox _buffs = new();
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
        AutoScroll = true;
        Build();
        LoadStateIntoControls();
        WireEvents();
        RefreshCalculations();
    }

    public void RefreshFromEquipment()
    {
        _state.EquippedKeys = _items.Where(item => item.Equipped).Select(item => item.ProgressKey).OrderBy(value => value).ToList();
        RefreshCalculations();
    }

    public CharacterState CurrentState => _state;
    public int ActiveTemplateIndex => _activeTemplateIndex;
    public bool CalculationToolTipsReady =>
        !string.IsNullOrWhiteSpace(_calculationToolTip.GetToolTip(_acValue))
        && !string.IsNullOrWhiteSpace(_calculationToolTip.GetToolTip(_spellDcValue))
        && !string.IsNullOrWhiteSpace(_calculationToolTip.GetToolTip(_criticalValue));

    public void SetLanguage()
    {
        _title.Text = Localization.T("CharacterSheet");
        _templateCaption.Text = Localization.T("CharacterTemplate");
        _characterNameCaption.Text = Localization.T("CharacterName");
        _identityCaption.Text = Localization.T("Identity");
        _abilityCaption.Text = Localization.T("BaseAbilities");
        _offenseCaption.Text = Localization.T("Offense");
        _defenseCaption.Text = Localization.T("Defense");
        _threatCaption.Text = Localization.T("EnemyHitChance");
        _equipmentCaption.Text = Localization.T("EquippedGear");
        _conditionsCaption.Text = Localization.T("ActiveConditions");
        _raceCaption.Text = Localization.T("Race");
        _classCaption.Text = Localization.T("StartClass");
        _levelCaption.Text = Localization.T("TotalLevel");
        _classLevelsCaption.Text = Localization.T("ClassLevels");
        _classFeaturesCaption.Text = Localization.T("ClassFeatures");
        _featsCaption.Text = Localization.T("Feats");
        _activeBuffsCaption.Text = Localization.T("ActiveSpellsConditions");
        _buildTab.Text = Localization.T("BuildTab");
        _statsTab.Text = Localization.T("StatsTab");
        _difficultyCaption.Text = Localization.T("Difficulty");
        _difficultyInfo.Text = Localization.T("Difficulty" + (_difficulty.SelectedItem as string ?? _state.Difficulty));
        _benchmarkNote.Text = Localization.T("BenchmarkNote");
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
            Height = 58,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(10, 4, 10, 4),
            BackColor = Theme.ParchmentAlt
        };
        templateBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        templateBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        ConfigureCaption(_templateCaption);
        ConfigureCaption(_characterNameCaption);
        ConfigureCombo(_template);
        _characterName.Dock = DockStyle.Top;
        _characterName.Font = Theme.Body(8.5f);
        _characterName.BackColor = Theme.Parchment;
        _characterName.MaxLength = 40;
        templateBar.Controls.Add(_templateCaption, 0, 0);
        templateBar.Controls.Add(_characterNameCaption, 1, 0);
        templateBar.Controls.Add(_template, 0, 1);
        templateBar.Controls.Add(_characterName, 1, 1);
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
        ConfigureCombo(_difficulty);
        _race.Items.AddRange(CharacterCalculator.Races);
        _class.Items.AddRange(CharacterCalculator.Classes);
        _difficulty.Items.AddRange(CharacterCalculator.Difficulties);
        ConfigureCaption(_raceCaption);
        ConfigureCaption(_classCaption);
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
        content.Controls.Add(identity);

        ConfigureSection(_classLevelsCaption);
        content.Controls.Add(_classLevelsCaption);
        var classLevels = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Margin = new Padding(0, 0, 0, 4) };
        classLevels.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        classLevels.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        for (var index = 0; index < CharacterCalculator.Classes.Length; index++)
        {
            var className = CharacterCalculator.Classes[index];
            var box = new NumericUpDown
            {
                Minimum = 0,
                Maximum = 12,
                Dock = DockStyle.Top,
                Font = Theme.Body(8.5f),
                Tag = className,
                Margin = new Padding(2)
            };
            _classLevels[className] = box;
            var cell = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, Margin = new Padding(1) };
            cell.Controls.Add(Caption(className));
            cell.Controls.Add(box);
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
            var box = new NumericUpDown
            {
                Minimum = 3,
                Maximum = 20,
                Dock = DockStyle.Top,
                Font = Theme.Body(9f),
                Tag = ability,
                Margin = new Padding(2)
            };
            _abilities[ability] = box;
            var cell = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, Margin = new Padding(1) };
            cell.Controls.Add(Caption(ability));
            cell.Controls.Add(box);
            abilities.Controls.Add(cell, index % 3, index / 3);
        }
        content.Controls.Add(abilities);

        ConfigureSection(_classFeaturesCaption);
        content.Controls.Add(_classFeaturesCaption);
        var styles = new TableLayoutPanel { Dock = DockStyle.Top, Height = 92, AutoSize = false, ColumnCount = 2, RowCount = 3, Margin = new Padding(0, 0, 0, 4) };
        styles.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        styles.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
        var fightingStyleClasses = new[] { "Fighter", "Paladin", "Ranger" };
        for (var index = 0; index < fightingStyleClasses.Length; index++)
        {
            var className = fightingStyleClasses[index];
            var combo = new ComboBox();
            ConfigureCombo(combo);
            _fightingStyles[className] = combo;
            styles.Controls.Add(Caption(className), 0, index);
            styles.Controls.Add(combo, 1, index);
        }
        content.Controls.Add(styles);

        ConfigureSection(_featsCaption);
        content.Controls.Add(_featsCaption);
        var feats = new TableLayoutPanel { Dock = DockStyle.Top, Height = 124, AutoSize = false, ColumnCount = 2, RowCount = 4, Margin = new Padding(0, 0, 0, 3) };
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
        _buffs.Font = Theme.Body(8f);
        _buffs.BackColor = Theme.Parchment;
        content.Controls.Add(_buffs);
        ConfigureBody(_buffInfo, true);
        content.Controls.Add(_buffInfo);
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

        ConfigureSection(_threatCaption);
        statsContent.Controls.Add(_threatCaption);
        _threats.Dock = DockStyle.Top;
        _threats.AutoSize = true;
        _threats.FlowDirection = FlowDirection.TopDown;
        _threats.WrapContents = false;
        _threats.Margin = new Padding(0, 0, 0, 3);
        statsContent.Controls.Add(_threats);
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
        _conditions.Font = Theme.Body(8f);
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
            StateChanged?.Invoke(this, EventArgs.Empty);
        };
        _race.SelectedIndexChanged += (_, _) => UpdateStateFromControls();
        _class.SelectedIndexChanged += (_, _) => ChangeStartingClass();
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
        _buffs.ItemCheck += (_, eventArgs) => HandleBuffCheck(eventArgs);
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
                Choice = slot.Choice.SelectedItem as string ?? ""
            }).ToList();
        _featSummary.Text = Localization.Format("FeatSlots", featSlotCount);
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
        _state.ClassName = newClass;
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
            pair.Value.Items.AddRange(BuildOptions.FightingStylesByClass[pair.Key]);
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
        foreach (var buff in BuildOptions.Buffs)
            _buffs.Items.Add(new BuffOption(buff), _state.HasBuff(buff.Name));
        _buffs.EndUpdate();
        _updating = previousUpdating;
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
            slot.Choice.Items.AddRange(definition.Choices);
        slot.Choice.SelectedItem = slot.Choice.Items.Contains(previous) ? previous : slot.Choice.Items[0];
        slot.Choice.EndUpdate();
        slot.Choice.Enabled = slot.Feat.Enabled && definition?.Choices.Length > 0;
        _updating = wasUpdating;
        if (definition is not null)
            _featSummary.Text = definition.Description;
    }

    private void HandleBuffCheck(ItemCheckEventArgs eventArgs)
    {
        if (_updating || _buffs.Items[eventArgs.Index] is not BuffOption option)
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
        var stats = CharacterCalculator.Calculate(_state, _items);
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
        var blessAttack = stats.AttackBonusDie > 0 ? $" + 1d{stats.AttackBonusDie}" : "";
        _offense.Text = Localization.Format("OffenseLine", Signed(stats.WeaponAttack) + blessAttack, stats.AttackAbility, Signed(stats.SpellAttack) + blessAttack, stats.SpellAbility, stats.SpellClass, stats.Proficiency) + advantage + enemySaves;
        var defenseExtras = new List<string>();
        if (stats.CriticalHitImmune) defenseExtras.Add(Localization.T("NoCriticalHits"));
        if (stats.DamageReduction > 0) defenseExtras.Add(Localization.Format("DamageReduction", stats.DamageReduction));
        if (stats.Resistances.Count > 0) defenseExtras.Add(Localization.Format("Resistances", string.Join(", ", stats.Resistances)));
        if (stats.NonProficientGear.Count > 0) defenseExtras.Add(Localization.Format("NonProficientGear", string.Join(", ", stats.NonProficientGear)));
        if (stats.BuildWarnings.Count > 0) defenseExtras.Add(Localization.Format("UnmetRequirements", stats.BuildWarnings.Count));
        _defense.Text = Localization.Format("DefenseLine", stats.HitPoints, Signed(stats.Initiative), stats.Movement) + (defenseExtras.Count == 0 ? "" : Environment.NewLine + string.Join(" • ", defenseExtras));
        var blessSave = stats.SavingThrowBonusDie > 0 ? $" + 1d{stats.SavingThrowBonusDie}" : "";
        _saves.Text = Localization.T("SavingThrows") + ": " + string.Join("  ", CharacterCalculator.AbilityNames.Select(ability => $"{ability} {Signed(stats.Saves[ability])}{blessSave}"));
        _buildWarnings.Text = stats.BuildWarnings.Count == 0
            ? Localization.T("AllRequirementsMet")
            : Localization.T("UnmetRequirementsTitle") + Environment.NewLine + string.Join(Environment.NewLine, stats.BuildWarnings.Select(warning => "• " + warning));

        _threats.SuspendLayout();
        _threats.Controls.Clear();
        foreach (var threat in stats.Threats)
            _threats.Controls.Add(CreateThreatCard(threat));
        _threats.ResumeLayout();

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

    private Control CreateThreatCard(ActThreat threat)
    {
        var panel = new Panel
        {
            Width = Math.Max(210, ClientSize.Width - 35),
            Height = 104,
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
            Text = Localization.Format(
                "ThreatLine",
                FormatChance(threat.AttackHitChance), threat.AttackEnemy, threat.AttackBonus,
                FormatChance(threat.SpellAttackHitChance), threat.SpellEnemy, threat.SpellAttackBonus,
                FormatChance(threat.SpellEffectChance), threat.SpellDc, threat.SpellSaveAbility),
            Font = Theme.Body(8f),
            ForeColor = Theme.Ink
        };
        panel.Controls.Add(values);
        panel.Controls.Add(title);
        return panel;
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
        combo.Font = Theme.Body(8.5f);
        combo.BackColor = Theme.Parchment;
        combo.Margin = new Padding(2);
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

    private sealed record BuffOption(BuffDefinition Definition)
    {
        public override string ToString() => Definition.Name + (Definition.Concentration ? "  [C]" : "");
    }
}
