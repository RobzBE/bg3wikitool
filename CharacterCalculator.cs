using System.Text.RegularExpressions;

namespace BG3ItemExplorer;

internal sealed record ClassProfile(
    string SpellAbility,
    string AttackAbility,
    string SaveOne,
    string SaveTwo,
    int StartingHp,
    int HpPerLevel,
    string[] ArmourTraining,
    string[] MulticlassArmourTraining);

internal sealed record EnemyThreatProfile(
    string Act,
    string AttackEnemy,
    int AttackBonus,
    string AttackCreatureType,
    string SpellEnemy,
    int SpellDc,
    string SpellCreatureType,
    EnemyDefenseProfile Defense);

internal sealed record EnemyDefenseProfile(
    string Enemy,
    int ArmourClass,
    IReadOnlyDictionary<string, int> Saves,
    bool MagicResistance = false);

internal sealed record ActThreat(
    string Act,
    string Benchmark,
    string AttackEnemy,
    int AttackBonus,
    string SpellEnemy,
    int SpellAttackBonus,
    int SpellDc,
    double AttackHitChance,
    double SpellAttackHitChance,
    double SpellEffectChance,
    string SpellSaveAbility,
    IReadOnlyDictionary<string, double> SpellEffectChances,
    string TargetEnemy,
    int TargetArmourClass,
    double CharacterWeaponHitChance,
    double CharacterSpellAttackHitChance,
    int CharacterSpellSaveDc,
    IReadOnlyDictionary<string, double> CharacterSpellEffectChances);

internal sealed class CharacterStats
{
    public Dictionary<string, int> Abilities { get; init; } = [];
    public Dictionary<string, int> Saves { get; init; } = [];
    public int Proficiency { get; init; }
    public int ArmourClass { get; init; }
    public int SpellSaveDc { get; init; }
    public int SpellAttack { get; init; }
    public int WeaponAttack { get; init; }
    public int HitPoints { get; init; }
    public int Initiative { get; init; }
    public decimal Movement { get; init; }
    public string SpellAbility { get; init; } = "INT";
    public string SpellClass { get; init; } = "Wizard";
    public string AttackAbility { get; init; } = "STR";
    public List<ActThreat> Threats { get; init; } = [];
    public List<ItemEffect> ActiveEffects { get; init; } = [];
    public bool AttackRollAdvantage { get; init; }
    public bool AttackRollDisadvantage { get; init; }
    public bool EnemySavingThrowDisadvantage { get; init; }
    public bool CriticalHitImmune { get; init; }
    public int DamageReduction { get; init; }
    public List<string> Resistances { get; init; } = [];
    public List<string> NonProficientGear { get; init; } = [];
    public List<string> BuildWarnings { get; init; } = [];
    public int AttackBonusDie { get; init; }
    public int SavingThrowBonusDie { get; init; }
    public int AttackBonusD4Count { get; init; }
    public int SavingThrowBonusD4Count { get; init; }
    public int TemporaryHitPoints { get; init; }
    public int CriticalThreshold { get; init; } = 20;
    public int SpellCriticalThreshold { get; init; } = 20;
    public string ArmourClassBreakdown { get; init; } = "";
    public string SpellSaveDcBreakdown { get; init; } = "";
    public string CriticalBreakdown { get; init; } = "";
}

internal static partial class CharacterCalculator
{
    public static readonly string[] Classes = ["Barbarian", "Bard", "Cleric", "Druid", "Fighter", "Monk", "Paladin", "Ranger", "Rogue", "Sorcerer", "Warlock", "Wizard"];
    public static readonly string[] Races = ["Human", "Elf", "Drow", "Half-Elf", "Half-Orc", "Halfling", "Dwarf", "Gnome", "Githyanki", "Dragonborn", "Tiefling"];
    public static readonly string[] Difficulties = ["Explorer", "Balanced", "Tactician", "Honour"];
    public static readonly string[] AbilityNames = ["STR", "DEX", "CON", "INT", "WIS", "CHA"];

