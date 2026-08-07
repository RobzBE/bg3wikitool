using System.Text.RegularExpressions;

namespace BG3ItemExplorer;

internal enum ItemEffectKind
{
    EnemyAttackDisadvantage,
    EnemySpellAttackDisadvantage,
    SavingThrowAdvantage,
    SpellSavingThrowAdvantage,
    SavingThrowDisadvantage,
    AttackRollAdvantage,
    EnemySavingThrowDisadvantage,
    CriticalHitImmunity,
    CriticalThresholdReduction,
    DamageReduction,
    Resistance,
    ArmourClassBonus,
    SpellSaveDcBonus,
    SpellAttackBonus,
    AttackRollBonus,
    InitiativeBonus,
    SavingThrowBonus
}

internal sealed record ItemEffect(
    string Id,
    string ItemName,
    ItemEffectKind Kind,
    string Scope,
    string Summary,
    bool Conditional,
    bool DefaultActive,
    int Value = 0);

internal static class ItemEffectParser
{
    public static List<ItemEffect> Parse(ItemRecord item)
    {
        var text = string.Join(" ", item.Properties, item.Description).ReplaceLineEndings(" ");
        var effects = new List<ItemEffect>();

        AddIf(effects, item, text,
            Regex.IsMatch(text, @"\benemies\s+have\s+Disadvantage\s+on\s+Attack Rolls\s+against\s+you", RegexOptions.IgnoreCase),
            ItemEffectKind.EnemyAttackDisadvantage, "All attacks", "Enemy attack rolls have Disadvantage");
        AddIf(effects, item, text,
            Regex.IsMatch(text, @"Spell Attack Rolls\s+against\s+you\s+have\s+Disadvantage", RegexOptions.IgnoreCase),
            ItemEffectKind.EnemySpellAttackDisadvantage, "Spell attacks", "Enemy spell attack rolls have Disadvantage");

        foreach (var ability in CharacterCalculator.AbilityNames)
        {
            var fullName = FullAbilityName(ability);
            AddIf(effects, item, text,
                Regex.IsMatch(text, $@"Advantage\s+(?:on|with)\s+{fullName}(?:[^.]*?)Saving Throws?", RegexOptions.IgnoreCase),
                ItemEffectKind.SavingThrowAdvantage, ability, $"Advantage on {ability} saving throws");
            AddIf(effects, item, text,
                Regex.IsMatch(text, $@"Advantage\s+(?:on|with)[^.\r\n]{{0,90}}\b{fullName}\b[^.\r\n]{{0,55}}Saving Throws?", RegexOptions.IgnoreCase)
                && !effects.Any(effect => effect.Kind == ItemEffectKind.SavingThrowAdvantage && effect.Scope == ability),
                ItemEffectKind.SavingThrowAdvantage, ability, $"Advantage on {ability} saving throws");
        }

        AddIf(effects, item, text,
            Regex.IsMatch(text, @"Advantage\s+on\s+Saving Throws\s+against\s+spells", RegexOptions.IgnoreCase)
            || Regex.IsMatch(text, @"Advantage\s+on\s+Saving Throws\s+against\s+their[^.]*spells", RegexOptions.IgnoreCase),
            ItemEffectKind.SpellSavingThrowAdvantage, "Spells", "Advantage on saving throws against spells");
        AddIf(effects, item, text,
            Regex.IsMatch(text, @"Advantage\s+on\s+Saving Throws(?:\.|,|\s+until)", RegexOptions.IgnoreCase)
            && !Regex.IsMatch(text, @"Advantage\s+on\s+Saving Throws\s+against", RegexOptions.IgnoreCase),
            ItemEffectKind.SavingThrowAdvantage, "ALL", "Advantage on saving throws");
        AddIf(effects, item, text,
            Regex.IsMatch(text, @"Disadvantage\s+on\s+Saving Throws", RegexOptions.IgnoreCase),
            ItemEffectKind.SavingThrowDisadvantage, "ALL", "Disadvantage on saving throws");
        AddIf(effects, item, text,
            Regex.IsMatch(text, @"Advantage\s+on\s+Attack Rolls", RegexOptions.IgnoreCase)
            && !Regex.IsMatch(text, @"Advantage\s+on\s+Attack Rolls\s+against\s+(?:aberrations|undead|goblins)", RegexOptions.IgnoreCase),
            ItemEffectKind.AttackRollAdvantage, "ALL", "Advantage on your attack rolls");
        AddIf(effects, item, text,
            Regex.IsMatch(text, @"Enemies\s+have\s+Disadvantage\s+on\s+Saving Throws", RegexOptions.IgnoreCase)
            || Regex.IsMatch(text, @"Creatures\s+have\s+Disadvantage\s+on\s+Saving Throws", RegexOptions.IgnoreCase),
            ItemEffectKind.EnemySavingThrowDisadvantage, "Scoped", "Enemies have Disadvantage on relevant saving throws");
        AddIf(effects, item, text,
            Regex.IsMatch(text, @"(?:Attackers|Enemies)\s+can(?:not|'t)\s+land\s+Critical Hits", RegexOptions.IgnoreCase),
            ItemEffectKind.CriticalHitImmunity, "ALL", "Attackers cannot land critical hits");

        var reduction = Regex.Match(text, @"Reduce all incoming damage by\s+(\d+)", RegexOptions.IgnoreCase);
        if (reduction.Success)
            Add(effects, item, text, ItemEffectKind.DamageReduction, "ALL", $"Incoming damage reduced by {reduction.Groups[1].Value}", int.Parse(reduction.Groups[1].Value));

        var resistances = Regex.Matches(text, @"Resistance to\s+([A-Za-z]+)\s+damage", RegexOptions.IgnoreCase)
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var resistance in resistances)
            Add(effects, item, text, ItemEffectKind.Resistance, resistance, $"Resistance to {resistance} damage");

