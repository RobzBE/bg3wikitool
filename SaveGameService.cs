using System.Text;
using System.Text.Json;
using LSLib.LS;

namespace BG3ItemExplorer;

internal sealed class SaveLinkState
{
    public string WatchDirectory { get; set; } = "";
    public string LinkedSavePath { get; set; } = "";
    public bool AutoSync { get; set; } = true;
    public DateTime? LastImportedWriteUtc { get; set; }
}

internal sealed class SaveCharacterSnapshot
{
    public string Name { get; set; } = "";
    public string Race { get; set; } = "";
    public string StartingClass { get; set; } = "";
    public string Subclass { get; set; } = "";
    public int? Level { get; set; }
    public bool IsMulticlass { get; set; }
    public Dictionary<string, int> ClassLevels { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> Abilities { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> EquippedKeys { get; } = [];
}

internal sealed class SaveImportResult
{
    public string SavePath { get; init; } = "";
    public string SaveName { get; init; } = "";
    public DateTime WriteUtc { get; init; }
    public List<SaveCharacterSnapshot> Characters { get; } = [];
    public List<string> Warnings { get; } = [];
    public int MatchedItems { get; set; }
}

internal static class SaveGameService
{
    public static string DefaultStoryDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Larian Studios", "Baldur's Gate 3", "PlayerProfiles", "Public", "Savegames", "Story");

    public static string? FindNewestSupportedSave(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return null;

        try
        {
            return Directory.EnumerateFiles(directory, "*.lsv", SearchOption.AllDirectories)
                .Where(path => !IsAutoSave(path))
                .Select(path => new FileInfo(path))
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .ThenByDescending(file => file.Length)
                .Select(file => file.FullName)
                .FirstOrDefault();
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static string FindWatchDirectory(string savePath)
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(savePath))!);
        for (var current = directory; current is not null; current = current.Parent)
        {
            if (current.Name.Equals("Story", StringComparison.OrdinalIgnoreCase))
                return current.FullName;
        }
        return directory.Parent?.FullName ?? directory.FullName;
    }

    public static string SaveKind(string path)
    {
        var text = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        return text.Contains("QuickSave_", StringComparison.OrdinalIgnoreCase) ? "QuickSave" : "Manual";
    }

    public static async Task<SaveImportResult> ImportAsync(string savePath, IReadOnlyList<ItemRecord> items, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(savePath))
            throw new FileNotFoundException("The linked BG3 save no longer exists.", savePath);

        var info = new FileInfo(savePath);
        var result = new SaveImportResult
        {
            SavePath = info.FullName,
            SaveName = Path.GetFileNameWithoutExtension(info.Name),
            WriteUtc = info.LastWriteTimeUtc
        };

        var tempPath = Path.Combine(Path.GetTempPath(), "BG3ItemExplorer-save-" + Guid.NewGuid().ToString("N") + ".lsv");
        try
        {
            await CopyStableAsync(savePath, tempPath, cancellationToken);
            using var package = new PackageReader().Read(tempPath);
            var metadata = package.Files.FirstOrDefault(file =>
                Path.GetFileName(file.Name).Equals("SaveInfo.json", StringComparison.OrdinalIgnoreCase) ||
                Path.GetFileName(file.Name).Equals("Info.json", StringComparison.OrdinalIgnoreCase));
            if (metadata is not null)
            {
                using var stream = metadata.CreateContentReader();
                using var reader = new StreamReader(stream, Encoding.UTF8, true);
                ParseSaveInfo(await reader.ReadToEndAsync(cancellationToken), result);
            }
            else
            {
                var sidecar = FindSidecar(savePath, "SaveInfo.json", "info.json");
                if (sidecar is not null)
                    ParseSaveInfo(await File.ReadAllTextAsync(sidecar, cancellationToken), result);
            }

            var meta = package.Files.FirstOrDefault(file => Path.GetFileName(file.Name).Equals("meta.lsf", StringComparison.OrdinalIgnoreCase));
            if (meta is not null)
            {
                using var stream = meta.CreateContentReader();
                using var memory = new MemoryStream();
                await stream.CopyToAsync(memory, cancellationToken);
                memory.Position = 0;
                using var lsf = new LSFReader(memory);
                ApplyLeaderName(lsf.Read(), result);
            }

            var globals = package.Files.FirstOrDefault(file => Path.GetFileName(file.Name).Equals("Globals.lsf", StringComparison.OrdinalIgnoreCase));
            if (globals is not null)
            {
                using var stream = globals.CreateContentReader();
                using var memory = new MemoryStream();
                await stream.CopyToAsync(memory, cancellationToken);
                memory.Position = 0;
                using var lsf = new LSFReader(memory);
                ParseGlobals(lsf.Read(), result, items);
            }
            else
            {
                result.Warnings.Add("Globals.lsf was not present in the save package.");
            }
        }
        finally
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
        }

