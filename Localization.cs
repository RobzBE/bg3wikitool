namespace BG3ItemExplorer;

internal enum UiLanguage
{
    English,
    Dutch
}

internal static class Localization
{
    public static UiLanguage Current { get; set; } = UiLanguage.English;

    private static readonly Dictionary<string, (string English, string Dutch)> Texts = new()
    {
        ["Subtitle"] = ("Acts 1, 2 & 3 • offline • complete item information • data & images: bg3.wiki", "Act 1, 2 & 3 • offline • volledige iteminformatie • data & afbeeldingen: bg3.wiki"),
        ["FiltersTitle"] = ("SEARCH & FILTER", "ZOEKEN & FILTEREN"),
        ["Language"] = ("Language", "Taal"),
        ["SearchAll"] = ("Search all fields", "Zoek in alle velden"),
        ["SearchPlaceholder"] = ("Name, effect, location...", "Naam, effect, locatie..."),
        ["Acts"] = ("Acts", "Acts"),
        ["Rarity"] = ("Rarity", "Zeldzaamheid"),
        ["Type"] = ("Type", "Type"),
        ["AllTypes"] = ("All types", "Alle types"),
        ["Place"] = ("Area or location", "Gebied of locatie"),
        ["AllPlaces"] = ("All areas/locations", "Alle gebieden/locaties"),
        ["NotesOnly"] = ("Only items with a note", "Alleen items met een notitie"),
        ["Progress"] = ("Progress", "Voortgang"),
        ["AllItems"] = ("All items", "Alle items"),
        ["NotFound"] = ("Still needed", "Nog zoeken"),
        ["Found"] = ("Found", "Gevonden"),
        ["Sorting"] = ("Sort", "Sorteren"),
        ["SortName"] = ("Name", "Naam"),
        ["SortRarity"] = ("Rarity", "Zeldzaamheid"),
        ["SortType"] = ("Type", "Type"),
        ["SortAct"] = ("Act", "Act"),
        ["SortLocation"] = ("Location", "Locatie"),
        ["SortStatus"] = ("Status", "Status"),
        ["Ascending"] = ("A → Z", "A → Z"),
        ["Descending"] = ("Z → A", "Z → A"),
        ["Reset"] = ("Clear filters", "Filters wissen"),
        ["ResultCount"] = ("{0:N0} visible • {1:N0}/{2:N0} found", "{0:N0} zichtbaar • {1:N0}/{2:N0} gevonden"),
        ["NoItems"] = ("No items found", "Geen items gevonden"),
        ["AdjustFilters"] = ("Adjust the filters to show results.", "Pas de filters aan om resultaten te tonen."),
        ["GridFound"] = ("✓", "✓"),
        ["FoundTooltip"] = ("Found / collected", "Gevonden / opgehaald"),
        ["GridEquipped"] = ("⚔", "⚔"),
        ["EquippedTooltip"] = ("Equipped on character", "Gedragen door personage"),
        ["GridName"] = ("Name", "Naam"),
        ["GridRarity"] = ("Rarity", "Zeldzaamheid"),
        ["GridType"] = ("Type", "Type"),
        ["GridProperties"] = ("Properties", "Eigenschappen"),
        ["GridActArea"] = ("Act Area", "Actgebied"),
        ["GridLocation"] = ("Location", "Locatie"),
        ["GridDescription"] = ("Description", "Beschrijving"),
        ["GridNotes"] = ("Notes", "Notities"),
        ["Properties"] = ("PROPERTIES", "EIGENSCHAPPEN"),
        ["ActArea"] = ("ACT AREA", "ACTGEBIED"),
        ["Location"] = ("LOCATION", "LOCATIE"),
        ["Description"] = ("DESCRIPTION", "BESCHRIJVING"),
        ["Notes"] = ("NOTES", "NOTITIES"),
        ["MarkFound"] = ("Mark as found", "Markeer als gevonden"),
        ["FoundButton"] = ("✓ Found", "✓ Gevonden"),
        ["OpenLink"] = ("Open {0}", "Open {0}"),
        ["NoLink"] = ("No external source link for this item.", "Geen externe bronlink voor dit item."),
        ["Source"] = ("Data & images: bg3.wiki", "Data & afbeeldingen: bg3.wiki"),
        ["Footer"] = ("Non-commercial fan tool • progress is saved next to the exe", "Niet-commerciële fan-tool • voortgang wordt naast de exe opgeslagen"),
        ["ProgressError"] = ("Progress could not be saved next to the exe. Check whether the USB folder is writable.\n\n{0}", "De voortgang kon niet naast de exe worden opgeslagen. Controleer of de USB-map schrijfbaar is.\n\n{0}"),
        ["LinkError"] = ("The link could not be opened.\n\n{0}", "De link kon niet worden geopend.\n\n{0}"),
        ["LoadError"] = ("BG3 Item Explorer encountered an unexpected error.\n\n{0}\n\nDiagnostic log: {1}", "BG3 Item Explorer kreeg een onverwachte fout.\n\n{0}\n\nDiagnostisch logboek: {1}"),
        ["WarningTitle"] = ("BG3 Item Explorer", "BG3 Item Explorer"),
        ["CharacterSheet"] = ("CHARACTER SHEET", "CHARACTER SHEET"),
        ["Identity"] = ("BUILD", "BUILD"),
        ["Race"] = ("Race", "Ras"),
        ["Class"] = ("Class", "Klasse"),
        ["Level"] = ("Level", "Level"),
        ["Difficulty"] = ("Difficulty", "Moeilijkheid"),
        ["DifficultyExplorer"] = ("Explorer: party HP ×2; every creature gets +2 proficiency; no multiclassing.", "Explorer: party-HP ×2; elk wezen krijgt +2 proficiency; geen multiclassing."),
        ["DifficultyBalanced"] = ("Balanced: standard rules and baseline values.", "Balanced: standaardregels en baselinewaarden."),
        ["DifficultyTactician"] = ("Tactician: enemies gain +2 attack and save DC; tougher tactics; long rest costs 80 supplies.", "Tactician: vijanden krijgen +2 attack en save DC; betere tactieken; long rest kost 80 supplies."),
        ["DifficultyHonour"] = ("Honour: Tactician combat bonuses plus Legendary Actions and a single-save campaign.", "Honour: Tactician-combatbonussen plus Legendary Actions en één savebestand."),
        ["BaseAbilities"] = ("LEVEL 1 ABILITY SCORES", "ABILITY SCORES OP LEVEL 1"),
        ["Offense"] = ("OFFENSE", "AANVAL"),
        ["Defense"] = ("DEFENSE", "VERDEDIGING"),
        ["EnemyHitChance"] = ("ENEMY SUCCESS CHANCE", "KANS DAT VIJAND SLAAGT"),
        ["EquippedGear"] = ("EQUIPPED GEAR", "GEDRAGEN UITRUSTING"),
        ["ActiveConditions"] = ("ACTIVE CONDITIONAL EFFECTS", "ACTIEVE VOORWAARDELIJKE EFFECTEN"),
        ["BenchmarkNote"] = ("Baseline per act. Attack uses AC; spell attack uses AC; spell effect uses the average failure chance of DEX/CON/WIS saves. Toggle conditional item effects below.", "Baseline per act. Attack gebruikt AC; spell attack gebruikt AC; spell effect gebruikt de gemiddelde faalkans van DEX/CON/WIS saves. Schakel voorwaardelijke itemeffecten hieronder in of uit."),
        ["OffenseLine"] = ("Weapon {0} ({1}) • Spell attack {2} ({3}) • Proficiency +{4}", "Wapen {0} ({1}) • Spell attack {2} ({3}) • Proficiency +{4}"),
        ["DefenseLine"] = ("HP {0} • Initiative {1} • Movement {2:0.#} m", "HP {0} • Initiative {1} • Beweging {2:0.#} m"),
        ["SavingThrows"] = ("Saving throws", "Saving throws"),
        ["ThreatLine"] = ("Attack {0}% • spell attack {1}%\nSpell effect {2}%", "Attack {0}% • spell attack {1}%\nSpell effect {2}%"),
        ["NoGearEquipped"] = ("No gear equipped", "Geen uitrusting gedragen"),
        ["NoConditionalEffects"] = ("No conditional effects", "Geen voorwaardelijke effecten"),
        ["NoCriticalHits"] = ("critical-hit immune", "immuun voor critical hits"),
        ["DamageReduction"] = ("damage reduction {0}", "damage reduction {0}"),
        ["Resistances"] = ("resistance: {0}", "resistance: {0}"),
        ["NonProficientGear"] = ("not proficient: {0} (attack rolls and saves: DIS; spellcasting blocked)", "niet proficient: {0} (attack rolls en saves: DIS; spellcasting geblokkeerd)")
    };

    public static string T(string key) =>
        Texts.TryGetValue(key, out var value)
            ? Current == UiLanguage.English ? value.English : value.Dutch
            : key;

    public static string Format(string key, params object[] values) => string.Format(T(key), values);
}
