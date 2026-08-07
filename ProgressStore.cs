using System.Text.Json;

namespace BG3ItemExplorer;

internal sealed class ProgressStore
{
    private readonly string _path;

    public ProgressStore(string? baseDirectory = null)
    {
        _path = Path.Combine(baseDirectory ?? AppContext.BaseDirectory, "BG3-Item-Explorer-progress.json");
    }

    public string PathOnDisk => _path;

    public HashSet<string> Load()
    {
        if (!File.Exists(_path))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var json = File.ReadAllText(_path);
            var state = JsonSerializer.Deserialize<ProgressState>(json);
            return state?.FoundKeys?.ToHashSet(StringComparer.OrdinalIgnoreCase)
                   ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public void Save(IEnumerable<ItemRecord> items)
    {
        var state = new ProgressState
        {
            UpdatedUtc = DateTime.UtcNow,
            FoundKeys = items.Where(item => item.Found).Select(item => item.ProgressKey).OrderBy(key => key).ToList()
        };
        var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
        var temporary = _path + ".tmp";
        File.WriteAllText(temporary, json);
        File.Move(temporary, _path, true);
    }

    private sealed class ProgressState
    {
        public DateTime UpdatedUtc { get; init; }
        public List<string> FoundKeys { get; init; } = [];
    }
}
