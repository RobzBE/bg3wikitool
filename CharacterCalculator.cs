using System.Text.RegularExpressions;

namespace BG3ItemExplorer;

internal sealed record ClassProfile(
    string SpellAbility,
    string AttackAbility,
    string SaveOne,
    string SaveTwo,
    int StartingHp,
    int HpPerLevel,
    string[] ArmourTraining);

internal sealed record EnemyThreatProfile(
    string Act,
    string AttackEnemy,
    int AttackBonus,
    string SpellEnemy,
    int SpellDc);

internal sealed record ActThreat(
    string Act,
    string AttackEnemy,
    int AttackBonus,
    string SpellEnemy,
    int SpellAttackBonus,
    int SpellDc,
    double AttackHitChance,
    double SpellAttackHitChance,
    double SpellEffectChance,
    string SpellSaveAbility);

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
}

internal static partial class CharacterCalculator
{
    public static readonly string[] Classes = ["Barbarian", "Bard", "Cleric", "Druid", "Fighter", "Monk", "Paladin", "Ranger", "Rogue", "Sorcerer", "Warlock", "Wizard"];
    public static readonly string[] Races = ["Human", "Elf", "Drow", "Half-Elf", "Half-Orc", "Halfling", "Dwarf", "Gnome", "Githyanki", "Dragonborn", "Tiefling"];
    public static readonly string[] Difficulties = ["Explorer", "Balanced", "Tactician", "Honour"];
    public static readonly string[] AbilityNames = ["STR", "DEX", "CON", "INT", "WIS", "CHA"];