    private static readonly Dictionary<string, ClassProfile> Profiles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Barbarian"] = new("CHA", "STR", "STR", "CON", 12, 7, ["Light", "Medium", "Shield"], ["Shield"]),
        ["Bard"] = new("CHA", "DEX", "DEX", "CHA", 8, 5, ["Light"], ["Light"]),
        ["Cleric"] = new("WIS", "WIS", "WIS", "CHA", 8, 5, ["Light", "Medium", "Shield"], ["Light", "Medium", "Shield"]),
        ["Druid"] = new("WIS", "WIS", "INT", "WIS", 8, 5, ["Light", "Medium", "Shield"], ["Light", "Medium", "Shield"]),
        ["Fighter"] = new("INT", "STR", "STR", "CON", 10, 6, ["Light", "Medium", "Heavy", "Shield"], ["Light", "Medium", "Shield"]),
        ["Monk"] = new("WIS", "DEX", "STR", "DEX", 8, 5, [], []),
        ["Paladin"] = new("CHA", "STR", "WIS", "CHA", 10, 6, ["Light", "Medium", "Heavy", "Shield"], ["Light", "Medium", "Shield"]),
        ["Ranger"] = new("WIS", "DEX", "STR", "DEX", 10, 6, ["Light", "Medium", "Shield"], ["Light", "Medium", "Shield"]),
        ["Rogue"] = new("INT", "DEX", "DEX", "INT", 8, 5, ["Light"], ["Light"]),
        ["Sorcerer"] = new("CHA", "CHA", "CON", "CHA", 6, 4, [], []),
        ["Warlock"] = new("CHA", "CHA", "WIS", "CHA", 8, 5, ["Light"], ["Light"]),
        ["Wizard"] = new("INT", "INT", "INT", "WIS", 6, 4, [], [])
    };

    private static readonly Dictionary<string, decimal> RaceMovement = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Halfling"] = 7.5m,
        ["Dwarf"] = 7.5m,
        ["Gnome"] = 7.5m
    };

    // Worst-case hostile encounter baselines from the BG3 Wiki creature sheets.
    // Attack bonuses include the creature's ability modifier, proficiency and the
    // difficulty modifier. Spell attack is derived from the listed casting DC (DC - 8).
    private static readonly Dictionary<string, EnemyThreatProfile[]> WorstCaseThreats = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Balanced"] =
        [
            new("ACT 1", "Grym", 11, "Construct", "Grym", 19, "Construct", Defense("Grym", 18, 7, 2, 7, -4, 4, -5)),
            new("ACT 2", "Apostle of Myrkul", 10, "Undead", "Apostle of Myrkul", 17, "Undead", Defense("Apostle of Myrkul", 19, 6, 1, 6, 2, 7, 9)),
            new("ACT 3", "Dominated Red Dragon", 14, "Dragon", "Netherbrain", 23, "Aberration", Defense("Dominated Red Dragon", 19, 8, 6, 13, 3, 7, 11, true))
        ],
        ["Explorer"] =
        [
            new("ACT 1", "Grym", 13, "Construct", "Grym", 21, "Construct", Defense("Grym", 18, 7, 4, 7, -4, 6, -5)),
            new("ACT 2", "Apostle of Myrkul", 12, "Undead", "Apostle of Myrkul", 19, "Undead", Defense("Apostle of Myrkul", 19, 6, 1, 6, 2, 9, 11)),
            new("ACT 3", "Dominated Red Dragon", 16, "Dragon", "Netherbrain", 25, "Aberration", Defense("Dominated Red Dragon", 19, 8, 8, 15, 3, 9, 13, true))
        ],
        ["Tactician"] =
        [
            new("ACT 1", "Grym", 13, "Construct", "Grym", 21, "Construct", Defense("Grym", 18, 7, 2, 7, -4, 4, -5)),
            new("ACT 2", "Apostle of Myrkul", 13, "Undead", "Apostle of Myrkul", 21, "Undead", Defense("Apostle of Myrkul", 20, 7, 2, 6, 2, 7, 11)),
            new("ACT 3", "Dominated Red Dragon", 16, "Dragon", "Netherbrain", 25, "Aberration", Defense("Dominated Red Dragon", 19, 8, 6, 13, 3, 7, 11, true))
        ],
        ["Honour"] =
        [
            new("ACT 1", "Grym", 13, "Construct", "Grym", 21, "Construct", Defense("Grym", 18, 7, 2, 7, -4, 4, -5)),
            new("ACT 2", "Apostle of Myrkul", 13, "Undead", "Apostle of Myrkul", 21, "Undead", Defense("Apostle of Myrkul", 20, 7, 2, 6, 2, 7, 11)),
            new("ACT 3", "Dominated Red Dragon", 16, "Dragon", "Netherbrain", 25, "Aberration", Defense("Dominated Red Dragon", 19, 8, 6, 13, 3, 7, 11, true))
        ]
    };

    // Representative act baselines: expected hostile proficiency plus a typical
    // primary attack/casting modifier for the act's level range. Difficulty
    // applies the same +2 attack/DC adjustment used by the worst-case cards.
    private static readonly Dictionary<string, EnemyThreatProfile[]> AverageThreats = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Balanced"] = CreateAverageThreats(0),
        ["Explorer"] = CreateAverageThreats(2),
        ["Tactician"] = CreateAverageThreats(2),
        ["Honour"] = CreateAverageThreats(2)
    };

    private static EnemyThreatProfile[] CreateAverageThreats(int difficultyBonus) =>
    [
        new("ACT 1", "Average enemy", 5 + difficultyBonus, "Mixed", "Average caster", 13 + difficultyBonus, "Mixed", Defense("Average Act 1 enemy", 14, 2, 2, 2, 0, 1, 0)),
        new("ACT 2", "Average enemy", 7 + difficultyBonus, "Mixed", "Average caster", 15 + difficultyBonus, "Mixed", Defense("Average Act 2 enemy", 16, 4, 3, 4, 1, 3, 1)),
        new("ACT 3", "Average enemy", 10 + difficultyBonus, "Mixed", "Average caster", 18 + difficultyBonus, "Mixed", Defense("Average Act 3 enemy", 18, 6, 5, 6, 3, 5, 3))
    ];

    private static EnemyDefenseProfile Defense(string enemy, int armourClass, int str, int dex, int con, int intelligence, int wis, int cha, bool magicResistance = false) =>
        new(enemy, armourClass, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["STR"] = str, ["DEX"] = dex, ["CON"] = con, ["INT"] = intelligence, ["WIS"] = wis, ["CHA"] = cha
        }, magicResistance);

    public static CharacterStats Calculate(CharacterState state, IEnumerable<ItemRecord> allItems)
    {
        state.NormalizeClassLevels(!state.Difficulty.Equals("Explorer", StringComparison.OrdinalIgnoreCase));
        var totalLevel = state.TotalLevel;
        var equipped = allItems.Where(item => item.Equipped).ToList();
        var parsedEffects = ItemEffectParser.ParseEquipped(equipped);
        var activeEffects = parsedEffects.Where(state.IsEffectActive).ToList();
        activeEffects.AddRange(PermanentBonusEffects(state));
        var startingProfile = Profiles.GetValueOrDefault(state.ClassName, Profiles["Fighter"]);
        var abilities = AbilityNames.ToDictionary(name => name, state.GetAbility, StringComparer.OrdinalIgnoreCase);
        ApplyFeatAbilityChanges(state, abilities);
        ApplyPermanentAbilityChanges(state, abilities);
        ApplyEquipmentAbilityChanges(abilities, equipped);
        // Keep the exact live totals from an imported save. The editable fields
        // remain the real level-1 scores; any manual build/gear edit clears these.
        if (state.ImportedCurrentAbilities)
            foreach (var pair in state.ImportedAbilityTotals.Where(pair => AbilityNames.Contains(pair.Key, StringComparer.OrdinalIgnoreCase) && pair.Value is >= 1 and <= 40))
                abilities[pair.Key] = pair.Value;
        var buildWarnings = ValidateBuildOptions(state, abilities, equipped, startingProfile);
        var nonProficientGear = equipped.Where(item => !IsArmourProficient(state, startingProfile, item)).Select(item => item.Name).ToList();

        var spellClass = state.ClassLevels.Keys
            .Where(Profiles.ContainsKey)
            .OrderByDescending(className => Modifier(abilities[Profiles[className].SpellAbility]))
            .ThenByDescending(state.GetClassLevel)
            .FirstOrDefault() ?? state.ClassName;
        var spellProfile = Profiles.GetValueOrDefault(spellClass, startingProfile);

        var proficiency = (totalLevel <= 4 ? 2 : totalLevel <= 8 ? 3 : 4)
                          + (state.Difficulty.Equals("Explorer", StringComparison.OrdinalIgnoreCase) ? 2 : 0);
        var dexterityModifier = Modifier(abilities["DEX"]);
        var armourClass = CalculateArmourClass(state, abilities, equipped, activeEffects, proficiency, buildWarnings, out var armourClassBreakdown);
        var spellBonus = activeEffects.Where(effect => effect.Kind == ItemEffectKind.SpellSaveDcBonus).Sum(effect => effect.Value);
        var spellAttackBonus = activeEffects.Where(effect => effect.Kind == ItemEffectKind.SpellAttackBonus).Sum(effect => effect.Value);
        var spellAbilityModifier = Modifier(abilities[spellProfile.SpellAbility]);
        var spellSaveDc = 8 + proficiency + spellAbilityModifier + spellBonus;
        var spellAttack = proficiency + Modifier(abilities[spellProfile.SpellAbility]) + spellAttackBonus;
        var spellDcLines = new List<string>
        {
            Localization.T("CalculationBaseEight"),
            Localization.Format("CalculationProficiency", Signed(proficiency)),
            Localization.Format("CalculationAbilityModifier", spellProfile.SpellAbility, abilities[spellProfile.SpellAbility], Signed(spellAbilityModifier))
        };
        spellDcLines.AddRange(activeEffects
            .Where(effect => effect.Kind == ItemEffectKind.SpellSaveDcBonus)
            .Select(effect => $"{Signed(effect.Value)} {effect.ItemName}"));
        var spellSaveDcBreakdown = Localization.Format("SpellDcCalculation", spellSaveDc)
                                   + Environment.NewLine + string.Join(Environment.NewLine, spellDcLines)
                                   + Environment.NewLine + $"= {spellSaveDc}";

        var criticalEffects = activeEffects.Where(effect => effect.Kind == ItemEffectKind.CriticalThresholdReduction).ToList();
        var criticalReduction = criticalEffects.Sum(effect => Math.Max(1, effect.Value));
        var criticalLines = new List<string> { Localization.T("CriticalBase") };
        criticalLines.AddRange(criticalEffects.Select(effect => $"- {Math.Max(1, effect.Value)}  {effect.ItemName}"));
        if (state.HasBuff("Champion: Improved Critical Hit") && state.GetClassLevel("Fighter") >= 3
            && state.GetSubclass("Fighter").Equals("Champion", StringComparison.OrdinalIgnoreCase))
        {
            criticalReduction++;
            criticalLines.Add("- 1  Champion: Improved Critical Hit");
        }
        if (state.HasBuff("Elixir of Viciousness"))
        {
            criticalReduction++;
            criticalLines.Add("- 1  Elixir of Viciousness");
        }
        var criticalThreshold = Math.Clamp(20 - criticalReduction, 2, 20);
        var spellCriticalThreshold = criticalThreshold;
        if (state.HasFeat("Spell Sniper"))
        {
            spellCriticalThreshold = Math.Max(2, spellCriticalThreshold - 1);
            criticalLines.Add(Localization.T("CriticalSpellSniper"));
        }
        criticalLines.Add(Localization.Format("CriticalWeaponResult", criticalThreshold, CriticalChance(criticalThreshold)));
        criticalLines.Add(Localization.Format("CriticalSpellResult", spellCriticalThreshold, CriticalChance(spellCriticalThreshold)));
        criticalLines.Add(Localization.T("CriticalNaturalOne"));
        var criticalBreakdown = Localization.T("CriticalCalculation") + Environment.NewLine + string.Join(Environment.NewLine, criticalLines);

        var attackAbility = DetermineWeaponAbility(startingProfile.AttackAbility, spellProfile.SpellAbility, abilities, equipped);
        var weaponAttack = proficiency + Modifier(abilities[attackAbility]) + ExtractWeaponEnchantment(equipped)
                           + activeEffects.Where(effect => effect.Kind == ItemEffectKind.AttackRollBonus).Sum(effect => effect.Value);
        var mainHand = PrimaryWeapon(equipped);
        var rangedWeapon = IsRangedWeapon(mainHand);
        var twoHandedWeapon = IsTwoHandedWeapon(mainHand);
        var styles = state.FightingStyles.Values.Where(style => !string.IsNullOrWhiteSpace(style)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (styles.Contains("Archery", StringComparer.OrdinalIgnoreCase) && rangedWeapon)
            weaponAttack += 2;
        if (state.HasBuff("Magic Weapon +1")) weaponAttack += 1;
        if (state.HasBuff("Magic Weapon +2")) weaponAttack += 2;
        if (state.HasBuff("Magic Weapon +3")) weaponAttack += 3;
        if (state.HasBuff("Great Weapon Master: All In") && state.HasFeat("Great Weapon Master") && twoHandedWeapon)
            weaponAttack -= 5;
        if (state.HasBuff("Sharpshooter: All In") && state.HasFeat("Sharpshooter") && rangedWeapon)
            weaponAttack -= 5;
        if (mainHand?.Description.Contains("Double your Proficiency Bonus", StringComparison.OrdinalIgnoreCase) == true && nonProficientGear.Count == 0)
            weaponAttack += proficiency;
        var constitutionModifier = Modifier(abilities["CON"]);
        var hitPoints = startingProfile.StartingHp + constitutionModifier;
        foreach (var className in state.ClassLevels.Keys.Where(Profiles.ContainsKey))
        {
            var addedLevels = state.GetClassLevel(className) - (className.Equals(state.ClassName, StringComparison.OrdinalIgnoreCase) ? 1 : 0);
            hitPoints += Math.Max(0, addedLevels) * (Profiles[className].HpPerLevel + constitutionModifier);
        }
        hitPoints = Math.Max(totalLevel, hitPoints);
        if (state.HasFeat("Tough")) hitPoints += totalLevel * 2;
        if (state.HasBuff("Heroes' Feast")) hitPoints += 12;
        hitPoints += ActiveAidBonus(state);
        if (state.Difficulty.Equals("Explorer", StringComparison.OrdinalIgnoreCase))
            hitPoints *= 2;
        var initiative = dexterityModifier + activeEffects.Where(effect => effect.Kind == ItemEffectKind.InitiativeBonus).Sum(effect => effect.Value)
                         + (state.HasFeat("Alert") ? 5 : 0);
        if (state.GetClassLevel("Bard") >= 2)
            initiative += proficiency / 2;

        var generalSaveBonus = activeEffects.Where(effect => effect.Kind == ItemEffectKind.SavingThrowBonus && effect.Scope == "ALL").Sum(effect => effect.Value)
                               + (state.HasBuff("Warding Bond") ? 1 : 0);
        if (state.GetClassLevel("Paladin") >= 6 && state.HasBuff("Paladin Aura active"))
            generalSaveBonus += Math.Max(0, Modifier(abilities["CHA"]));
        var saves = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var ability in AbilityNames)
        {
            var value = Modifier(abilities[ability]) + generalSaveBonus;
            if (ability == startingProfile.SaveOne || ability == startingProfile.SaveTwo)
                value += proficiency;
            if (state.Feats.Any(feat => feat.Name.Equals("Resilient", StringComparison.OrdinalIgnoreCase) && feat.Choice.Equals(ability, StringComparison.OrdinalIgnoreCase))
                && ability != startingProfile.SaveOne && ability != startingProfile.SaveTwo)
                value += proficiency;
            if (ability == "DEX" && HasShieldEquipped(equipped) && state.HasFeat("Shield Master"))
                value += 2;
            value += activeEffects.Where(effect => effect.Kind == ItemEffectKind.SavingThrowBonus && effect.Scope == ability).Sum(effect => effect.Value);
            saves[ability] = value;
        }

        var enemyAttackDisadvantage = activeEffects.Any(effect => effect.Kind == ItemEffectKind.EnemyAttackDisadvantage)
                                      || state.HasBuff("Blur") || state.HasBuff("Greater Invisibility") || state.HasBuff("Invisibility")
                                      || (state.HasBuff("Patient Defence") && state.GetClassLevel("Monk") >= 2);
        var enemyAttackAdvantage = state.HasBuff("Reckless Attack") && state.GetClassLevel("Barbarian") >= 2;
        var enemySpellAttackDisadvantage = enemyAttackDisadvantage || activeEffects.Any(effect => effect.Kind == ItemEffectKind.EnemySpellAttackDisadvantage);
        var generalSaveAdvantage = activeEffects.Any(effect => effect.Kind == ItemEffectKind.SavingThrowAdvantage && effect.Scope == "ALL");
        var spellSaveAdvantage = generalSaveAdvantage || activeEffects.Any(effect => effect.Kind == ItemEffectKind.SpellSavingThrowAdvantage);
        var generalSaveDisadvantage = nonProficientGear.Count > 0 || activeEffects.Any(effect => effect.Kind == ItemEffectKind.SavingThrowDisadvantage);
        var criticalHitImmune = activeEffects.Any(effect => effect.Kind == ItemEffectKind.CriticalHitImmunity);
        var threatProfiles = WorstCaseThreats.GetValueOrDefault(state.Difficulty, WorstCaseThreats["Balanced"]);
        var averageProfiles = AverageThreats.GetValueOrDefault(state.Difficulty, AverageThreats["Balanced"]);
        var sweetStone = state.HasPermanentBonus("Sweet Stone Features");
        var attackBonusD4Count = (state.HasBuff("Bless") ? 1 : 0) + (sweetStone ? 1 : 0);
        var savingThrowBonusD4Count = (state.HasBuff("Bless") || state.HasBuff("Resistance") ? 1 : 0) + (sweetStone ? 1 : 0);
        var playerAttackAdvantage = activeEffects.Any(effect => effect.Kind == ItemEffectKind.AttackRollAdvantage)
                                    || state.HasBuff("Lucky: attack advantage") || state.HasBuff("Greater Invisibility")
                                    || (state.HasBuff("Reckless Attack") && state.GetClassLevel("Barbarian") >= 2);
        var playerAttackDisadvantage = nonProficientGear.Count > 0
                                       || (state.HasPermanentBonus("Paid the Price") && state.HasBuff("Paid the Price: attacking a Hag"));
        var enemySavingThrowDisadvantage = activeEffects.Any(effect => effect.Kind == ItemEffectKind.EnemySavingThrowDisadvantage);
        var sanctuary = state.HasBuff("Sanctuary");
        var threats = threatProfiles
            .SelectMany((profile, index) => new[] { (Benchmark: "WorstCase", Profile: profile), (Benchmark: "Average", Profile: averageProfiles[index]) })
            .Select(entry =>
            {
                var profile = entry.Profile;
                var spellAttackBonus = profile.SpellDc - 8;
                var protectedAttack = state.HasBuff("Protection from Evil and Good") && IsProtectedCreatureType(profile.AttackCreatureType);
                var protectedSpellAttack = state.HasBuff("Protection from Evil and Good") && IsProtectedCreatureType(profile.SpellCreatureType);
                var spellEffects = CalculateSpellEffectChances(state, profile.SpellDc, saves, activeEffects, spellSaveAdvantage, generalSaveDisadvantage, savingThrowBonusD4Count);
                var worstSpellEffect = spellEffects.OrderByDescending(value => value.Value).First();
                var characterSpellEffects = AbilityNames.ToDictionary(
                    ability => ability,
                    ability => SavingThrowFailureChance(
                        profile.Defense.Saves[ability],
                        spellSaveDc,
                        0,
                        profile.Defense.MagicResistance,
                        enemySavingThrowDisadvantage),
                    StringComparer.OrdinalIgnoreCase);
                return new ActThreat(
                    profile.Act,
                    entry.Benchmark,
                    profile.AttackEnemy,
                    profile.AttackBonus,
                    profile.SpellEnemy,
                    spellAttackBonus,
                    profile.SpellDc,
                    sanctuary ? 0 : ApplyRollMode(AttackHitChance(armourClass, profile.AttackBonus, criticalHitImmune), enemyAttackAdvantage, enemyAttackDisadvantage || protectedAttack),
                    sanctuary ? 0 : ApplyRollMode(AttackHitChance(armourClass, spellAttackBonus, criticalHitImmune), enemyAttackAdvantage, enemySpellAttackDisadvantage || protectedSpellAttack),
                    worstSpellEffect.Value,
                    worstSpellEffect.Key,
                    spellEffects,
                    profile.Defense.Enemy,
                    profile.Defense.ArmourClass,
                    AttackHitChanceWithDice(profile.Defense.ArmourClass, weaponAttack, attackBonusD4Count, playerAttackAdvantage, playerAttackDisadvantage, criticalThreshold),
                    AttackHitChanceWithDice(profile.Defense.ArmourClass, spellAttack, attackBonusD4Count, playerAttackAdvantage, playerAttackDisadvantage, spellCriticalThreshold),
                    spellSaveDc,
                    characterSpellEffects);
            })
            .ToList();

        return new CharacterStats
        {
            Abilities = abilities,
            Saves = saves,
            Proficiency = proficiency,
            ArmourClass = armourClass,
            SpellSaveDc = spellSaveDc,
            SpellAttack = spellAttack,
            WeaponAttack = weaponAttack,
            HitPoints = hitPoints,
            Initiative = initiative,
            Movement = CalculateMovement(state, equipped),
            SpellAbility = spellProfile.SpellAbility,
            SpellClass = spellClass,
            AttackAbility = attackAbility,
            Threats = threats,
            ActiveEffects = activeEffects,
            AttackRollAdvantage = playerAttackAdvantage,
            AttackRollDisadvantage = playerAttackDisadvantage,
            EnemySavingThrowDisadvantage = enemySavingThrowDisadvantage,
            CriticalHitImmune = criticalHitImmune,
            DamageReduction = activeEffects.Where(effect => effect.Kind == ItemEffectKind.DamageReduction).Sum(effect => effect.Value)
                              + (state.HasFeat("Heavy Armour Master") && IsWearingArmourCategory(equipped, "Heavy") ? 3 : 0),
            Resistances = activeEffects.Where(effect => effect.Kind == ItemEffectKind.Resistance).Select(effect => effect.Scope)
                .Concat(state.HasBuff("Warding Bond") ? ["All"] : Array.Empty<string>())
                .Concat((state.HasBuff("Blade Ward") || state.HasBuff("Stoneskin")) ? ["Bludgeoning", "Piercing", "Slashing"] : Array.Empty<string>())
                .Concat(ProtectionFromEnergyResistances(state))
                .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value).ToList(),
            NonProficientGear = nonProficientGear,
            BuildWarnings = buildWarnings,
            AttackBonusDie = attackBonusD4Count > 0 ? 4 : 0,
            SavingThrowBonusDie = savingThrowBonusD4Count > 0 ? 4 : 0,
            AttackBonusD4Count = attackBonusD4Count,
            SavingThrowBonusD4Count = savingThrowBonusD4Count,
            TemporaryHitPoints = state.HasPermanentBonus("The Tharchiate Codex: Blessing") ? 20 : 0,
            CriticalThreshold = criticalThreshold,
            SpellCriticalThreshold = spellCriticalThreshold,
            ArmourClassBreakdown = armourClassBreakdown,
            SpellSaveDcBreakdown = spellSaveDcBreakdown,
            CriticalBreakdown = criticalBreakdown
        };
    }

    public static int Modifier(int score) => (int)Math.Floor((score - 10) / 2.0);

    private static int CalculateArmourClass(
        CharacterState state,
        Dictionary<string, int> abilities,
        List<ItemRecord> equipped,
        List<ItemEffect> activeEffects,
        int proficiency,
        List<string> warnings,
        out string breakdown)
    {
        var dex = Modifier(abilities["DEX"]);
        var body = equipped.FirstOrDefault(item => GearRules.SlotFor(item) == "Body");
        var wearingArmour = IsWearingAnyArmour(equipped);
        var shield = HasShieldEquipped(equipped);
        int baseAc;
        var lines = new List<string>();
        if (body is null || body.Type.Equals("Clothing", StringComparison.OrdinalIgnoreCase))
        {
            var candidates = new List<(int Value, string Formula)>
            {
                (10 + dex, Localization.Format("AcUnarmoured", Signed(dex)))
            };
            if (state.HasBuff("Mage Armour") && !wearingArmour)
                candidates.Add((13 + dex, Localization.Format("AcMageArmour", Signed(dex))));
            if (state.GetSubclass("Sorcerer").Equals("Draconic Bloodline", StringComparison.OrdinalIgnoreCase) && !wearingArmour)
                candidates.Add((13 + dex, Localization.Format("AcDraconicResilience", Signed(dex))));
            if (state.HasClass("Barbarian") && !wearingArmour)
                candidates.Add((10 + dex + Modifier(abilities["CON"]), Localization.Format("AcBarbarian", Signed(dex), Signed(Modifier(abilities["CON"])))));
            if (state.HasClass("Monk") && !wearingArmour && !shield)
                candidates.Add((10 + dex + Modifier(abilities["WIS"]), Localization.Format("AcMonk", Signed(dex), Signed(Modifier(abilities["WIS"])))));
            var selected = candidates.OrderByDescending(candidate => candidate.Value).First();
            baseAc = selected.Value;
            lines.Add(selected.Formula);
        }
        else
        {
            var listedAc = ExtractListedAc(body.Properties);
            if (body.Type.Contains("Heavy", StringComparison.OrdinalIgnoreCase))
            {
                baseAc = listedAc;
                lines.Add(Localization.Format("AcArmourBase", body.Name, listedAc));
            }
            else if (body.Type.Contains("Medium", StringComparison.OrdinalIgnoreCase))
            {
                var cap = state.HasFeat("Medium Armour Master") ? 3 : 2;
                var appliedDex = body.Description.Contains("Dexterity Modifier", StringComparison.OrdinalIgnoreCase) ? dex : Math.Min(cap, dex);
                baseAc = listedAc + appliedDex;
                lines.Add(Localization.Format("AcArmourDexBase", body.Name, listedAc, Signed(appliedDex)));
            }
            else
            {
                baseAc = listedAc + dex;
                lines.Add(Localization.Format("AcArmourDexBase", body.Name, listedAc, Signed(dex)));
            }
        }

        var ac = baseAc;
        void AddBonus(string source, int value)
        {
            if (value == 0) return;
            ac += value;
            lines.Add($"{Signed(value)} {source}");
        }
        foreach (var item in equipped)
            AddBonus(item.Name, ExtractAcBonus(item.Properties));
        foreach (var effect in activeEffects.Where(effect => effect.Kind == ItemEffectKind.ArmourClassBonus))
            AddBonus(effect.ItemName, effect.Value);
        if (state.FightingStyles.Values.Contains("Defence", StringComparer.OrdinalIgnoreCase) && wearingArmour) AddBonus("Defence Fighting Style", 1);
        if (state.HasBuff("Shield")) AddBonus("Shield", 5);
        if (state.HasBuff("Shield of Faith")) AddBonus("Shield of Faith", 2);
        if (state.HasBuff("Haste")) AddBonus("Haste", 2);
        if (state.HasBuff("Warding Bond")) AddBonus("Warding Bond", 1);
        if (state.HasBuff("Mirror Image (3 images)")) AddBonus("Mirror Image (3 images)", 9);
        else if (state.HasBuff("Mirror Image (2 images)")) AddBonus("Mirror Image (2 images)", 6);
        else if (state.HasBuff("Mirror Image (1 image)")) AddBonus("Mirror Image (1 image)", 3);
        if (state.HasBuff("Defensive Duellist reaction") && state.HasFeat("Defensive Duellist")
            && abilities["DEX"] >= 13 && IsFinesseWeapon(equipped.FirstOrDefault(item => GearRules.SlotFor(item) == "Main Hand")))
            AddBonus("Defensive Duellist", proficiency);
        if (state.HasFeat("Dual Wielder") && HasTwoMeleeWeapons(equipped)) AddBonus("Dual Wielder", 1);
        if (state.HasBuff("Barkskin") && ac < 16)
        {
            lines.Add(Localization.Format("AcBarkskinMinimum", ac));
            ac = 16;
        }
        if (state.HasBuff("Mage Armour") && wearingArmour)
            warnings.Add("Mage Armour inactive: Armour is equipped.");
        breakdown = Localization.Format("AcCalculation", ac)
                    + Environment.NewLine + string.Join(Environment.NewLine, lines)
                    + Environment.NewLine + $"= {ac}";
        return ac;
    }

    private static int CriticalChance(int threshold) => (21 - threshold) * 5;

    private static string Signed(int value) => value >= 0 ? $"+{value}" : value.ToString();

    private static void ApplyFeatAbilityChanges(CharacterState state, Dictionary<string, int> abilities)
    {
        foreach (var feat in state.Feats.Take(BuildOptions.FeatSlotCount(state)))
        {
            if (string.IsNullOrWhiteSpace(feat.Name))
                continue;
            if (feat.Name.Equals("Ability Improvement", StringComparison.OrdinalIgnoreCase))
            {
                foreach (Match match in Regex.Matches(feat.Choice, @"\b(STR|DEX|CON|INT|WIS|CHA)\s*\+(1|2)\b", RegexOptions.IgnoreCase))
                {
                    var ability = match.Groups[1].Value.ToUpperInvariant();
                    abilities[ability] = Math.Min(20, abilities[ability] + int.Parse(match.Groups[2].Value));
                }
            }
            else if (new[] { "Actor", "Athlete", "Durable", "Heavily Armoured", "Heavy Armour Master", "Lightly Armoured", "Moderately Armoured", "Performer", "Resilient", "Tavern Brawler", "Weapon Master" }
                     .Contains(feat.Name, StringComparer.OrdinalIgnoreCase)
                     && AbilityNames.Contains(feat.Choice, StringComparer.OrdinalIgnoreCase))
            {
                abilities[feat.Choice] = Math.Min(20, abilities[feat.Choice] + 1);
            }
        }
    }

    private static void ApplyPermanentAbilityChanges(CharacterState state, Dictionary<string, int> abilities)
    {
        var hairAbility = state.PermanentBonusChoice("Auntie Ethel's Hair");
        if (state.HasPermanentBonus("Auntie Ethel's Hair") && abilities.ContainsKey(hairAbility))
            abilities[hairAbility]++;

        if (state.HasPermanentBonus("Potion of Everlasting Vigour"))
            abilities["STR"] += 2;
        if (state.HasPermanentBonus("Zaith'isk Penalty: Intelligence"))
            abilities["INT"] = Math.Max(1, abilities["INT"] - 2);
        if (state.HasPermanentBonus("Zaith'isk Penalty: Wisdom"))
            abilities["WIS"] = Math.Max(1, abilities["WIS"] - 2);
        if (state.HasPermanentBonus("Zaith'isk Penalty: Constitution"))
            abilities["CON"] = Math.Max(1, abilities["CON"] - 2);
        if (state.HasPermanentBonus("Tharchiate Withering"))
            abilities["CON"] = Math.Max(1, abilities["CON"] - 5);

        var mirrorAbility = state.PermanentBonusChoice("Mirror of Loss");
        if (state.HasPermanentBonus("Mirror of Loss") && abilities.ContainsKey(mirrorAbility))
            abilities[mirrorAbility] = Math.Min(24, abilities[mirrorAbility] + 2);
        if (state.HasPermanentBonus("Patriar's Memory"))
            abilities["CHA"] = Math.Min(24, abilities["CHA"] + 1);
    }

    private static List<ItemEffect> PermanentBonusEffects(CharacterState state)
    {
        var effects = new List<ItemEffect>();
        void Add(string source, ItemEffectKind kind, string scope, string summary, int value = 0) =>
            effects.Add(new ItemEffect($"permanent|{source}|{kind}|{scope}", source, kind, scope, summary, false, true, value));

        if (state.HasPermanentBonus("Forbidden Knowledge"))
            Add("Forbidden Knowledge", ItemEffectKind.SavingThrowBonus, "WIS", "+1 Wisdom saving throws", 1);
        if (state.HasPermanentBonus("Anointed in Splendour"))
            Add("Anointed in Splendour", ItemEffectKind.SavingThrowBonus, "ALL", "+2 all saving throws", 2);
        if (state.HasPermanentBonus("Githzerai Mind Barrier"))
            Add("Githzerai Mind Barrier", ItemEffectKind.SavingThrowAdvantage, "INT", "Advantage on Intelligence saving throws");
        if (state.HasPermanentBonus("Loviatar's Love") && state.HasBuff("Loviatar's Love active (30% HP or less)"))
        {
            Add("Loviatar's Love", ItemEffectKind.AttackRollBonus, "Attacks", "+2 attack rolls", 2);
            Add("Loviatar's Love", ItemEffectKind.SavingThrowBonus, "WIS", "+2 Wisdom saving throws", 2);
        }
        if (state.HasPermanentBonus("BOOOAL's Benediction") && state.HasBuff("BOOOAL target is Bleeding"))
            Add("BOOOAL's Benediction", ItemEffectKind.AttackRollAdvantage, "ALL", "Advantage against Bleeding targets");

        return effects;
    }

    private static void ApplyEquipmentAbilityChanges(Dictionary<string, int> abilities, List<ItemRecord> equipped)
    {
        foreach (var item in equipped)
        {
            foreach (var ability in AbilityNames)
            {
                var text = item.Description;
                var bonusMatch = Regex.Match(text, $@"\b{FullAbilityName(ability)}\s*\+\s*(\d+)", RegexOptions.IgnoreCase);
                if (bonusMatch.Success)
                {
                    var bonus = int.Parse(bonusMatch.Groups[1].Value);
                    var cap = text.Contains("22", StringComparison.OrdinalIgnoreCase) ? 22 : 20;
                    abilities[ability] = Math.Min(cap, abilities[ability] + bonus);
                }
                var abilityName = FullAbilityName(ability);
                var setMatches = new[]
                {
                    Regex.Match(text, $@"\b{abilityName}[^\r\n]{{0,35}}(?:set|increase(?:s|d)?)[^\r\n]{{0,15}}\b(?:to\s*)?(\d{{2}})", RegexOptions.IgnoreCase),
                    Regex.Match(text, $@"\bIncrease(?:s|d)?(?:\s+the wearer's)?\s+{abilityName}(?:\s+score)?\s+to\s+(\d{{2}})", RegexOptions.IgnoreCase)
                };
                var setValue = setMatches.Where(match => match.Success).Select(match => int.Parse(match.Groups[1].Value)).DefaultIfEmpty(0).Max();
                if (setValue > 0)
                    abilities[ability] = Math.Max(abilities[ability], setValue);
            }
        }
    }

    private static string DetermineWeaponAbility(string defaultAbility, string spellAbility, Dictionary<string, int> abilities, List<ItemRecord> equipped)
    {
        var weapon = PrimaryWeapon(equipped);
        if (weapon is null)
            return defaultAbility;
        if (weapon.Description.Contains("Spellcasting Ability Modifier to Attack Rolls", StringComparison.OrdinalIgnoreCase))
            return spellAbility;
        if (weapon.Type.Contains("Bow", StringComparison.OrdinalIgnoreCase) || weapon.Type.Contains("Crossbow", StringComparison.OrdinalIgnoreCase))
            return "DEX";
        if (new[] { "Dagger", "Shortsword", "Scimitar", "Rapier" }.Any(type => weapon.Type.Equals(type, StringComparison.OrdinalIgnoreCase)))
            return abilities["DEX"] >= abilities["STR"] ? "DEX" : "STR";
        return "STR";
    }

    private static List<string> ValidateBuildOptions(CharacterState state, Dictionary<string, int> abilities, List<ItemRecord> equipped, ClassProfile startingProfile)
    {
        var warnings = new List<string>();
        if (state.Feats.Count > BuildOptions.FeatSlotCount(state))
            warnings.Add("Too many feats for the selected class levels.");
        foreach (var duplicate in state.Feats.Where(feat => !feat.Name.Equals("Ability Improvement", StringComparison.OrdinalIgnoreCase))
                     .GroupBy(feat => feat.Name, StringComparer.OrdinalIgnoreCase).Where(group => !string.IsNullOrWhiteSpace(group.Key) && group.Count() > 1))
            warnings.Add($"{duplicate.Key} can only be selected once.");
        foreach (var duplicate in state.FightingStyles.Values.Where(style => !string.IsNullOrWhiteSpace(style))
                     .GroupBy(style => style, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1))
            warnings.Add($"Fighting Style: {duplicate.Key} can only be learned once.");

        if (state.HasFeat("Defensive Duellist") && abilities["DEX"] < 13)
            warnings.Add("Defensive Duellist requires Dexterity 13.");
        var trainingAtSelection = GetArmourTraining(state, startingProfile, includeFeats: false);
        foreach (var feat in state.Feats)
        {
            if (feat.Name.Equals("Lightly Armoured", StringComparison.OrdinalIgnoreCase))
                trainingAtSelection.Add("Light");
            else if (feat.Name.Equals("Moderately Armoured", StringComparison.OrdinalIgnoreCase))
            {
                if (!trainingAtSelection.Contains("Light")) warnings.Add("Moderately Armoured requires Light Armour proficiency when selected.");
                trainingAtSelection.UnionWith(["Medium", "Shield"]);
            }
            else if (feat.Name.Equals("Heavily Armoured", StringComparison.OrdinalIgnoreCase))
            {
                if (!trainingAtSelection.Contains("Medium")) warnings.Add("Heavily Armoured requires Medium Armour proficiency when selected.");
                trainingAtSelection.Add("Heavy");
            }
            else if (feat.Name.Equals("Medium Armour Master", StringComparison.OrdinalIgnoreCase) && !trainingAtSelection.Contains("Medium"))
                warnings.Add("Medium Armour Master requires Medium Armour proficiency when selected.");
            else if (feat.Name.Equals("Heavy Armour Master", StringComparison.OrdinalIgnoreCase) && !trainingAtSelection.Contains("Heavy"))
                warnings.Add("Heavy Armour Master requires Heavy Armour proficiency when selected.");
        }

        var activeConcentration = state.ActiveBuffs.Select(BuildOptions.FindBuff).Where(buff => buff?.Concentration == true).ToList();
        if (activeConcentration.Count > 1)
            warnings.Add("Only one Concentration spell can be active at a time.");
        if (state.HasBuff("Defensive Duellist reaction") && (!state.HasFeat("Defensive Duellist") || abilities["DEX"] < 13 || !IsFinesseWeapon(equipped.FirstOrDefault(item => GearRules.SlotFor(item) == "Main Hand"))))
            warnings.Add("Defensive Duellist reaction inactive: feat, Dexterity 13 and a finesse weapon are required.");
        if (state.HasBuff("Great Weapon Master: All In") && (!state.HasFeat("Great Weapon Master") || !IsTwoHandedWeapon(equipped.FirstOrDefault(item => GearRules.SlotFor(item) == "Main Hand"))))
            warnings.Add("Great Weapon Master: All In inactive: feat and qualifying two-handed melee weapon required.");
        if (state.HasBuff("Sharpshooter: All In") && (!state.HasFeat("Sharpshooter") || !equipped.Any(item => GearRules.SlotFor(item) == "Ranged")))
            warnings.Add("Sharpshooter: All In inactive: feat and ranged weapon required.");
        if (state.HasBuff("Rage") && (state.GetClassLevel("Barbarian") < 1 || IsWearingArmourCategory(equipped, "Heavy")))
            warnings.Add("Rage inactive: Barbarian level and no Heavy Armour required.");
        if (state.HasBuff("Reckless Attack") && state.GetClassLevel("Barbarian") < 2)
            warnings.Add("Reckless Attack inactive: Barbarian level 2 required.");
        if (state.HasBuff("Patient Defence") && state.GetClassLevel("Monk") < 2)
            warnings.Add("Patient Defence inactive: Monk level 2 and Ki required.");
        if (state.HasBuff("Danger Sense") && state.GetClassLevel("Barbarian") < 2)
            warnings.Add("Danger Sense inactive: Barbarian level 2 required.");
        if (state.HasBuff("Indomitable") && state.GetClassLevel("Fighter") < 9)
            warnings.Add("Indomitable inactive: Fighter level 9 required.");
        if (state.HasBuff("Champion: Improved Critical Hit")
            && (state.GetClassLevel("Fighter") < 3 || !state.GetSubclass("Fighter").Equals("Champion", StringComparison.OrdinalIgnoreCase)))
            warnings.Add("Champion: Improved Critical Hit inactive: Fighter level 3 and Champion subclass required.");
        if (state.HasBuff("Loviatar's Love active (30% HP or less)") && !state.HasPermanentBonus("Loviatar's Love"))
            warnings.Add("Loviatar's Love condition inactive: permanent bonus not selected.");
        if (state.HasBuff("BOOOAL target is Bleeding") && !state.HasPermanentBonus("BOOOAL's Benediction"))
            warnings.Add("BOOOAL condition inactive: permanent bonus not selected.");
        if (state.HasBuff("Paid the Price: attacking a Hag") && !state.HasPermanentBonus("Paid the Price"))
            warnings.Add("Paid the Price condition inactive: permanent bonus not selected.");
        if (state.HasPermanentBonus("Paid the Price") && state.HasPermanentBonus("Volo's Ersatz Eye"))
            warnings.Add("Paid the Price and Volo's Ersatz Eye are mutually exclusive.");
        if (state.HasBuff("Paladin Aura active") && state.GetClassLevel("Paladin") < 6)
            warnings.Add("Aura of Protection inactive: Paladin level 6 required.");
        return warnings;
    }

    private static decimal CalculateMovement(CharacterState state, List<ItemRecord> equipped)
    {
        var movement = RaceMovement.GetValueOrDefault(state.Race, 9m);
        if (state.HasFeat("Mobile")) movement += 3m;
        if (state.HasBuff("Longstrider")) movement += 3m;
        if (state.HasBuff("Haste")) movement += 9m;
        if (state.GetClassLevel("Barbarian") >= 5 && !IsWearingArmourCategory(equipped, "Heavy")) movement += 3m;
        if (state.GetClassLevel("Monk") >= 2 && !IsWearingAnyArmour(equipped) && !HasShieldEquipped(equipped))
            movement += state.GetClassLevel("Monk") >= 10 ? 6m : state.GetClassLevel("Monk") >= 6 ? 4.5m : 3m;
        return movement;
    }

    private static int ActiveAidBonus(CharacterState state)
    {
        for (var level = 6; level >= 2; level--)
            if (state.HasBuff($"Aid (level {level})"))
                return (level - 1) * 5;
        return 0;
    }

    private static bool IsProtectedCreatureType(string creatureType) =>
        new[] { "Aberration", "Celestial", "Elemental", "Fey", "Fiend", "Undead" }.Contains(creatureType, StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<string> ProtectionFromEnergyResistances(CharacterState state) =>
        state.ActiveBuffs.Where(value => value.StartsWith("Protection from Energy: ", StringComparison.OrdinalIgnoreCase))
            .Select(value => value["Protection from Energy: ".Length..]);

    private static bool IsRangedWeapon(ItemRecord? item) => item is not null
        && (item.Type.Contains("Bow", StringComparison.OrdinalIgnoreCase) || item.Type.Contains("Crossbow", StringComparison.OrdinalIgnoreCase));

    private static bool IsTwoHandedWeapon(ItemRecord? item) => item is not null && !IsRangedWeapon(item)
        && (item.Properties.Contains("Two-Handed", StringComparison.OrdinalIgnoreCase)
            || item.Properties.Contains("Versatile", StringComparison.OrdinalIgnoreCase)
            || new[] { "Greatsword", "Greataxe", "Maul", "Halberd", "Glaive", "Pike" }.Contains(item.Type, StringComparer.OrdinalIgnoreCase));

    private static bool IsFinesseWeapon(ItemRecord? item) => item is not null
        && (item.Properties.Contains("Finesse", StringComparison.OrdinalIgnoreCase)
            || new[] { "Dagger", "Shortsword", "Scimitar", "Rapier" }.Contains(item.Type, StringComparer.OrdinalIgnoreCase));

    private static bool HasShieldEquipped(List<ItemRecord> equipped) =>
        equipped.Any(item => item.Type.Equals("Shield", StringComparison.OrdinalIgnoreCase));

    private static bool HasTwoMeleeWeapons(List<ItemRecord> equipped)
    {
        var weapons = equipped.Where(item => !IsRangedWeapon(item) && GearRules.SlotFor(item) is "Main Hand" or "Off Hand" && !item.Type.Equals("Shield", StringComparison.OrdinalIgnoreCase)).ToList();
        return weapons.Count >= 2;
    }

    private static bool IsWearingAnyArmour(List<ItemRecord> equipped) =>
        equipped.Any(item => ArmourCategory(item) is "Light" or "Medium" or "Heavy");

    private static bool IsWearingArmourCategory(List<ItemRecord> equipped, string category) =>
        equipped.Any(item => ArmourCategory(item).Equals(category, StringComparison.OrdinalIgnoreCase));

    private static string ArmourCategory(ItemRecord item)
    {
        var text = item.Type + " " + item.Properties;
        if (text.Contains("Heavy Armour", StringComparison.OrdinalIgnoreCase)) return "Heavy";
        if (text.Contains("Medium Armour", StringComparison.OrdinalIgnoreCase)) return "Medium";
        if (text.Contains("Light Armour", StringComparison.OrdinalIgnoreCase)) return "Light";
        return "";
    }

    private static bool IsArmourProficient(CharacterState state, ClassProfile startingProfile, ItemRecord item)
    {
        string? required = null;
        if (item.Type.Equals("Shield", StringComparison.OrdinalIgnoreCase)) required = "Shield";
        else if (item.Type.Contains("Heavy", StringComparison.OrdinalIgnoreCase)) required = "Heavy";
        else if (item.Type.Contains("Medium", StringComparison.OrdinalIgnoreCase)) required = "Medium";
        else if (item.Type.Contains("Light", StringComparison.OrdinalIgnoreCase)) required = "Light";
        if (required is null)
            return true;
        var training = GetArmourTraining(state, startingProfile, includeFeats: true);
        if (training.Contains(required))
            return true;
        if (state.Race.Equals("Human", StringComparison.OrdinalIgnoreCase) && required is "Light" or "Shield")
            return true;
        if (state.Race.Equals("Githyanki", StringComparison.OrdinalIgnoreCase) && required is "Light" or "Medium")
            return true;
        return false;
    }

    private static HashSet<string> GetArmourTraining(CharacterState state, ClassProfile startingProfile, bool includeFeats)
    {
        var training = new HashSet<string>(startingProfile.ArmourTraining, StringComparer.OrdinalIgnoreCase);
        foreach (var className in state.ClassLevels.Keys.Where(className => !className.Equals(state.ClassName, StringComparison.OrdinalIgnoreCase) && Profiles.ContainsKey(className)))
            training.UnionWith(Profiles[className].MulticlassArmourTraining);
        if (state.Race.Equals("Human", StringComparison.OrdinalIgnoreCase)) training.UnionWith(["Light", "Shield"]);
        if (state.Race.Equals("Githyanki", StringComparison.OrdinalIgnoreCase)) training.UnionWith(["Light", "Medium"]);
        if (!includeFeats) return training;
        if (state.HasFeat("Lightly Armoured")) training.Add("Light");
        if (state.HasFeat("Moderately Armoured")) training.UnionWith(["Medium", "Shield"]);
        if (state.HasFeat("Heavily Armoured")) training.Add("Heavy");
        return training;
    }

    private static int ExtractListedAc(string text)
    {
        var match = ListedAcRegex().Match(text);
        return match.Success ? int.Parse(match.Groups[1].Value) : 10;
    }

    private static int ExtractAcBonus(string text) => AcBonusRegex().Matches(text).Select(match => int.Parse(match.Groups[1].Value)).Sum();

    private static int ExtractBonus(string text, string label)
    {
        var before = Regex.Match(text, $@"\+\s*(\d+)\s*(?:bonus\s+to\s+)?{Regex.Escape(label)}", RegexOptions.IgnoreCase);
        var after = Regex.Match(text, $@"{Regex.Escape(label)}\s*\+\s*(\d+)", RegexOptions.IgnoreCase);
        return new[] { before, after }.Where(match => match.Success).Select(match => int.Parse(match.Groups[1].Value)).DefaultIfEmpty(0).Max();
    }

    private static int ExtractGeneralSavingThrowBonus(string text)
    {
        var matches = new[]
        {
            Regex.Match(text, @"Saving Throws?\s*\+\s*(\d+)", RegexOptions.IgnoreCase),
            Regex.Match(text, @"\+\s*(\d+)\s*bonus to Saving Throws", RegexOptions.IgnoreCase)
        };
        return matches.Where(match => match.Success).Select(match => int.Parse(match.Groups[1].Value)).DefaultIfEmpty(0).Max();
    }

    private static int ExtractSpecificSavingThrowBonus(string text, string ability)
    {
        var name = FullAbilityName(ability);
        var matches = new[]
        {
            Regex.Match(text, $@"{name} Saving Throws?\s*\+\s*(\d+)", RegexOptions.IgnoreCase),
            Regex.Match(text, $@"\+\s*(\d+)\s*bonus to {name} Saving Throws", RegexOptions.IgnoreCase)
        };
        return matches.Where(match => match.Success).Select(match => int.Parse(match.Groups[1].Value)).DefaultIfEmpty(0).Max();
    }

    private static int ExtractWeaponEnchantment(List<ItemRecord> equipped)
    {
        var weapon = PrimaryWeapon(equipped);
        if (weapon is null)
            return 0;
        return DamageBonusRegex().Matches(weapon.Properties)
            .Select(match => int.Parse(match.Groups[1].Value))
            .Where(value => value is >= 1 and <= 3)
            .DefaultIfEmpty(0)
            .Max();
    }

    private static ItemRecord? PrimaryWeapon(List<ItemRecord> equipped) =>
        equipped.FirstOrDefault(item => GearRules.SlotFor(item) == "Main Hand")
        ?? equipped.FirstOrDefault(item => GearRules.SlotFor(item) == "Ranged");

    internal static double AttackHitChance(int armourClass, int attackBonus, bool criticalHitImmune = false)
    {
        var requiredRoll = armourClass - attackBonus;
        // Natural 1 always misses. Natural 20 always hits unless critical-hit immunity
        // turns it into a regular roll that still has to meet the target's AC.
        var minimumSuccessfulFaces = criticalHitImmune ? 0 : 1;
        var successfulFaces = Math.Clamp(21 - Math.Max(requiredRoll, 2), minimumSuccessfulFaces, 19);
        return successfulFaces * 5;
    }

    internal static double AttackHitChanceWithDice(
        int armourClass,
        int attackBonus,
        int bonusD4Count,
        bool advantage,
        bool disadvantage,
        int criticalThreshold = 20)
    {
        var successes = 0;
        var total = 0;
        var bonusSums = new List<int> { 0 };
        for (var die = 0; die < bonusD4Count; die++)
            bonusSums = bonusSums.SelectMany(sum => Enumerable.Range(1, 4).Select(face => sum + face)).ToList();
        for (var first = 1; first <= 20; first++)
        for (var second = 1; second <= 20; second++)
        foreach (var bonus in bonusSums)
        {
            var roll = advantage == disadvantage ? first : advantage ? Math.Max(first, second) : Math.Min(first, second);
            if (advantage == disadvantage && second != 1)
                continue;
            if (roll != 1 && (roll >= criticalThreshold || roll + attackBonus + bonus >= armourClass))
                successes++;
            total++;
        }
        return Math.Round(successes * 100.0 / total, 2, MidpointRounding.AwayFromZero);
    }

    internal static double SavingThrowFailureChance(int savingThrowBonus, int dc, int bonusD4Count, bool advantage, bool disadvantage)
    {
        var fails = 0;
        var total = 0;
        var bonusSums = new List<int> { 0 };
        for (var die = 0; die < bonusD4Count; die++)
            bonusSums = bonusSums.SelectMany(sum => Enumerable.Range(1, 4).Select(face => sum + face)).ToList();
        for (var first = 1; first <= 20; first++)
        for (var second = 1; second <= 20; second++)
        foreach (var bonus in bonusSums)
        {
            var roll = advantage == disadvantage ? first : advantage ? Math.Max(first, second) : Math.Min(first, second);
            if (advantage == disadvantage && second != 1)
                continue;
            // Ordinary combat saving throws in BG3 do not automatically fail on a
            // natural 1 or succeed on a natural 20. Concentration and dialogue
            // saves are exceptions, neither of which is represented by these cards.
            var succeeds = roll + savingThrowBonus + bonus >= dc;
            if (!succeeds) fails++;
            total++;
        }
        return Math.Round(fails * 100.0 / total, 2, MidpointRounding.AwayFromZero);
    }

    private static Dictionary<string, double> CalculateSpellEffectChances(
        CharacterState state,
        int dc,
        Dictionary<string, int> saves,
        List<ItemEffect> effects,
        bool generalAdvantage,
        bool generalDisadvantage,
        int bonusD4Count)
    {
        var probabilities = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var ability in AbilityNames)
        {
            var specificAdvantage = effects.Any(effect => effect.Kind == ItemEffectKind.SavingThrowAdvantage && effect.Scope == ability);
            if (ability == "DEX" && state.HasBuff("Haste")) specificAdvantage = true;
            if (ability == "DEX" && state.HasBuff("Danger Sense") && state.GetClassLevel("Barbarian") >= 2) specificAdvantage = true;
            if (ability == "WIS" && state.HasBuff("Heroes' Feast")) specificAdvantage = true;
            var chance = SavingThrowFailureChance(saves[ability], dc, bonusD4Count, generalAdvantage || specificAdvantage, generalDisadvantage);
            if (state.HasBuff("Indomitable") && state.GetClassLevel("Fighter") >= 9)
                chance = Math.Round(chance * chance / 100.0, 2, MidpointRounding.AwayFromZero);
            probabilities[ability] = chance;
        }
        return probabilities;
    }

    internal static double ApplyRollMode(double normalChance, bool advantage, bool disadvantage)
    {
        if (advantage == disadvantage)
            return normalChance;
        var probability = normalChance / 100.0;
        var adjusted = disadvantage
            ? probability * probability
            : 1 - Math.Pow(1 - probability, 2);
        return Math.Round(adjusted * 100, 2, MidpointRounding.AwayFromZero);
    }

    private static string FullAbilityName(string ability) => ability switch
    {
        "STR" => "Strength",
        "DEX" => "Dexterity",
        "CON" => "Constitution",
        "INT" => "Intelligence",
        "WIS" => "Wisdom",
        "CHA" => "Charisma",
        _ => ability
    };

    [GeneratedRegex(@"(?<!\+)\b(\d{2})\s*AC\b", RegexOptions.IgnoreCase)]
    private static partial Regex ListedAcRegex();

    [GeneratedRegex(@"\+\s*(\d+)\s*AC\b", RegexOptions.IgnoreCase)]
    private static partial Regex AcBonusRegex();

    [GeneratedRegex(@"\d+d\d+\s*\+\s*(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex DamageBonusRegex();
}
