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
    public string Difficulty { get; set; } = "";
    public int? Level { get; set; }
    public bool IsMulticlass { get; set; }
    public Dictionary<string, int> ClassLevels { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> Subclasses { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> Abilities { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> BaseAbilities { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> EquippedKeys { get; } = [];
    public List<FeatSelection> Feats { get; } = [];
    public Dictionary<string, string> FightingStyles { get; } = new(StringComparer.OrdinalIgnoreCase);
    public bool HasAbilityData { get; set; }
    public bool HasBaseAbilityData { get; set; }
    public bool HasEquipmentData { get; set; }
    public bool HasFeatData { get; set; }
    public bool HasFightingStyleData { get; set; }
}

internal sealed class SaveImportResult
{
    public string SavePath { get; init; } = "";
    public string SaveName { get; init; } = "";
    public DateTime WriteUtc { get; init; }
    public List<SaveCharacterSnapshot> Characters { get; } = [];
    public List<string> Warnings { get; } = [];
    public HashSet<string> PresentKeys { get; } = new(StringComparer.OrdinalIgnoreCase);
    public int MatchedPresentItems { get; set; }
    public int MatchedItems { get; set; }
}

internal sealed record SavedItemReference(string StatsId, ulong Flags, string Level, int TemplateType, Guid? CurrentTemplate)
{
    private const ulong GlobalFlag = 0x04000000;

    public bool IsPresent =>
        TemplateType == 0
        && string.IsNullOrWhiteSpace(Level)
        && (Flags & GlobalFlag) != 0;
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
                var saveInfoJson = await reader.ReadToEndAsync(cancellationToken);
                ParseSaveInfo(saveInfoJson, result);
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
        if (CharacterCalculator.Difficulties.Contains(source.Difficulty, StringComparer.OrdinalIgnoreCase))
        {
            target.Difficulty = CharacterCalculator.Difficulties.First(value => value.Equals(source.Difficulty, StringComparison.OrdinalIgnoreCase));
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
        foreach (var pair in source.Subclasses)
        {
            target.SetSubclass(pair.Key, pair.Value);
            changed = true;
        }
        if (source.Subclasses.Count == 0 && !string.IsNullOrWhiteSpace(source.Subclass))
            target.SetSubclass(target.ClassName, source.Subclass);
        var importedBase = source.HasBaseAbilityData ? source.BaseAbilities : source.Abilities;
        foreach (var pair in importedBase.Where(pair => pair.Value is >= 3 and <= 30))
        {
            target.SetAbility(pair.Key, pair.Value);
            changed = true;
        }
        if (source.HasAbilityData)
        {
            target.ImportedCurrentAbilities = true;
            target.ImportedAbilityTotals = source.Abilities
                .Where(pair => pair.Value is >= 3 and <= 40)
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        }
        if (source.HasEquipmentData)
        {
            target.EquippedKeys = source.EquippedKeys.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            changed = true;
        }
        if (source.HasFeatData)
        {
            target.Feats = source.Feats.Select(feat => new FeatSelection { Name = feat.Name, Choice = feat.Choice }).ToList();
            changed = true;
        }
        if (source.HasFightingStyleData)
        {
            target.FightingStyles = new Dictionary<string, string>(source.FightingStyles, StringComparer.OrdinalIgnoreCase);
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
            var difficulty = ParseDifficulty(FindFirstProperty(document.RootElement, "Difficulty"));
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
                    snapshot.Difficulty = difficulty;
                    if (!string.IsNullOrWhiteSpace(snapshot.Name))
                        result.Characters.Add(snapshot);
                }
            }
            if (result.Characters.Count == 0)
            {
                var snapshot = SnapshotFromJson(document.RootElement, partyLevel);
                snapshot.Difficulty = difficulty;
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
            Race = CanonicalRace(FindFirstString(element, "Race", "RaceName")),
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
                var subclass = CanonicalSubclass(name, FindFirstString(classEntry, "Sub", "Subclass", "SubclassName"));
                if (!string.IsNullOrWhiteSpace(name))
                    parsedClasses.Add((name, subclass, level));
                if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(subclass))
                    snapshot.Subclasses[name] = subclass;
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
                if (snapshot.Level is >= 1 and <= 12)
                {
                    var distinctClasses = parsedClasses
                        .GroupBy(value => value.Main, StringComparer.OrdinalIgnoreCase)
                        .Select(group => group.First())
                        .ToList();
                    var minimumLevels = distinctClasses.ToDictionary(
                        value => value.Main,
                        value => string.IsNullOrWhiteSpace(value.Sub) ? 1 : BuildOptions.SubclassLevel(value.Main),
                        StringComparer.OrdinalIgnoreCase);
                    var assigned = minimumLevels.Values.Sum();
                    if (assigned <= snapshot.Level.Value)
                    {
                        minimumLevels[snapshot.StartingClass] += snapshot.Level.Value - assigned;
                        foreach (var pair in minimumLevels)
                            snapshot.ClassLevels[pair.Key] = pair.Value;
                    }
                }
            }
        }
        if (!string.IsNullOrWhiteSpace(snapshot.StartingClass))
        {
            snapshot.Subclass = snapshot.Subclasses.GetValueOrDefault(snapshot.StartingClass,
                CanonicalSubclass(snapshot.StartingClass, snapshot.Subclass));
            if (!string.IsNullOrWhiteSpace(snapshot.Subclass))
                snapshot.Subclasses[snapshot.StartingClass] = snapshot.Subclass;
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
        var canonicalPrimarySubclass = CanonicalSubclass(primary.StartingClass, primary.Subclass);
        if (!string.IsNullOrWhiteSpace(canonicalPrimarySubclass))
            primary.Subclasses[primary.StartingClass] = canonicalPrimarySubclass;
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

        var savedItems = new List<SavedItemReference>();
        if (resource.Regions.TryGetValue("Items", out var itemRegion))
            CollectSavedItems(itemRegion, savedItems);
        var itemLookup = items
            .SelectMany(item => item.GameIds.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => (Id: id, Item: item)))
            .GroupBy(pair => pair.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Item, StringComparer.OrdinalIgnoreCase);
        foreach (var savedItem in savedItems.Where(item => item.IsPresent))
        {
            if (itemLookup.TryGetValue(savedItem.StatsId, out var item))
                result.PresentKeys.Add(item.ProgressKey);
        }
        result.MatchedItems = 0;
        if (resource.Regions.TryGetValue("NewAge", out var newAgeRegion)
            && newAgeRegion.Attributes.TryGetValue("NewAge", out var newAgeAttribute)
            && newAgeAttribute.Value is byte[] newAge)
            NewAgeSaveParser.Apply(newAge, result, items, savedItems);
        result.MatchedPresentItems = result.PresentKeys.Count;
        if (savedItems.Count > 0 && result.MatchedPresentItems == 0)
            result.Warnings.Add("Saved item identifiers were found, but none matched this item catalogue.");
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

    private static void CollectSavedItems(Node node, List<SavedItemReference> items)
    {
        var stats = AttributeValue(node, "Stats")?.ToString() ?? "";
        if (!string.IsNullOrWhiteSpace(stats))
        {
            TryUnsignedInteger(AttributeValue(node, "Flags"), out var flags);
            TryInteger(AttributeValue(node, "CurrentTemplateType"), out var templateType);
            var templateText = AttributeValue(node, "CurrentTemplate")?.ToString();
            var currentTemplate = Guid.TryParse(templateText, out var parsedTemplate) ? parsedTemplate : (Guid?)null;
            items.Add(new SavedItemReference(stats.Trim(), flags, AttributeValue(node, "Level")?.ToString() ?? "", templateType, currentTemplate));
        }
        foreach (var children in node.Children.Values)
            foreach (var child in children)
                CollectSavedItems(child, items);
    }

    private static object? AttributeValue(Node node, string key) =>
        node.Attributes.FirstOrDefault(pair => pair.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).Value?.Value;

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

    private static bool TryUnsignedInteger(object? value, out ulong number)
    {
        try
        {
            if (value is byte or sbyte or short or ushort or int or uint or long or ulong)
            {
                number = Convert.ToUInt64(value);
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

    private static bool IsAutoSave(string path) => path.Contains("AutoSave_", StringComparison.OrdinalIgnoreCase);
    private static string NormalizeToken(string value) => new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static string CanonicalMatch(string? value, string[] choices) =>
        choices.FirstOrDefault(choice => choice.Equals(value, StringComparison.OrdinalIgnoreCase)) ?? "";

    private static string CanonicalRace(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";
        var normalized = NormalizeToken(value);
        if (normalized.StartsWith("halfelf", StringComparison.Ordinal)) return "Half-Elf";
        if (normalized.StartsWith("halforc", StringComparison.Ordinal)) return "Half-Orc";
        if (normalized.StartsWith("tiefling", StringComparison.Ordinal)) return "Tiefling";
        if (normalized.StartsWith("dragonborn", StringComparison.Ordinal)) return "Dragonborn";
        if (normalized.StartsWith("drow", StringComparison.Ordinal)) return "Drow";
        if (normalized.StartsWith("elf", StringComparison.Ordinal)) return "Elf";
        if (normalized.StartsWith("halfling", StringComparison.Ordinal)) return "Halfling";
        if (normalized.StartsWith("dwarf", StringComparison.Ordinal)) return "Dwarf";
        if (normalized.StartsWith("gnome", StringComparison.Ordinal)) return "Gnome";
        return CharacterCalculator.Races.FirstOrDefault(choice => NormalizeToken(choice) == normalized) ?? "";
    }

    private static string ParseDifficulty(JsonElement? value)
    {
        var tokens = new List<string>();
        if (value is { ValueKind: JsonValueKind.String })
            tokens.Add(value.Value.GetString() ?? "");
        else if (value is { ValueKind: JsonValueKind.Array })
            tokens.AddRange(value.Value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString() ?? ""));
        if (tokens.Any(token => token.Contains("Honour", StringComparison.OrdinalIgnoreCase))) return "Honour";
        if (tokens.Any(token => token.Equals("DifficultyHard", StringComparison.OrdinalIgnoreCase))) return "Tactician";
        if (tokens.Any(token => token.Equals("DifficultyEasy", StringComparison.OrdinalIgnoreCase))) return "Explorer";
        if (tokens.Any(token => token.Equals("DifficultyMedium", StringComparison.OrdinalIgnoreCase))) return "Balanced";
        return "";
    }

    private static string CanonicalSubclass(string className, string? value)
    {
        if (string.IsNullOrWhiteSpace(className) || string.IsNullOrWhiteSpace(value))
            return "";
        var normalized = NormalizeToken(value);
        return BuildOptions.SubclassesByClass.GetValueOrDefault(className, [])
            .FirstOrDefault(choice =>
            {
                var canonical = NormalizeToken(choice);
                return canonical.Equals(normalized, StringComparison.OrdinalIgnoreCase)
                       || (normalized.Length >= 4 && canonical.EndsWith(normalized, StringComparison.OrdinalIgnoreCase));
            }) ?? "";
    }

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
