namespace BG3ItemExplorer;

internal sealed class PermanentBonusSelection
{
    public string Name { get; set; } = "";
    public string Choice { get; set; } = "";
}

internal sealed record PermanentBonusDefinition(
    string Name,
    string Act,
    string Description,
    string[] Choices);

internal static class PermanentBonusCatalog
{
    private static readonly string[] Abilities = ["STR", "DEX", "CON", "INT", "WIS", "CHA"];

    public static readonly PermanentBonusDefinition[] All =
    [
        new("Auntie Ethel's Hair", "ACT 1", "+1 to the selected ability score; can raise it above 20.", Abilities),
        new("Awakened", "ACT 1", "Illithid powers can be used as Bonus Actions.", []),
        new("Zaith'isk Penalty: Intelligence", "ACT 1", "-2 Intelligence after failing the first Zaith'isk save; removed by consuming tadpoles or ceremorphosis.", []),
        new("Zaith'isk Penalty: Wisdom", "ACT 1", "-2 Wisdom after failing the second Zaith'isk save; removed by consuming tadpoles or ceremorphosis.", []),
        new("Zaith'isk Penalty: Constitution", "ACT 1", "-2 Constitution after failing the third Zaith'isk save; removed by consuming tadpoles or ceremorphosis.", []),
        new("BOOOAL's Benediction", "ACT 1", "Advantage on attack rolls against Bleeding targets.", []),
        new("Brand of the Absolute", "ACT 1", "Enables the benefits of Absolute equipment and related dialogue.", []),
        new("Find Familiar: Scratch", "ACT 1", "Can summon Scratch outside camp.", []),
        new("Find Familiar: Cheeky Quasit", "ACT 1", "Can summon Shovel, Basket, or Fork.", []),
        new("Instrument Proficiency", "ACT 1", "Can play musical instruments and entertain crowds.", []),
        new("Loviatar's Love", "ACT 1", "+2 attack rolls and Wisdom saves while at 30% HP or less; lost on death.", []),
        new("Forbidden Knowledge", "ACT 1", "+1 Wisdom saving throws and ability checks; grants Speak with Dead once per Long Rest.", []),
        new("Paid the Price", "ACT 1", "+1 Intimidation, Perception disadvantage, and disadvantage attacking Hags; exclusive with Volo's Ersatz Eye.", []),
        new("Survival Instinct", "ACT 1", "Can use the unique Survival Instinct illithid power.", []),
        new("Volo's Ersatz Eye", "ACT 1", "Permanent See Invisibility; exclusive with Paid the Price.", []),

        new("Arabella's Shadow Entangle", "ACT 2", "Can entangle an Undead or Shadow creature.", []),
        new("Improved Bardic Inspiration", "ACT 2", "One separate 1d12 Bardic Inspiration use per Long Rest.", []),
        new("Githzerai Mind Barrier", "ACT 2", "Advantage on Intelligence saving throws; normally lost on death.", []),
        new("Consumed Shadow Weave", "ACT 2", "Gale origin gains one separate level 3 Shadow Spell Slot.", []),
        new("Potion of Everlasting Vigour", "ACT 2", "+2 Strength and can raise Strength above 20.", []),
        new("Slayer Form", "ACT 2", "Dark Urge can transform into the Slayer once per Long Rest.", []),
        new("Summon Us", "ACT 2", "Can summon Us as an intellect devourer familiar.", []),

        new("Anointed in Splendour", "ACT 3", "+2 to all saving throws.", []),
        new("Partial Ceremorphosis", "ACT 3", "Unlocks tier 3 illithid powers, Fly, and all tier 1 powers.", []),
        new("Danse Macabre", "ACT 3", "Can summon four ghouls; its removable Tharchiate Withering curse is listed separately.", []),
        new("Tharchiate Withering", "ACT 3", "-5 Constitution until Remove Curse is used; Danse Macabre remains afterwards.", []),
        new("Monk's Hideous Laughter", "ACT 3", "Can cast Tasha's Hideous Laughter once per Long Rest.", []),
        new("Mirror of Loss", "ACT 3", "+2 to the selected ability score, up to 24.", Abilities),
        new("Patriar's Memory", "ACT 3", "+1 Charisma from the Mirror of Loss, up to 24.", []),
        new("Slayer Knowledge", "ACT 3", "Advantage against Slayer abilities and a Dark Urge interaction.", []),
        new("Sweet Stone Features", "ACT 3", "+1d4 to attack rolls and saving throws after a Long Rest.", []),
        new("The Tharchiate Codex: Blessing", "ACT 3", "Gain 20 temporary hit points after each Long Rest.", []),
        new("Unstable Blood", "ACT 3", "Blood surfaces created by the character explode on contact with fire.", []),
        new("Vampire", "ACT 3", "Gain Bite as a Vampire Spawn and access to Circle of Bones.", []),
        new("Vampire Ascendant", "ACT 3", "Astarion gains Ascendant Bite, Misty Escape, and +1d10 necrotic weapon/unarmed damage.", [])
    ];

    public static PermanentBonusDefinition? Find(string name) =>
        All.FirstOrDefault(bonus => bonus.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
}