        AddNumericEffects(effects, item);

        return effects;
    }

    public static List<ItemEffect> ParseEquipped(IEnumerable<ItemRecord> items) => items.Where(item => item.Equipped).SelectMany(Parse).ToList();

    private static void AddIf(List<ItemEffect> effects, ItemRecord item, string text, bool condition, ItemEffectKind kind, string scope, string summary)
    {
        if (condition)
            Add(effects, item, text, kind, scope, summary);
    }

    private static void Add(List<ItemEffect> effects, ItemRecord item, string text, ItemEffectKind kind, string scope, string summary, int value = 0)
    {
        var conditional = Regex.IsMatch(text, @"\b(?:while|until|if|when|once per|at the beginning)\b", RegexOptions.IgnoreCase);
        var defaultActive = !conditional || Regex.IsMatch(text, @"\b(?:at the beginning of your turn|at the beginning of the wearer's turn|at the start of your turn)\b", RegexOptions.IgnoreCase);
        effects.Add(new ItemEffect($"{item.ProgressKey}|{kind}|{scope}", item.Name, kind, scope, summary, conditional, defaultActive, value));
    }

    private static void AddNumericEffects(List<ItemEffect> effects, ItemRecord item)
    {
        var sentences = item.Description.Split(['\r', '\n', '.'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var sentence in sentences)
        {
            if (Regex.IsMatch(sentence, @"number you need to roll a Critical Hit[^.]*reduced by\s+1", RegexOptions.IgnoreCase)
                || Regex.IsMatch(sentence, @"land a Critical Hit when rolling a 19", RegexOptions.IgnoreCase))
            {
                // "while attacking" and "when rolling a 19" describe the roll, not
                // an optional state. Preserve real conditions such as while Hiding,
                // Obscured, Invisible, or while the off-hand is empty.
                var conditionText = Regex.Replace(sentence, @"\bwhile attacking\b|\bwhen rolling a 19\b", "", RegexOptions.IgnoreCase);
                Add(effects, item, conditionText, ItemEffectKind.CriticalThresholdReduction, "Attack rolls", "Critical threshold -1", 1);
            }
            AddNumericMatch(effects, item, sentence, ItemEffectKind.SpellSaveDcBonus, "Spell Save DC", @"\+\s*(\d+)\s*(?:bonus\s+to\s+)?Spell Save DC", "Spell Save DC");
            AddNumericMatch(effects, item, sentence, ItemEffectKind.SpellSaveDcBonus, "Spell Save DC", @"\+\s*(\d+)[^.]{0,55}Spell Attack Rolls?[^.]{0,40}Spell Save DC", "Spell Save DC");
            AddNumericMatch(effects, item, sentence, ItemEffectKind.SpellSaveDcBonus, "Spell Save DC", @"\+\s*(\d+)[^.]{0,55}Attack Rolls?[^.]{0,45}Spell Save DC", "Spell Save DC");
            AddNumericMatch(effects, item, sentence, ItemEffectKind.SpellAttackBonus, "Spell attacks", @"\+\s*(\d+)\s*(?:bonus\s+to\s+)?Spell Attack Rolls?", "spell attack rolls");
            AddNumericMatch(effects, item, sentence, ItemEffectKind.SpellAttackBonus, "Spell attacks", @"\+\s*(\d+)[^.]{0,55}Spell Save DC[^.]{0,40}Spell Attack Rolls?", "spell attack rolls");
            if (!sentence.Contains("Spell Attack", StringComparison.OrdinalIgnoreCase))
            {
                AddNumericMatch(effects, item, sentence, ItemEffectKind.AttackRollBonus, "Attacks", @"\+\s*(\d+)\s*(?:bonus\s+to\s+)?(?:Attack|Weapon Attack) Rolls?", "attack rolls");
                AddNumericMatch(effects, item, sentence, ItemEffectKind.AttackRollBonus, "Attacks", @"\+\s*(\d+)\s*(?:bonus\s+to\s+)?Attack(?:\s+and\s+damage)?\s+Rolls?", "attack rolls");
                AddNumericMatch(effects, item, sentence, ItemEffectKind.AttackRollBonus, "Attacks", @"Attack Rolls?\s*\+\s*(\d+)", "attack rolls");
            }
            AddNumericMatch(effects, item, sentence, ItemEffectKind.InitiativeBonus, "Initiative", @"\+\s*(\d+)\s*(?:bonus\s+to\s+)?Initiative(?: Rolls?)?", "initiative");
            AddNumericMatch(effects, item, sentence, ItemEffectKind.InitiativeBonus, "Initiative", @"Initiative(?: Rolls?)?\s*\+\s*(\d+)", "initiative");
            AddNumericMatch(effects, item, sentence, ItemEffectKind.ArmourClassBonus, "AC", @"\+\s*(\d+)\s*AC\b", "AC");
            foreach (var ability in CharacterCalculator.AbilityNames)
            {
                var full = FullAbilityName(ability);
                AddNumericMatch(effects, item, sentence, ItemEffectKind.SavingThrowBonus, ability, $@"(?:{full}\s+Saving Throws?\s*[:]?\s*\+\s*(\d+)|\+\s*(\d+)\s*bonus\s+to\s+{full}\s+Saving Throws?)", $"{ability} saves");
            }
            if (!CharacterCalculator.AbilityNames.Any(ability => sentence.Contains(FullAbilityName(ability) + " Saving", StringComparison.OrdinalIgnoreCase)))
                AddNumericMatch(effects, item, sentence, ItemEffectKind.SavingThrowBonus, "ALL", @"(?:Saving Throws?\s*\+\s*(\d+)|\+\s*(\d+)\s*bonus\s+to\s+Saving Throws?)", "saving throws");
        }
    }

    private static void AddNumericMatch(List<ItemEffect> effects, ItemRecord item, string sentence, ItemEffectKind kind, string scope, string pattern, string label)
    {
        var match = Regex.Match(sentence, pattern, RegexOptions.IgnoreCase);
        if (!match.Success)
            return;
        var valueGroup = match.Groups.Cast<Group>().Skip(1).FirstOrDefault(group => group.Success && int.TryParse(group.Value, out _));
        if (valueGroup is null)
            return;
        var value = int.Parse(valueGroup.Value);
        var stackMatch = Regex.Match(sentence, @"(?:stacking\s+)?up to\s+(\d+)\s+(?:times|stacks?)", RegexOptions.IgnoreCase);
        var stackSuffix = "";
        if (stackMatch.Success)
        {
            var stacks = int.Parse(stackMatch.Groups[1].Value);
            value *= stacks;
            stackSuffix = $" at {stacks} stacks";
        }
        var conditional = Regex.IsMatch(sentence, @"\b(?:while|until|if|when|after|as long as|once per|for every|per stack|stacking)\b", RegexOptions.IgnoreCase);
        var defaultActive = !conditional || Regex.IsMatch(sentence, @"\b(?:at the beginning of your turn|at the start of your turn)\b", RegexOptions.IgnoreCase);
        var id = $"{item.ProgressKey}|{kind}|{scope}|{value}";
        if (!effects.Any(effect => effect.Id.Equals(id, StringComparison.OrdinalIgnoreCase)))
            effects.Add(new ItemEffect(id, item.Name, kind, scope, $"+{value} {label}{stackSuffix}", conditional, defaultActive, value));
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
}
