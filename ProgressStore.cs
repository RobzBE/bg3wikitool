using System.Text.Json;
using System.Text.Json.Serialization;

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
            var characters = state?.Characters?.Where(character => character is not null).Take(4).ToList() ?? [];
            if (characters.Count == 0 && state?.Character is not null)
                characters.Add(state.Character);
            NormalizeCharacters(characters);
            return new AppProgressState
            {
                FoundKeys = state?.FoundKeys?.ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                Characters = characters,
                ActiveCharacterIndex = Math.Clamp(state?.ActiveCharacterIndex ?? 0, 0, 3)
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
        Save(items, [character], 0);
    }

    public void Save(IEnumerable<ItemRecord> items, IReadOnlyList<CharacterState> characters, int activeCharacterIndex)
    {
        var normalizedCharacters = characters.Take(4).ToList();
        NormalizeCharacters(normalizedCharacters);
        var activeIndex = Math.Clamp(activeCharacterIndex, 0, 3);
        normalizedCharacters[activeIndex].EquippedKeys = items.Where(item => item.Equipped).Select(item => item.ProgressKey).OrderBy(key => key).ToList();
        var state = new ProgressState
        {
            UpdatedUtc = DateTime.UtcNow,
            FoundKeys = items.Where(item => item.Found).Select(item => item.ProgressKey).OrderBy(key => key).ToList(),
            Characters = normalizedCharacters,
            ActiveCharacterIndex = activeIndex
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
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public CharacterState? Character { get; init; }
        public List<CharacterState>? Characters { get; init; }
        public int ActiveCharacterIndex { get; init; }
    }

    private static void NormalizeCharacters(List<CharacterState> characters)
    {
        while (characters.Count < 4)
            characters.Add(new CharacterState());
        if (characters.Count > 4)
            characters.RemoveRange(4, characters.Count - 4);
        for (var index = 0; index < characters.Count; index++)
        {
            characters[index] ??= new CharacterState();
            if (string.IsNullOrWhiteSpace(characters[index].Name) || characters[index].Name.Equals("Character", StringComparison.OrdinalIgnoreCase))
                characters[index].Name = $"Character {index + 1}";
            characters[index].NormalizeClassLevels(!characters[index].Difficulty.Equals("Explorer", StringComparison.OrdinalIgnoreCase));
        }
    }
}

internal sealed class AppProgressState
{
    public HashSet<string> FoundKeys { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public List<CharacterState> Characters { get; init; } = [
        new() { Name = "Character 1" }, new() { Name = "Character 2" },
        new() { Name = "Character 3" }, new() { Name = "Character 4" }
    ];
    public int ActiveCharacterIndex { get; init; }
    public CharacterState Character => Characters[Math.Clamp(ActiveCharacterIndex, 0, Characters.Count - 1)];
}
