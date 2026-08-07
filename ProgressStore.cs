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

    public AppProgressState LoadState()
    {
        if (!File.Exists(_path))
            return new AppProgressState();
        try
        {
            var json = File.ReadAllText(_path);
            var state = JsonSerializer.Deserialize<ProgressState>(json);
            return new AppProgressState
            {
                FoundKeys = state?.FoundKeys?.ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                Character = state?.Character ?? new CharacterState()
            };
        }
        catch
        {
            return new AppProgressState();
        }
    }

    public HashSet<string> Load() => LoadState().FoundKeys;

    public void Save(IEnumerable<ItemRecord> items, CharacterState? character = null)
    {
        character ??= new CharacterState();
        character.EquippedKeys = items.Where(item => item.Equipped).Select(item => item.ProgressKey).OrderBy(key => key).ToList();
        var state = new ProgressState
        {
            UpdatedUtc = DateTime.UtcNow,
            FoundKeys = items.Where(item => item.Found).Select(item => item.ProgressKey).OrderBy(key => key).ToList(),
            Character = character
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
        public CharacterState Character { get; init; } = new();
    }
}

internal sealed class AppProgressState
{
    public HashSet<string> FoundKeys { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public CharacterState Character { get; init; } = new();
}
