namespace BG3ItemExplorer;

internal sealed class CharacterSheetPanel : UserControl
{
    private readonly CharacterState _state;
    private readonly List<ItemRecord> _items;
    private readonly ComboBox _race = new();
    private readonly ComboBox _class = new();
    private readonly ComboBox _difficulty = new();
    private readonly NumericUpDown _level = new();
    private readonly Dictionary<string, NumericUpDown> _abilities = [];
    private readonly Label _title = new();
    private readonly Label _identityCaption = new();
    private readonly Label _abilityCaption = new();
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
    private readonly Label _offense = new();
    private readonly Label _defense = new();
    private readonly Label _saves = new();
    private readonly FlowLayoutPanel _threats = new();
    private readonly ListBox _equipment = new();
    private readonly CheckedListBox _conditions = new();
    private readonly Label _benchmarkNote = new();
    private bool _updating;

    public event EventHandler? StateChanged;

    public CharacterSheetPanel(CharacterState state, List<ItemRecord> items)
    {
        _state = state;
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

    public void SetLanguage()
    {
        _title.Text = Localization.T("CharacterSheet");
        _identityCaption.Text = Localization.T("Identity");
        _abilityCaption.Text = Localization.T("BaseAbilities");
        _offenseCaption.Text = Localization.T("Offense");
        _defenseCaption.Text = Localization.T("Defense");
        _threatCaption.Text = Localization.T("EnemyHitChance");
        _equipmentCaption.Text = Localization.T("EquippedGear");
        _conditionsCaption.Text = Localization.T("ActiveConditions");
        _raceCaption.Text = Localization.T("Race");
        _classCaption.Text = Localization.T("Class");
        _levelCaption.Text = Localization.T("Level");
        _difficultyCaption.Text = Localization.T("Difficulty");
        _difficultyInfo.Text = Localization.T("Difficulty" + (_difficulty.SelectedItem as string ?? _state.Difficulty));
        _benchmarkNote.Text = Localization.T("BenchmarkNote");
        RefreshCalculations();
    }

    private void Build()
    {
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
        _level.Dock = DockStyle.Top;
        _level.Font = Theme.Body(9f);
        identity.Controls.Add(_level, 0, 3);
        identity.Controls.Add(_difficultyCaption, 1, 2);
        identity.Controls.Add(_difficulty, 1, 3);
        ConfigureBody(_difficultyInfo, true);
        identity.Controls.Add(_difficultyInfo, 0, 4);
        identity.SetColumnSpan(_difficultyInfo, 2);
        content.Controls.Add(identity);

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

        var highlights = new TableLayoutPanel { Dock = DockStyle.Top, Height = 82, ColumnCount = 2, Margin = new Padding(0, 2, 0, 10) };
        highlights.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        highlights.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        highlights.Controls.Add(HighlightCard("AC", _acValue), 0, 0);
        highlights.Controls.Add(HighlightCard("SPELL SAVE DC", _spellDcValue), 1, 0);
        content.Controls.Add(highlights);

        ConfigureSection(_offenseCaption);
        content.Controls.Add(_offenseCaption);
        ConfigureBody(_offense);
        content.Controls.Add(_offense);

        ConfigureSection(_defenseCaption);
        content.Controls.Add(_defenseCaption);
        ConfigureBody(_defense);
        ConfigureBody(_saves);
        content.Controls.Add(_defense);
        content.Controls.Add(_saves);

        ConfigureSection(_threatCaption);
        content.Controls.Add(_threatCaption);
        _threats.Dock = DockStyle.Top;
        _threats.AutoSize = true;
        _threats.FlowDirection = FlowDirection.TopDown;
        _threats.WrapContents = false;
        _threats.Margin = new Padding(0, 0, 0, 3);
        content.Controls.Add(_threats);
        ConfigureBody(_benchmarkNote, true);
        content.Controls.Add(_benchmarkNote);

        ConfigureSection(_equipmentCaption);
        content.Controls.Add(_equipmentCaption);
        _equipment.Dock = DockStyle.Top;
        _equipment.Height = 118;
        _equipment.Font = Theme.Body(8.25f);
        _equipment.BackColor = Theme.Parchment;
        content.Controls.Add(_equipment);

        ConfigureSection(_conditionsCaption);
        content.Controls.Add(_conditionsCaption);
        _conditions.Dock = DockStyle.Top;
        _conditions.Height = 86;
        _conditions.CheckOnClick = true;
        _conditions.Font = Theme.Body(8f);
        _conditions.BackColor = Theme.Parchment;
        content.Controls.Add(_conditions);

        Controls.Add(content);
        SetLanguage();
    }

    private void LoadStateIntoControls()
    {
        _updating = true;
        _race.SelectedItem = CharacterCalculator.Races.Contains(_state.Race) ? _state.Race : "Human";
        _class.SelectedItem = CharacterCalculator.Classes.Contains(_state.ClassName) ? _state.ClassName : "Fighter";
        _difficulty.SelectedItem = CharacterCalculator.Difficulties.Contains(_state.Difficulty) ? _state.Difficulty : "Balanced";
        _level.Value = Math.Clamp(_state.Level, 1, 12);
        foreach (var ability in CharacterCalculator.AbilityNames)
            _abilities[ability].Value = Math.Clamp(_state.GetAbility(ability), 3, 20);
        _difficultyInfo.Text = Localization.T("Difficulty" + (_difficulty.SelectedItem as string ?? "Balanced"));
        _updating = false;
    }

    private void WireEvents()
    {
        _race.SelectedIndexChanged += (_, _) => UpdateStateFromControls();
        _class.SelectedIndexChanged += (_, _) => UpdateStateFromControls();
        _difficulty.SelectedIndexChanged += (_, _) => UpdateStateFromControls();
        _level.ValueChanged += (_, _) => UpdateStateFromControls();
        foreach (var box in _abilities.Values)
            box.ValueChanged += (_, _) => UpdateStateFromControls();
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
        _state.Level = (int)_level.Value;
        foreach (var pair in _abilities)
            _state.SetAbility(pair.Key, (int)pair.Value.Value);
        RefreshCalculations();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RefreshCalculations(bool rebuildConditions = true)
    {
        var stats = CharacterCalculator.Calculate(_state, _items);
        _acValue.Text = stats.ArmourClass.ToString();
        _spellDcValue.Text = stats.SpellSaveDc.ToString();
        var advantage = stats.AttackRollAdvantage == stats.AttackRollDisadvantage
            ? ""
            : stats.AttackRollAdvantage ? " • ADV" : " • DIS";
        var enemySaves = stats.EnemySavingThrowDisadvantage ? " • Enemy saves: DIS" : "";
        _offense.Text = Localization.Format("OffenseLine", Signed(stats.WeaponAttack), stats.AttackAbility, Signed(stats.SpellAttack), stats.SpellAbility, stats.Proficiency) + advantage + enemySaves;
        var defenseExtras = new List<string>();
        if (stats.CriticalHitImmune) defenseExtras.Add(Localization.T("NoCriticalHits"));
        if (stats.DamageReduction > 0) defenseExtras.Add(Localization.Format("DamageReduction", stats.DamageReduction));
        if (stats.Resistances.Count > 0) defenseExtras.Add(Localization.Format("Resistances", string.Join(", ", stats.Resistances)));
        if (stats.NonProficientGear.Count > 0) defenseExtras.Add(Localization.Format("NonProficientGear", string.Join(", ", stats.NonProficientGear)));
        _defense.Text = Localization.Format("DefenseLine", stats.HitPoints, Signed(stats.Initiative), stats.Movement) + (defenseExtras.Count == 0 ? "" : Environment.NewLine + string.Join(" • ", defenseExtras));
        _saves.Text = Localization.T("SavingThrows") + ": " + string.Join("  ", CharacterCalculator.AbilityNames.Select(ability => $"{ability} {Signed(stats.Saves[ability])}"));

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
            Height = 82,
            BackColor = Color.FromArgb(238, 220, 184),
            Margin = new Padding(0, 0, 0, 4),
            Padding = new Padding(7, 4, 7, 3)
        };
        var title = new Label
        {
            Dock = DockStyle.Top,
            Height = 20,
            Text = $"{threat.Act}  —  {Localization.T("WorstCase")}",
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

    private sealed record ConditionalEffectOption(ItemEffect Effect)
    {
        public override string ToString() => $"{Effect.ItemName}: {Effect.Summary}";
    }
}
