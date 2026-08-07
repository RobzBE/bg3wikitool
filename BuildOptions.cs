namespace BG3ItemExplorer;

internal sealed class FeatSelection
{
    public string Name { get; set; } = "";
    public string Choice { get; set; } = "";
}

internal sealed record FeatDefinition(string Name, string Description, string[] Choices);

internal sealed record BuffDefinition(
    string Name,
    string Description,
    bool Concentration = false);

internal sealed record ClassOptionDefinition(
    string BuffName,
    string ClassName,
    int MinimumLevel,
    string SubclassName = "");

internal static class BuildOptions
{
    public const string None = "(None)";

    public static readonly string[] FightingStyles =
        ["Archery", "Defence", "Duelling", "Great Weapon Fighting", "Protection", "Two-Weapon Fighting"];

    public static readonly IReadOnlyDictionary<string, string[]> FightingStylesByClass =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Fighter"] = FightingStyles,
            ["Paladin"] = ["Defence", "Duelling", "Great Weapon Fighting", "Protection"],
            ["Ranger"] = ["Archery", "Defence", "Duelling", "Two-Weapon Fighting"]
        };

    public static readonly (string Key, string ClassName, string Label, int MinimumLevel)[] FightingStyleSlots =
    [
        ("Fighter", "Fighter", "Fighter", 1),
        ("Fighter 2", "Fighter", "Fighter (level 10)", 10),
        ("Paladin", "Paladin", "Paladin", 2),
        ("Ranger", "Ranger", "Ranger", 2)
    ];

    public static string FightingStyleSlotKey(string className, int index) =>
        index == 0 ? className : $"{className} {index + 1}";

    public static string[] FightingStyleChoices(string slotKey)
    {
        var slot = FightingStyleSlots.FirstOrDefault(slot => slot.Key.Equals(slotKey, StringComparison.OrdinalIgnoreCase));
        return FightingStylesByClass.GetValueOrDefault(slot.ClassName ?? slotKey, []);
    }

    public static readonly IReadOnlyDictionary<string, string[]> SubclassesByClass =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Barbarian"] = ["Berserker", "Giant", "Wild Magic", "Wildheart"],
            ["Bard"] = ["College of Glamour", "College of Lore", "College of Swords", "College of Valour"],
            ["Cleric"] = ["Death Domain", "Knowledge Domain", "Life Domain", "Light Domain", "Nature Domain", "Tempest Domain", "Trickery Domain", "War Domain"],
            ["Druid"] = ["Circle of the Land", "Circle of the Moon", "Circle of the Spores", "Circle of the Stars"],
            ["Fighter"] = ["Arcane Archer", "Battle Master", "Champion", "Eldritch Knight"],
            ["Monk"] = ["Way of the Drunken Master", "Way of the Four Elements", "Way of the Open Hand", "Way of Shadow"],
            ["Paladin"] = ["Oath of the Ancients", "Oath of Devotion", "Oath of Vengeance", "Oath of the Crown", "Oathbreaker"],
            ["Ranger"] = ["Beast Master", "Gloom Stalker", "Hunter", "Swarmkeeper"],
            ["Rogue"] = ["Arcane Trickster", "Assassin", "Swashbuckler", "Thief"],
            ["Sorcerer"] = ["Draconic Bloodline", "Shadow Magic", "Storm Sorcery", "Wild Magic"],
            ["Warlock"] = ["The Archfey", "The Fiend", "The Great Old One", "The Hexblade"],
            ["Wizard"] = ["Abjuration School", "Bladesinging", "Conjuration School", "Divination School", "Enchantment School", "Evocation School", "Illusion School", "Necromancy School", "Transmutation School"]
        };

    public static readonly ClassOptionDefinition[] ClassOptions =
    [
        new("Rage", "Barbarian", 1),
        new("Reckless Attack", "Barbarian", 2),
        new("Danger Sense", "Barbarian", 2),
        new("Indomitable", "Fighter", 9),
        new("Patient Defence", "Monk", 2),
        new("Paladin Aura active", "Paladin", 6),
        new("Champion: Improved Critical Hit", "Fighter", 3, "Champion")
    ];

    public static readonly FeatDefinition[] Feats =
    [
        new("Ability Improvement", "+2 to one ability or +1 to two abilities (maximum 20).", AbilityImprovementChoices()),
        new("Actor", "+1 Charisma; Deception and Performance expertise.", ["CHA"]),
        new("Alert", "+5 Initiative and cannot be Surprised.", []),
        new("Athlete", "+1 Strength or Dexterity; improved standing jump and jump distance.", ["STR", "DEX"]),
        new("Charger", "Gain Charger: Weapon Attack and Charger: Shove.", []),
        new("Crossbow Expert", "Crossbow attacks in melee lose disadvantage; Piercing Shot lasts longer.", []),
        new("Defensive Duellist", "Reaction: add proficiency to AC while wielding a finesse weapon; requires Dexterity 13.", []),
        new("Dual Wielder", "+1 AC while dual-wielding melee weapons; permits non-heavy weapons.", []),
        new("Dungeon Delver", "Advantage to notice/resist traps and resistance to trap damage.", []),
        new("Durable", "+1 Constitution; regain full HP on Short Rest.", ["CON"]),
        new("Elemental Adept", "Ignore resistance and prevent damage rolls of 1 for the chosen element.", ["Acid", "Cold", "Fire", "Lightning", "Thunder"]),
        new("Great Weapon Master", "Bonus attack after crit/kill; optional -5 attack for +10 damage.", []),
        new("Heavily Armoured", "+1 Strength and Heavy Armour proficiency; requires Medium Armour proficiency.", ["STR"]),
        new("Heavy Armour Master", "+1 Strength and 3 physical damage reduction in Heavy Armour; requires Heavy Armour proficiency.", ["STR"]),
        new("Lightly Armoured", "+1 Strength or Dexterity and Light Armour proficiency.", ["STR", "DEX"]),
        new("Lucky", "3 Luck Points for advantage or to force an enemy attack reroll.", []),
        new("Mage Slayer", "Reaction and conditional advantages against nearby spellcasters.", []),
        new("Magic Initiate: Bard", "Learn two Bard cantrips and one level 1 Bard spell.", []),
        new("Magic Initiate: Cleric", "Learn two Cleric cantrips and one level 1 Cleric spell.", []),
        new("Magic Initiate: Druid", "Learn two Druid cantrips and one level 1 Druid spell.", []),
        new("Magic Initiate: Sorcerer", "Learn two Sorcerer cantrips and one level 1 Sorcerer spell.", []),
        new("Magic Initiate: Warlock", "Learn two Warlock cantrips and one level 1 Warlock spell.", []),
        new("Magic Initiate: Wizard", "Learn two Wizard cantrips and one level 1 Wizard spell.", []),
        new("Martial Adept", "Learn two Battle Master manoeuvres and gain one superiority die.", []),
        new("Medium Armour Master", "Medium Armour allows up to +3 Dexterity AC and no Stealth disadvantage; requires Medium proficiency.", []),
        new("Mobile", "+3 m movement; difficult terrain and opportunity-attack benefits while dashing/attacking.", []),
        new("Moderately Armoured", "+1 Strength or Dexterity and Medium Armour/Shield proficiency; requires Light proficiency.", ["STR", "DEX"]),
        new("Performer", "+1 Charisma and musical-instrument proficiency.", ["CHA"]),
        new("Polearm Master", "Bonus-action haft attack and opportunity attacks when targets enter reach.", []),
        new("Resilient", "+1 chosen ability and proficiency in that ability's saving throws.", ["STR", "DEX", "CON", "INT", "WIS", "CHA"]),
        new("Ritual Caster", "Learn two ritual spells.", []),
        new("Savage Attacker", "Roll melee weapon damage dice twice and use the higher result.", []),
        new("Sentinel", "Reactions and advantage on opportunity attacks to control nearby enemies.", []),
        new("Sharpshooter", "Ignore low-ground penalty; optional -5 ranged attack for +10 damage.", []),
        new("Shield Master", "+2 Dexterity saves while using a shield and a defensive reaction.", []),
        new("Skilled", "Gain proficiency in three skills.", []),
        new("Spell Sniper", "Learn a cantrip and reduce spell-attack critical threshold by 1.", []),
        new("Tavern Brawler", "+1 Strength or Constitution; double Strength modifier for unarmed, improvised and thrown attacks.", ["STR", "CON"]),
        new("Tough", "+2 maximum HP per total character level.", []),
        new("War Caster", "Advantage on Concentration saves and Shocking Grasp opportunity reaction.", []),
        new("Weapon Master", "+1 Strength or Dexterity and proficiency with four weapons.", ["STR", "DEX"])
    ];

    public static readonly BuffDefinition[] Buffs =
    [
        new("Mage Armour", "Base AC becomes 13 + Dexterity while no Armour is worn; shields are allowed."),
        new("Shield", "+5 AC until the start of the next turn after spending the reaction."),
        new("Shield of Faith", "+2 AC while Concentrating.", true),
        new("Blur", "Attack rolls against you have disadvantage while Concentrating.", true),
        new("Haste", "+2 AC, advantage on Dexterity saves and +9 m movement while Concentrating.", true),
        new("Barkskin", "AC cannot be lower than 16 while Concentrating.", true),
        new("Mirror Image (3 images)", "+9 AC while all three illusory duplicates remain; one image is lost when an attack misses you."),
        new("Mirror Image (2 images)", "+6 AC while two illusory duplicates remain."),
        new("Mirror Image (1 image)", "+3 AC while one illusory duplicate remains."),
        new("Greater Invisibility", "Your attacks have advantage and enemy attacks have disadvantage while Concentrating; actions can reveal you.", true),
        new("Invisibility", "Enemy attacks have disadvantage while Concentrating; normally ends after attacking, casting or interacting.", true),
        new("Sanctuary", "Direct attacks and hostile targeted spells cannot select you until you attack or harm a creature."),
        new("Longstrider", "+3 m movement until Long Rest."),
        new("Warding Bond", "+1 AC and saves plus resistance to all damage; bonded caster shares damage."),
        new("Heroes' Feast", "+12 maximum HP and advantage on Wisdom saves until Long Rest."),
        new("Aid (level 2)", "+5 maximum HP until Long Rest."),
        new("Aid (level 3)", "+10 maximum HP until Long Rest."),
        new("Aid (level 4)", "+15 maximum HP until Long Rest."),
        new("Aid (level 5)", "+20 maximum HP until Long Rest."),
        new("Aid (level 6)", "+25 maximum HP until Long Rest."),
        new("Magic Weapon +1", "+1 weapon attack and damage while Concentrating.", true),
        new("Magic Weapon +2", "+2 weapon attack and damage while Concentrating (level 4-5 slot).", true),
        new("Magic Weapon +3", "+3 weapon attack and damage while Concentrating (level 6 slot).", true),
        new("Bless", "+1d4 to attack rolls and saving throws while Concentrating; displayed as a roll range.", true),
        new("Resistance", "+1d4 to saving throws while Concentrating; calculated from the exact d20+d4 distribution.", true),
        new("Blade Ward", "Resistance to Bludgeoning, Piercing and Slashing damage for 2 turns."),
        new("Stoneskin", "Resistance to non-magical Bludgeoning, Piercing and Slashing damage while Concentrating.", true),
        new("Protection from Energy: Acid", "Resistance to Acid damage while Concentrating.", true),
        new("Protection from Energy: Cold", "Resistance to Cold damage while Concentrating.", true),
        new("Protection from Energy: Fire", "Resistance to Fire damage while Concentrating.", true),
        new("Protection from Energy: Lightning", "Resistance to Lightning damage while Concentrating.", true),
        new("Protection from Energy: Thunder", "Resistance to Thunder damage while Concentrating.", true),
        new("Protection from Evil and Good", "Aberrations, celestials, elementals, fey, fiends and undead attack with disadvantage.", true),
        new("Defensive Duellist reaction", "Use the feat reaction to add proficiency to AC for one melee attack."),
        new("Great Weapon Master: All In", "Use the feat toggle: -5 attack, +10 damage with a qualifying two-handed melee weapon."),
        new("Sharpshooter: All In", "Use the feat toggle: -5 attack, +10 damage with a ranged weapon."),
        new("Lucky: attack advantage", "Spend a Luck Point to gain advantage on the next attack."),
        new("Rage", "+2 melee/unarmed damage; requires Barbarian level and no Heavy Armour."),
        new("Reckless Attack", "Gain melee attack advantage; enemies gain attack advantage until your next turn. Requires Barbarian level 2."),
        new("Patient Defence", "Enemy attacks have disadvantage until your next turn. Requires Monk level 2 and Ki."),
        new("Danger Sense", "Advantage on visible Dexterity-save effects. Requires Barbarian level 2 and no incapacitation."),
        new("Indomitable", "Reroll one failed saving throw. Requires Fighter level 9; toggle for the next benchmark save."),
        new("Paladin Aura active", "Aura of Protection adds Charisma modifier to saves. Requires Paladin level 6 and consciousness."),
        new("Champion: Improved Critical Hit", "Toggle only for a Champion Fighter of level 3 or higher. Reduces the critical-hit threshold by 1."),
        new("Elixir of Viciousness", "Reduces the critical-hit threshold by 1 until Long Rest."),
        new("Loviatar's Love active (30% HP or less)", "+2 attack rolls and Wisdom saves while the permanent passive's HP condition is met."),
        new("BOOOAL target is Bleeding", "Advantage on attack rolls against a Bleeding target when BOOOAL's Benediction is owned."),
        new("Paid the Price: attacking a Hag", "Disadvantage on attacks against Hags when Paid the Price is owned.")
    ];

    public static int FeatSlotCount(CharacterState state)
    {
        var slots = CharacterCalculator.Classes.Sum(className =>
        {
            var level = state.GetClassLevel(className);
            return (level >= 4 ? 1 : 0) + (level >= 8 ? 1 : 0) + (level >= 12 ? 1 : 0);
        });
        if (state.GetClassLevel("Fighter") >= 6) slots++;
        if (state.GetClassLevel("Rogue") >= 10) slots++;
        return Math.Min(4, slots);
    }

    public static bool FightingStyleAvailable(CharacterState state, string slotKey)
    {
        var slot = FightingStyleSlots.FirstOrDefault(slot => slot.Key.Equals(slotKey, StringComparison.OrdinalIgnoreCase));
        return !string.IsNullOrWhiteSpace(slot.ClassName) && state.GetClassLevel(slot.ClassName) >= slot.MinimumLevel;
    }

    public static FeatDefinition? FindFeat(string name) =>
        Feats.FirstOrDefault(feat => feat.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    public static BuffDefinition? FindBuff(string name) =>
        Buffs.FirstOrDefault(buff => buff.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    public static int SubclassLevel(string className) => className switch
    {
        "Cleric" or "Sorcerer" or "Warlock" => 1,
        "Druid" or "Wizard" => 2,
        _ => 3
    };

    public static bool IsClassOption(string buffName) =>
        ClassOptions.Any(option => option.BuffName.Equals(buffName, StringComparison.OrdinalIgnoreCase));

    public static ClassOptionDefinition[] AvailableClassOptions(CharacterState state) =>
        ClassOptions.Where(option =>
                state.GetClassLevel(option.ClassName) >= option.MinimumLevel
                && (string.IsNullOrWhiteSpace(option.SubclassName)
                    || option.SubclassName.Equals(state.GetSubclass(option.ClassName), StringComparison.OrdinalIgnoreCase)))
            .ToArray();

    private static string[] AbilityImprovementChoices()
    {
        var choices = CharacterCalculator.AbilityNames.Select(ability => $"{ability} +2").ToList();
        for (var first = 0; first < CharacterCalculator.AbilityNames.Length; first++)
        for (var second = first + 1; second < CharacterCalculator.AbilityNames.Length; second++)
            choices.Add($"{CharacterCalculator.AbilityNames[first]} +1 / {CharacterCalculator.AbilityNames[second]} +1");
        return [.. choices];
    }
}
