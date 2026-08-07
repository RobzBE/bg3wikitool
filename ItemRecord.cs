using System.Text.Json.Serialization;

namespace BG3ItemExplorer;

public sealed class ItemRecord
{
    public int SourceRow { get; init; }
    public string Act { get; init; } = "";
    public string Name { get; init; } = "";
    public string Rarity { get; init; } = "";
    public string Type { get; init; } = "";
    public string Properties { get; init; } = "";
    public string ActArea { get; init; } = "";
    public string Location { get; init; } = "";
    public string Description { get; init; } = "";
    public string ImageKey { get; init; } = "";
    public Dictionary<string, string> Links { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> Notes { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonIgnore]
    public bool Found { get; set; }

    [JsonIgnore]
    public bool Equipped { get; set; }

    [JsonIgnore]
    public string ProgressKey => $"{Act}|{SourceRow}|{Name}";

    [JsonIgnore]
    public string NotesText => Notes.Count == 0
        ? ""
        : string.Join(Environment.NewLine + Environment.NewLine,
            Notes.Select(pair => $"{pair.Key}: {pair.Value}"));

    [JsonIgnore]
    public string SearchText => string.Join('\u001f', new[]
    {
        Act, Name, Rarity, Type, Properties, ActArea, Location, Description, NotesText,
        Found ? "gevonden opgehaald" : "nog zoeken",
        Equipped ? "equipped gedragen uitgerust" : ""
    });
}