        if (result.Characters.Count == 0)
            result.Characters.Add(new SaveCharacterSnapshot { Name = result.SaveName });
        return result;
    }

    public static bool MergeInto(CharacterState target, SaveCharacterSnapshot source)
    {
        var changed = false;
        if (!string.IsNullOrWhiteSpace(source.Name) && !target.Name.Equals(source.Name, StringComparison.Ordinal))
        {
            target.Name = source.Name.Trim();
            changed = true;
        }
        if (CharacterCalculator.Races.Contains(source.Race, StringComparer.OrdinalIgnoreCase))
        {
            target.Race = CharacterCalculator.Races.First(value => value.Equals(source.Race, StringComparison.OrdinalIgnoreCase));
            changed = true;
        }
        if (CharacterCalculator.Classes.Contains(source.StartingClass, StringComparer.OrdinalIgnoreCase))
        {
            target.ClassName = CharacterCalculator.Classes.First(value => value.Equals(source.StartingClass, StringComparison.OrdinalIgnoreCase));
            changed = true;
        }
        if (source.Level is >= 1 and <= 12)
        {
            target.Level = source.Level.Value;
            changed = true;
        }
        if (source.ClassLevels.Count > 0 && source.ClassLevels.Values.Sum() is >= 1 and <= 12)
        {
            target.ClassLevels = source.ClassLevels.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
            target.Level = target.ClassLevels.Values.Sum();
            changed = true;
        }
        else if (!source.IsMulticlass && source.Level is >= 1 and <= 12 && !string.IsNullOrWhiteSpace(source.StartingClass))
        {
            target.ClassLevels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { [target.ClassName] = source.Level.Value };
        }
        if (!string.IsNullOrWhiteSpace(source.Subclass))
        {
            target.SubclassName = source.Subclass;
            changed = true;
        }
        foreach (var pair in source.Abilities.Where(pair => pair.Value is >= 3 and <= 30))
        {
            target.SetAbility(pair.Key, pair.Value);
            changed = true;
        }
        if (source.EquippedKeys.Count > 0)
        {
            target.EquippedKeys = source.EquippedKeys.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            changed = true;
        }
        target.NormalizeClassLevels(!target.Difficulty.Equals("Explorer", StringComparison.OrdinalIgnoreCase));
        return changed;
    }

    private static async Task CopyStableAsync(string source, string destination, CancellationToken cancellationToken)
    {
        Exception? lastError = null;
        for (var attempt = 0; attempt < 6; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1024 * 128, true);
                await using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 128, true);
                await input.CopyToAsync(output, cancellationToken);
                return;
            }
            catch (IOException exception)
            {
                lastError = exception;
                await Task.Delay(350 * (attempt + 1), cancellationToken);
            }
        }
        throw new IOException("The BG3 save was still being written or synchronized and could not yet be read.", lastError);
    }

    private static void ParseSaveInfo(string json, SaveImportResult result)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var partyLevel = FindFirstInteger(document.RootElement, "PartyLevel", "Level");
            var party = FindFirstProperty(document.RootElement, "Active Party") ?? FindFirstProperty(document.RootElement, "Party");
            var partyCharacters = party is { ValueKind: JsonValueKind.Object }
                ? FindFirstProperty(party.Value, "Characters")
                : party;
            if (partyCharacters is { ValueKind: JsonValueKind.Array })
            {
                foreach (var member in partyCharacters.Value.EnumerateArray().Take(4))
                {
                    var snapshot = SnapshotFromJson(member, partyLevel);
                    if (!string.IsNullOrWhiteSpace(snapshot.Name))
                        result.Characters.Add(snapshot);
                }
            }
            if (result.Characters.Count == 0)
            {
                var snapshot = SnapshotFromJson(document.RootElement, partyLevel);
                if (!string.IsNullOrWhiteSpace(snapshot.Name) || snapshot.Level.HasValue)
                    result.Characters.Add(snapshot);
            }
        }
        catch (JsonException exception)
        {
            result.Warnings.Add("SaveInfo.json could not be parsed: " + exception.Message);
        }
    }

    private static SaveCharacterSnapshot SnapshotFromJson(JsonElement element, int? fallbackLevel)
    {
        var snapshot = new SaveCharacterSnapshot
        {
            Name = FindFirstString(element, "Name", "PlayerName", "CharacterName", "Origin") ?? "",
            Race = CanonicalMatch(FindFirstString(element, "Race", "RaceName"), CharacterCalculator.Races),
            StartingClass = CanonicalMatch(FindFirstString(element, "Class", "ClassName", "Class Type"), CharacterCalculator.Classes),
            Subclass = FindFirstString(element, "Subclass", "SubclassName") ?? "",
            Level = FindFirstInteger(element, "Level", "CharacterLevel") ?? fallbackLevel
        };
        var classes = FindFirstProperty(element, "Classes");
        if (classes is { ValueKind: JsonValueKind.Array })
        {
            var parsedClasses = new List<(string Main, string Sub, int? Level)>();
            foreach (var classEntry in classes.Value.EnumerateArray())
            {
                var name = CanonicalMatch(FindFirstString(classEntry, "Main", "Name", "Class", "ClassName"), CharacterCalculator.Classes);
                var level = FindFirstInteger(classEntry, "Level", "ClassLevel");
                var subclass = FindFirstString(classEntry, "Sub", "Subclass", "SubclassName") ?? "";
                if (!string.IsNullOrWhiteSpace(name))
                    parsedClasses.Add((name, subclass, level));
                if (!string.IsNullOrWhiteSpace(name) && level is >= 1 and <= 12)
                    snapshot.ClassLevels[name] = level.Value;
                if (string.IsNullOrWhiteSpace(snapshot.Subclass))
                    snapshot.Subclass = subclass;
            }
            snapshot.IsMulticlass = parsedClasses.Select(value => value.Main).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1;
            if (snapshot.ClassLevels.Count > 0)
            {
                snapshot.StartingClass = snapshot.ClassLevels.Keys.First();
                snapshot.Level = snapshot.ClassLevels.Values.Sum();
            }
            else if (parsedClasses.Count > 0)
            {
                snapshot.StartingClass = parsedClasses[0].Main;
                snapshot.Subclass = parsedClasses.Select(value => value.Sub).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? snapshot.Subclass;
                if (!snapshot.IsMulticlass && snapshot.Level is >= 1 and <= 12)
                    snapshot.ClassLevels[snapshot.StartingClass] = snapshot.Level.Value;
            }
        }
        return snapshot;
    }

    private static void ApplyLeaderName(Resource resource, SaveImportResult result)
    {
        var strings = new List<(string Key, string Value, string Path)>();
        var numeric = new List<(string Key, int Value, string Path)>();
        foreach (var region in resource.Regions)
            Collect(region.Value, region.Key, strings, numeric);
        var leader = strings.FirstOrDefault(value => value.Key.Equals("LeaderName", StringComparison.OrdinalIgnoreCase)).Value;
        if (string.IsNullOrWhiteSpace(leader))
            return;
        var player = result.Characters.FirstOrDefault(character =>
            character.Name.Equals("Generic", StringComparison.OrdinalIgnoreCase) ||
            character.Name.Equals("Custom", StringComparison.OrdinalIgnoreCase) ||
            character.Name.Equals("DarkUrge", StringComparison.OrdinalIgnoreCase));
        if (player is not null)
            player.Name = leader;
    }

    private static void ParseGlobals(Resource resource, SaveImportResult result, IReadOnlyList<ItemRecord> items)
    {
        var strings = new List<(string Key, string Value, string Path)>();
        var numeric = new List<(string Key, int Value, string Path)>();
        foreach (var region in resource.Regions)
            Collect(region.Value, region.Key, strings, numeric);

        var primary = result.Characters.FirstOrDefault() ?? new SaveCharacterSnapshot { Name = result.SaveName };
        if (result.Characters.Count == 0)
            result.Characters.Add(primary);

        primary.Race = FirstCanonical(strings, CharacterCalculator.Races, "Race", "RaceName", "PlayerRace") ?? primary.Race;
        primary.StartingClass = FirstCanonical(strings, CharacterCalculator.Classes, "Class", "ClassName", "CharacterClass") ?? primary.StartingClass;
        primary.Subclass = FirstCanonical(strings, BuildOptions.SubclassesByClass.Values.SelectMany(value => value).ToArray(), "Subclass", "SubclassName") ?? primary.Subclass;
        primary.Level ??= FirstNumber(numeric, 1, 12, "Level", "CharacterLevel", "PartyLevel");

        foreach (var ability in CharacterCalculator.AbilityNames)
        {
            var fullName = ability switch { "STR" => "Strength", "DEX" => "Dexterity", "CON" => "Constitution", "INT" => "Intelligence", "WIS" => "Wisdom", _ => "Charisma" };
            var value = FirstNumber(numeric, 3, 30, fullName, ability, fullName + "Base");
            if (value.HasValue)
                primary.Abilities[ability] = value.Value;
        }

        foreach (var className in CharacterCalculator.Classes)
        {
            var level = strings
                .Where(value => value.Value.Equals(className, StringComparison.OrdinalIgnoreCase))
                .Select(value => FindNearbyNumber(numeric, value.Path, 1, 12, "Level", "ClassLevel"))
                .FirstOrDefault(value => value.HasValue);
            if (level.HasValue)
                primary.ClassLevels[className] = level.Value;
        }

        var itemLookup = items.GroupBy(item => Normalize(item.Name)).ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var equipped = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in strings.Where(value => LooksEquipped(value.Key, value.Path)))
        {
            var normalized = Normalize(value.Value);
            if (itemLookup.TryGetValue(normalized, out var item))
                equipped.Add(item.ProgressKey);
            else
            {
                var match = itemLookup.FirstOrDefault(pair => pair.Key.Length >= 7 && normalized.Contains(pair.Key, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(match.Key))
                    equipped.Add(match.Value.ProgressKey);
            }
        }
        primary.EquippedKeys.AddRange(equipped);
        result.MatchedItems = equipped.Count;
        if (equipped.Count == 0)
            result.Warnings.Add("No equipped item names could be matched; the save may use Patch 8 ECS identifiers that are not display names.");
    }

    private static void Collect(Node node, string path, List<(string Key, string Value, string Path)> strings, List<(string Key, int Value, string Path)> numeric)
    {
        var nodePath = path + "/" + (node.Name ?? "node");
        foreach (var pair in node.Attributes)
        {
            if (pair.Value.Value is string text && !string.IsNullOrWhiteSpace(text))
                strings.Add((pair.Key, text.Trim(), nodePath));
            else if (pair.Value.Value is TranslatedString translated && !string.IsNullOrWhiteSpace(translated.Value))
                strings.Add((pair.Key, translated.Value.Trim(), nodePath));
            else if (TryInteger(pair.Value.Value, out var number))
                numeric.Add((pair.Key, number, nodePath));
        }
        foreach (var children in node.Children.Values)
            foreach (var child in children)
                Collect(child, nodePath, strings, numeric);
    }

    private static bool TryInteger(object? value, out int number)
    {
        try
        {
            if (value is byte or sbyte or short or ushort or int or uint or long or ulong)
            {
                number = Convert.ToInt32(value);
                return true;
            }
        }
        catch (OverflowException) { }
        number = 0;
        return false;
    }

    private static string? FirstCanonical(IEnumerable<(string Key, string Value, string Path)> values, string[] choices, params string[] keys)
    {
        foreach (var value in values.Where(value => keys.Contains(value.Key, StringComparer.OrdinalIgnoreCase)))
        {
            var match = choices.FirstOrDefault(choice => value.Value.Equals(choice, StringComparison.OrdinalIgnoreCase) || value.Value.Contains(choice, StringComparison.OrdinalIgnoreCase));
            if (match is not null) return match;
        }
        return null;
    }

    private static int? FirstNumber(IEnumerable<(string Key, int Value, string Path)> values, int minimum, int maximum, params string[] keys) =>
        values.Where(value => value.Value >= minimum && value.Value <= maximum && keys.Contains(value.Key, StringComparer.OrdinalIgnoreCase)).Select(value => (int?)value.Value).FirstOrDefault();

    private static int? FindNearbyNumber(IEnumerable<(string Key, int Value, string Path)> values, string path, int minimum, int maximum, params string[] keys) =>
        values.Where(value => value.Path.Equals(path, StringComparison.OrdinalIgnoreCase) && value.Value >= minimum && value.Value <= maximum && keys.Contains(value.Key, StringComparer.OrdinalIgnoreCase)).Select(value => (int?)value.Value).FirstOrDefault();

    private static bool LooksEquipped(string key, string path) =>
        key.Contains("Equip", StringComparison.OrdinalIgnoreCase) || key.Contains("Slot", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("Equip", StringComparison.OrdinalIgnoreCase) || path.Contains("Wield", StringComparison.OrdinalIgnoreCase);

    private static bool IsAutoSave(string path) => path.Contains("AutoSave_", StringComparison.OrdinalIgnoreCase);
    private static string Normalize(string value) => new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static string CanonicalMatch(string? value, string[] choices) =>
        choices.FirstOrDefault(choice => choice.Equals(value, StringComparison.OrdinalIgnoreCase)) ?? "";

    private static string? FindSidecar(string savePath, params string[] names)
    {
        var directory = Path.GetDirectoryName(savePath);
        return names.Select(name => Path.Combine(directory!, name)).FirstOrDefault(File.Exists);
    }

    private static JsonElement? FindFirstProperty(JsonElement element, string name)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) return property.Value;
                var nested = FindFirstProperty(property.Value, name);
                if (nested.HasValue) return nested;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) { var nested = FindFirstProperty(item, name); if (nested.HasValue) return nested; }
        return null;
    }

    private static string? FindFirstString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            var value = FindFirstProperty(element, name);
            if (value is { ValueKind: JsonValueKind.String }) return value.Value.GetString();
        }
        return null;
    }

    private static int? FindFirstInteger(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            var value = FindFirstProperty(element, name);
            if (value is { ValueKind: JsonValueKind.Number } && value.Value.TryGetInt32(out var number)) return number;
        }
        return null;
    }
}