    private static readonly Dictionary<string, ClassProfile> Profiles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Barbarian"] = new("CHA", "STR", "STR", "CON", 12, 7, ["Light", "Medium", "Shield"]),
        ["Bard"] = new("CHA", "DEX", "DEX", "CHA", 8, 5, ["Light"]),
        ["Cleric"] = new("WIS", "WIS", "WIS", "CHA", 8, 5, ["Light", "Medium", "Shield"]),
        ["Druid"] = new("WIS", "WIS", "INT", "WIS", 8, 5, ["Light", "Medium", "Shield"]),
        ["Fighter"] = new("INT", "STR", "STR", "CON", 10, 6, ["Light", "Medium", "Heavy", "Shield"]),
        ["Monk"] = new("WIS", "DEX", "STR", "DEX", 8, 5, []),
        ["Paladin"] = new("CHA", "STR", "WIS", "CHA", 10, 6, ["Light", "Medium", "Heavy", "Shield"]),
        ["Ranger"] = new("WIS", "DEX", "STR", "DEX", 10, 6, ["Light", "Medium", "Shield"]),
        ["Rogue"] = new("INT", "DEX", "DEX", "INT", 8, 5, ["Light"]),
        ["Sorcerer"] = new("CHA", "CHA", "CON", "CHA", 6, 4, []),
        ["Warlock"] = new("CHA", "CHA", "WIS", "CHA", 8, 5, ["Light"]),
        ["Wizard"] = new("INT", "INT", "INT", "WIS", 6, 4, [])
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
            new("ACT 1", "Grym", 11, "Grym", 19),
            new("ACT 2", "Apostle of Myrkul", 10, "Apostle of Myrkul", 17),
            new("ACT 3", "Dominated Red Dragon", 14, "Netherbrain", 23)
        ],
        ["Explorer"] =
        [
            new("ACT 1", "Grym", 13, "Grym", 21),
            new("ACT 2", "Apostle of Myrkul", 12, "Apostle of Myrkul", 19),
            new("ACT 3", "Dominated Red Dragon", 16, "Netherbrain", 25)
        ],
        ["Tactician"] =
        [
            new("ACT 1", "Grym", 13, "Grym", 21),
            new("ACT 2", "Apostle of Myrkul", 13, "Apostle of Myrkul", 21),
            new("ACT 3", "Dominated Red Dragon", 16, "Netherbrain", 25)
        ],
        ["Honour"] =
        [
            new("ACT 1", "Grym", 13, "Grym", 21),
            new("ACT 2", "Apostle of Myrkul", 13, "Apostle of Myrkul", 21),
            new("ACT 3", "Dominated Red Dragon", 16, "Netherbrain", 25)
        ]
    };

    public static CharacterStats Calculate(CharacterState state, IEnumerable<ItemRecord> allItems)
    {
        var equipped = allItems.Where(item => item.Equipped).ToList();
        var parsedEffects = ItemEffectParser.ParseEquipped(equipped);
        var activeEffects = parsedEffects.Where(state.IsEffectActive).ToList();
        var profile = Profiles.GetValueOrDefault(state.ClassName, Profiles["Fighter"]);
        var nonProficientGear = equipped.Where(item => !IsArmourProficient(state, profile, item)).Select(item => item.Name).ToList();
        var abilities = AbilityNames.ToDictionary(name => name, state.GetAbility, StringComparer.OrdinalIgnoreCase);
        ApplyEquipmentAbilityChanges(abilities, equipped);

        var proficiency = (state.Level <= 4 ? 2 : state.Level <= 8 ? 3 : 4)
                          + (state.Difficulty.Equals("Explorer", StringComparison.OrdinalIgnoreCase) ? 2 : 0);
        var dexterityModifier = Modifier(abilities["DEX"]);
        var armourClass = CalculateArmourClass(state, abilities, equipped, activeEffects);
        var spellBonus = activeEffects.Where(effect => effect.Kind == ItemEffectKind.SpellSaveDcBonus).Sum(effect => effect.Value);
        var spellAttackBonus = activeEffects.Where(effect => effect.Kind == ItemEffectKind.SpellAttackBonus).Sum(effect => effect.Value);
        var spellSaveDc = 8 + proficiency + Modifier(abilities[profile.SpellAbility]) + spellBonus;
        var spellAttack = proficiency + Modifier(abilities[profile.SpellAbility]) + spellAttackBonus;

        var attackAbility = DetermineWeaponAbility(profile.AttackAbility, profile.SpellAbility, abilities, equipped);
        var weaponAttack = proficiency + Modifier(abilities[attackAbility]) + ExtractWeaponEnchantment(equipped)
                           + activeEffects.Where(effect => effect.Kind == ItemEffectKind.AttackRollBonus).Sum(effect => effect.Value);
        var mainHand = equipped.FirstOrDefault(item => GearRules.SlotFor(item) == "Main Hand");
        if (mainHand?.Description.Contains("Double your Proficiency Bonus", StringComparison.OrdinalIgnoreCase) == true && nonProficientGear.Count == 0)
            weaponAttack += proficiency;
        var hitPoints = profile.StartingHp + Modifier(abilities["CON"])
                        + Math.Max(0, state.Level - 1) * (profile.HpPerLevel + Modifier(abilities["CON"]));
        hitPoints = Math.Max(state.Level, hitPoints);
        if (state.Difficulty.Equals("Explorer", StringComparison.OrdinalIgnoreCase))
            hitPoints *= 2;
        var initiative = dexterityModifier + activeEffects.Where(effect => effect.Kind == ItemEffectKind.InitiativeBonus).Sum(effect => effect.Value);

        var generalSaveBonus = activeEffects.Where(effect => effect.Kind == ItemEffectKind.SavingThrowBonus && effect.Scope == "ALL").Sum(effect => effect.Value);
        var saves = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var ability in AbilityNames)
        {
            var value = Modifier(abilities[ability]) + generalSaveBonus;
            if (ability == profile.SaveOne || ability == profile.SaveTwo)
                value += proficiency;
            value += activeEffects.Where(effect => effect.Kind == ItemEffectKind.SavingThrowBonus && effect.Scope == ability).Sum(effect => effect.Value);
            saves[ability] = value;
        }

        var enemyAttackDisadvantage = activeEffects.Any(effect => effect.Kind == ItemEffectKind.EnemyAttackDisadvantage);
        var enemySpellAttackDisadvantage = enemyAttackDisadvantage || activeEffects.Any(effect => effect.Kind == ItemEffectKind.EnemySpellAttackDisadvantage);
        var generalSaveAdvantage = activeEffects.Any(effect => effect.Kind == ItemEffectKind.SavingThrowAdvantage && effect.Scope == "ALL");
        var spellSaveAdvantage = generalSaveAdvantage || activeEffects.Any(effect => effect.Kind == ItemEffectKind.SpellSavingThrowAdvantage);
        var generalSaveDisadvantage = nonProficientGear.Count > 0 || activeEffects.Any(effect => effect.Kind == ItemEffectKind.SavingThrowDisadvantage);
        var criticalHitImmune = activeEffects.Any(effect => effect.Kind == ItemEffectKind.CriticalHitImmunity);
        var threatProfiles = WorstCaseThreats.GetValueOrDefault(state.Difficulty, WorstCaseThreats["Balanced"]);
        var threats = threatProfiles
            .Select(profile =>
            {
                var spellAttackBonus = profile.SpellDc - 8;
                var spellEffect = CalculateWorstSpellEffectChance(profile.SpellDc, saves, activeEffects, spellSaveAdvantage, generalSaveDisadvantage);
                return new ActThreat(
                    profile.Act,
                    profile.AttackEnemy,
                    profile.AttackBonus,
                    profile.SpellEnemy,
                    spellAttackBonus,
                    profile.SpellDc,
                    ApplyRollMode(AttackHitChance(armourClass, profile.AttackBonus, criticalHitImmune), false, enemyAttackDisadvantage),
                    ApplyRollMode(AttackHitChance(armourClass, spellAttackBonus, criticalHitImmune), false, enemySpellAttackDisadvantage),
                    spellEffect.Chance,
                    spellEffect.Ability);
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
            Movement = RaceMovement.GetValueOrDefault(state.Race, 9m),
            SpellAbility = profile.SpellAbility,
            AttackAbility = attackAbility,
            Threats = threats,
            ActiveEffects = activeEffects,
            AttackRollAdvantage = activeEffects.Any(effect => effect.Kind == ItemEffectKind.AttackRollAdvantage),
            AttackRollDisadvantage = nonProficientGear.Count > 0,
            EnemySavingThrowDisadvantage = activeEffects.Any(effect => effect.Kind == ItemEffectKind.EnemySavingThrowDisadvantage),
            CriticalHitImmune = criticalHitImmune,
            DamageReduction = activeEffects.Where(effect => effect.Kind == ItemEffectKind.DamageReduction).Sum(effect => effect.Value),
            Resistances = activeEffects.Where(effect => effect.Kind == ItemEffectKind.Resistance).Select(effect => effect.Scope).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value).ToList(),
            NonProficientGear = nonProficientGear
        };
    }

    public static int Modifier(int score) => (int)Math.Floor((score - 10) / 2.0);

    private static int CalculateArmourClass(CharacterState state, Dictionary<string, int> abilities, List<ItemRecord> equipped, List<ItemEffect> activeEffects)
    {
        var dex = Modifier(abilities["DEX"]);
        var body = equipped.FirstOrDefault(item => GearRules.SlotFor(item) == "Body");
        int baseAc;
        if (body is null || body.Type.Equals("Clothing", StringComparison.OrdinalIgnoreCase))
        {
            baseAc = 10 + dex;
            if (state.ClassName.Equals("Barbarian", StringComparison.OrdinalIgnoreCase))
                baseAc = Math.Max(baseAc, 10 + dex + Modifier(abilities["CON"]));
            if (state.ClassName.Equals("Monk", StringComparison.OrdinalIgnoreCase) && !equipped.Any(item => item.Type.Equals("Shield", StringComparison.OrdinalIgnoreCase)))
                baseAc = Math.Max(baseAc, 10 + dex + Modifier(abilities["WIS"]));
        }
        else
        {
            var listedAc = ExtractListedAc(body.Properties);
            if (body.Type.Contains("Heavy", StringComparison.OrdinalIgnoreCase))
                baseAc = listedAc;
            else if (body.Type.Contains("Medium", StringComparison.OrdinalIgnoreCase))
                baseAc = listedAc + (body.Description.Contains("Dexterity Modifier", StringComparison.OrdinalIgnoreCase) ? dex : Math.Min(2, dex));
            else
                baseAc = listedAc + dex;
        }

        return baseAc
               + equipped.Sum(item => ExtractAcBonus(item.Properties))
               + activeEffects.Where(effect => effect.Kind == ItemEffectKind.ArmourClassBonus).Sum(effect => effect.Value);
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
        var weapon = equipped.FirstOrDefault(item => GearRules.SlotFor(item) == "Main Hand");
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

    private static bool IsArmourProficient(CharacterState state, ClassProfile profile, ItemRecord item)
    {
        string? required = null;
        if (item.Type.Equals("Shield", StringComparison.OrdinalIgnoreCase)) required = "Shield";
        else if (item.Type.Contains("Heavy", StringComparison.OrdinalIgnoreCase)) required = "Heavy";
        else if (item.Type.Contains("Medium", StringComparison.OrdinalIgnoreCase)) required = "Medium";
        else if (item.Type.Contains("Light", StringComparison.OrdinalIgnoreCase)) required = "Light";
        if (required is null)
            return true;
        if (profile.ArmourTraining.Contains(required, StringComparer.OrdinalIgnoreCase))
            return true;
        if (state.Race.Equals("Human", StringComparison.OrdinalIgnoreCase) && required is "Light" or "Shield")
            return true;
        if (state.Race.Equals("Githyanki", StringComparison.OrdinalIgnoreCase) && required is "Light" or "Medium")
            return true;
        return false;
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
        var weapon = equipped.FirstOrDefault(item => GearRules.SlotFor(item) == "Main Hand");
        if (weapon is null)
            return 0;
        return DamageBonusRegex().Matches(weapon.Properties)
            .Select(match => int.Parse(match.Groups[1].Value))
            .Where(value => value is >= 1 and <= 3)
            .DefaultIfEmpty(0)
            .Max();
    }

    internal static double AttackHitChance(int armourClass, int attackBonus, bool criticalHitImmune = false)
    {
        var requiredRoll = armourClass - attackBonus;
        // Natural 1 always misses. Natural 20 always hits unless critical-hit immunity
        // turns it into a regular roll that still has to meet the target's AC.
        var minimumSuccessfulFaces = criticalHitImmune ? 0 : 1;
        var successfulFaces = Math.Clamp(21 - Math.Max(requiredRoll, 2), minimumSuccessfulFaces, 19);
        return successfulFaces * 5;
    }

    private static double SpellEffectChance(int savingThrowBonus, int dc)
    {
        var failingFaces = Math.Clamp(dc - savingThrowBonus - 1, 0, 20);
        return failingFaces * 5;
    }

    private static (double Chance, string Ability) CalculateWorstSpellEffectChance(
        int dc,
        Dictionary<string, int> saves,
        List<ItemEffect> effects,
        bool generalAdvantage,
        bool generalDisadvantage)
    {
        var probabilities = new List<(double Chance, string Ability)>();
        foreach (var ability in new[] { "DEX", "CON", "WIS" })
        {
            var specificAdvantage = effects.Any(effect => effect.Kind == ItemEffectKind.SavingThrowAdvantage && effect.Scope == ability);
            var normal = SpellEffectChance(saves[ability], dc);
            probabilities.Add((ApplyRollMode(normal, generalDisadvantage, generalAdvantage || specificAdvantage), ability));
        }
        return probabilities.OrderByDescending(value => value.Chance).First();
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
