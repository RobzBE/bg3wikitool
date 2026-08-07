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
        ["LoadError"] = ("The embedded item database could not be loaded.\n\n{0}", "De ingebedde itemdatabase kon niet worden geladen.\n\n{0}"),
        ["WarningTitle"] = ("BG3 Item Explorer", "BG3 Item Explorer")
    };

    public static string T(string key) =>
        Texts.TryGetValue(key, out var value)
            ? Current == UiLanguage.English ? value.English : value.Dutch
            : key;

    public static string Format(string key, params object[] values) => string.Format(T(key), values);
}
