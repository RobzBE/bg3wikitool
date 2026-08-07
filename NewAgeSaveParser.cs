using System.Buffers.Binary;

namespace BG3ItemExplorer;

/// <summary>Reads the Patch 8 ECS snapshot embedded in Globals.lsf.</summary>
internal sealed class NewAgeSaveParser
{
    private const int DataBase = 48;
    private readonly byte[] _data;
    private readonly Dictionary<string, Component> _components;
    private readonly Dictionary<int, OwnerRange> _ownerRanges;

    private readonly record struct Component(int Index, int Size, int Elements, long Offset);
    private readonly record struct OwnerRange(long Start, long End);

    private NewAgeSaveParser(byte[] data)
    {
        _data = data;
        _components = ReadComponents();
        _ownerRanges = ReadOwnerRanges();
    }

    public static void Apply(
        byte[] data,
        SaveImportResult result,
        IReadOnlyList<ItemRecord> catalogue,
        IReadOnlyList<SavedItemReference> savedItems)
    {
        if (data.Length < DataBase || !data.AsSpan(0, 4).SequenceEqual("LSMF"u8))
            return;
        try
        {
            new NewAgeSaveParser(data).Apply(result, catalogue, savedItems);
        }
        catch (InvalidDataException exception)
        {
            result.Warnings.Add("Patch 8 character data could not be read: " + exception.Message);
        }
    }

    private void Apply(SaveImportResult result, IReadOnlyList<ItemRecord> catalogue, IReadOnlyList<SavedItemReference> savedItems)
    {
        var characterOwners = ReadCharacterOwners();
        if (characterOwners.Count == 0)
            return;

        ApplyAbilities(result, characterOwners);
        ApplyBaseAbilities(result, characterOwners);
        ApplyFeats(result, characterOwners);
        ApplyFightingStyles(result, characterOwners);
        ApplyEquipment(result, characterOwners, catalogue, savedItems);
    }

    private void ApplyFightingStyles(SaveImportResult result, Dictionary<string, uint> characterOwners)
    {
        if (!_components.TryGetValue("game.character_creation.v3.LevelUpComponentData", out var levelDataComponent)
            || !_components.TryGetValue("game.character_creation.v2.LevelUpComponentSelectors", out var selectorsComponent)
            || !_components.TryGetValue("game.character_creation.v2.PassiveSelector", out var passiveComponent)
            || !_components.TryGetValue("game.character_creation.v3.LevelUpComponent", out var levelUpComponent))
            return;
        var levelUpElements = ElementByOwner(levelUpComponent);
        foreach (var character in characterOwners)
        {
            var snapshot = FindCharacter(result, character.Key);
            if (snapshot is null || !levelUpElements.TryGetValue(character.Value, out var levelElement))
                continue;
            var lr = Record(levelUpComponent, levelElement);
            var levelsStart = ReadInt64(lr); var levelsEnd = ReadInt64(lr + 8);
            if (!ValidRange(levelsStart, levelsEnd, 8))
                continue;

            var levelRecords = new List<(int Element, Guid ClassId)>();
            for (var cursor = levelsStart; cursor < levelsEnd; cursor += 8)
            {
                var levelPointer = ReadInt64(Relative(cursor));
                var delta = levelPointer - levelDataComponent.Offset;
                if (delta < 0 || delta % levelDataComponent.Size != 0)
                    continue;
                var dataElement = (int)(delta / levelDataComponent.Size);
                if (dataElement < 0 || dataElement >= selectorsComponent.Elements)
                    continue;
                levelRecords.Add((dataElement, ReadLarianGuid(Record(levelDataComponent, dataElement))));
            }
            var distinctClassIds = levelRecords.Select(record => record.ClassId).Where(id => id != Guid.Empty).Distinct().ToList();
            var classNames = new[] { snapshot.StartingClass }
                .Concat(snapshot.ClassLevels.Keys.Where(name => !name.Equals(snapshot.StartingClass, StringComparison.OrdinalIgnoreCase)))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToList();
            if (distinctClassIds.Count != classNames.Count)
                continue;
            var classById = distinctClassIds.Select((id, index) => (id, classNames[index])).ToDictionary(pair => pair.id, pair => pair.Item2);
            var stylesByClass = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var levelRecord in levelRecords)
            {
                if (!classById.TryGetValue(levelRecord.ClassId, out var className))
                    continue;
                var dataElement = levelRecord.Element;
                var sr = Record(selectorsComponent, dataElement);
                var passivesStart = ReadInt64(sr + 80); var passivesEnd = ReadInt64(sr + 88);
                if (!ValidRange(passivesStart, passivesEnd, 8))
                    continue;
                for (var p = passivesStart; p < passivesEnd; p += 8)
                {
                    var passivePointer = ReadInt64(Relative(p));
                    var passiveDelta = passivePointer - passiveComponent.Offset;
                    if (passiveDelta < 0 || passiveDelta % passiveComponent.Size != 0)
                        continue;
                    var passiveElement = (int)(passiveDelta / passiveComponent.Size);
                    if (passiveElement < 0 || passiveElement >= passiveComponent.Elements)
                        continue;
                    var pr = Record(passiveComponent, passiveElement);
                    var slotsStart = ReadInt64(pr + 24);
                    var slotsEnd = ReadInt64(pr + 32);
                    if (!ValidRange(slotsStart, slotsEnd, 16))
                        continue;
                    for (var slot = slotsStart; slot < slotsEnd; slot += 16)
                    {
                        var textStart = ReadInt64(Relative(slot));
                        var textLength = ReadInt64(Relative(slot) + 8);
                        if (textStart < 0 || textLength is <= 0 or > 128 || !ValidRelative(textStart, (int)textLength))
                            continue;
                        var passiveName = System.Text.Encoding.UTF8.GetString(_data, Relative(textStart), (int)textLength);
                        if (!FightingStyleNames.TryGetValue(passiveName, out var fightingStyle))
                            continue;
                        if (!stylesByClass.TryGetValue(className, out var styles))
                            stylesByClass[className] = styles = [];
                        if (!styles.Contains(fightingStyle, StringComparer.OrdinalIgnoreCase))
                            styles.Add(fightingStyle);
                    }
                }
            }
            snapshot.FightingStyles.Clear();
            foreach (var pair in stylesByClass)
                for (var index = 0; index < pair.Value.Count; index++)
                    snapshot.FightingStyles[BuildOptions.FightingStyleSlotKey(pair.Key, index)] = pair.Value[index];
            snapshot.HasFightingStyleData = true;
        }
    }

    private Dictionary<string, uint> ReadCharacterOwners()
    {
        var component = Required("game.character_creation.v1.CharacterCreationStatsComponent");
        var owners = ReadOwners(component.Index);
        var result = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        for (var element = 0; element < Math.Min(component.Elements, owners.Length); element++)
        {
            var record = Record(component, element);
            var pointer = ReadInt64(record + 56);
            var length = ReadInt32(record + 64);
            if (length is <= 0 or > 256 || !ValidRelative(pointer, length))
                continue;
            var name = System.Text.Encoding.UTF8.GetString(_data, checked(DataBase + (int)pointer), length).TrimEnd('\0');
            if (!string.IsNullOrWhiteSpace(name))
                result[name] = owners[element];
        }
        return result;
    }

    private void ApplyAbilities(SaveImportResult result, Dictionary<string, uint> characterOwners)
    {
        if (!_components.TryGetValue("game.stats.v3.StatsComponent", out var component))
            return;
        var elements = ElementByOwner(component);
        foreach (var pair in characterOwners)
        {
            var snapshot = FindCharacter(result, pair.Key);
            if (snapshot is null || !elements.TryGetValue(pair.Value, out var element))
                continue;
            var record = Record(component, element);
            var values = new[]
            {
                ReadInt32(record + 8), ReadInt32(record + 12), ReadInt32(record + 16),
                ReadInt32(record + 20), ReadInt32(record + 24), ReadInt32(record + 28)
            };
            if (values.Any(value => value is < 1 or > 40))
                continue;
            snapshot.Abilities.Clear();
            for (var index = 0; index < CharacterCalculator.AbilityNames.Length; index++)
                snapshot.Abilities[CharacterCalculator.AbilityNames[index]] = values[index];
            snapshot.HasAbilityData = true;
        }
    }

    private void ApplyBaseAbilities(SaveImportResult result, Dictionary<string, uint> characterOwners)
    {
        if (!_components.TryGetValue("game.character_creation.v1.CharacterCreationStatsComponent", out var creationComponent)
            || !_components.TryGetValue("game.character_creation.v3.LevelUpComponent", out var levelUpComponent)
            || !_components.TryGetValue("game.character_creation.v3.LevelUpComponentData", out var levelDataComponent)
            || !_components.TryGetValue("game.character_creation.v2.LevelUpComponentSelectors", out var selectorsComponent))
            return;

        var creationElements = ElementByOwner(creationComponent);
        var levelUpElements = ElementByOwner(levelUpComponent);
        foreach (var pair in characterOwners)
        {
            var snapshot = FindCharacter(result, pair.Key);
            if (snapshot is null
                || !creationElements.TryGetValue(pair.Value, out var creationElement)
                || !levelUpElements.TryGetValue(pair.Value, out var levelUpElement))
                continue;

            var creationRecord = Record(creationComponent, creationElement);
            var allocationStart = ReadInt64(creationRecord + 72);
            var allocationEnd = ReadInt64(creationRecord + 80);
            if (!ValidRange(allocationStart, allocationEnd, 4) || allocationEnd - allocationStart < 28)
                continue;

            var baseAbilities = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < CharacterCalculator.AbilityNames.Length; index++)
            {
                // The first allocation entry is EAbility::None. Entries 1..6 store
                // the amount above the level-1 floor of 8.
                var allocation = ReadInt32(Relative(allocationStart + (index + 1) * 4L));
                if (allocation is < 0 or > 12)
                {
                    baseAbilities.Clear();
                    break;
                }
                baseAbilities[CharacterCalculator.AbilityNames[index]] = 8 + allocation;
            }
            if (baseAbilities.Count != CharacterCalculator.AbilityNames.Length)
                continue;

            var levelRecord = Record(levelUpComponent, levelUpElement);
            var levelsStart = ReadInt64(levelRecord);
            var levelsEnd = ReadInt64(levelRecord + 8);
            if (ValidRange(levelsStart, levelsEnd, 8) && levelsStart < levelsEnd)
            {
                var firstLevelPointer = ReadInt64(Relative(levelsStart));
                var delta = firstLevelPointer - levelDataComponent.Offset;
                if (delta >= 0 && delta % levelDataComponent.Size == 0)
                {
                    var dataElement = (int)(delta / levelDataComponent.Size);
                    if (dataElement >= 0 && dataElement < selectorsComponent.Elements)
                    {
                        foreach (var bonus in ReadAbilityBonuses(dataElement, selectorsComponent))
                            baseAbilities[bonus.Ability] = Math.Min(20, baseAbilities[bonus.Ability] + bonus.Value);
                    }
                }
            }

            snapshot.BaseAbilities.Clear();
            foreach (var ability in baseAbilities)
                snapshot.BaseAbilities[ability.Key] = ability.Value;
            snapshot.HasBaseAbilityData = true;
        }
    }

    private void ApplyFeats(SaveImportResult result, Dictionary<string, uint> characterOwners)
    {
        if (!_components.TryGetValue("game.character_creation.v3.LevelUpComponent", out var levelUp)
            || !_components.TryGetValue("game.character_creation.v3.LevelUpComponentData", out var levelDataComponent)
            || !_components.TryGetValue("game.character_creation.v2.LevelUpComponentSelectors", out var selectorsComponent))
            return;
        var elements = ElementByOwner(levelUp);
        foreach (var pair in characterOwners)
        {
            var snapshot = FindCharacter(result, pair.Key);
            if (snapshot is null || !elements.TryGetValue(pair.Value, out var element))
                continue;
            var record = Record(levelUp, element);
            var start = ReadInt64(record);
            var end = ReadInt64(record + 8);
            if (!ValidRange(start, end, 8))
                continue;
            snapshot.Feats.Clear();
            var classIds = new List<Guid>();
            for (var cursor = start; cursor < end; cursor += 8)
            {
                var levelData = ReadInt64(Relative(cursor));
                if (!ValidRelative(levelData + 32, 16))
                    continue;
                var dataDelta = levelData - levelDataComponent.Offset;
                var dataElement = dataDelta >= 0 && dataDelta % levelDataComponent.Size == 0
                    ? (int)(dataDelta / levelDataComponent.Size)
                    : -1;
                var classId = ReadLarianGuid(Relative(levelData));
                if (classId != Guid.Empty)
                    classIds.Add(classId);
                var featId = ReadLarianGuid(Relative(levelData + 32));
                if (FeatNames.TryGetValue(featId, out var name))
                    snapshot.Feats.Add(new FeatSelection
                    {
                        Name = name,
                        Choice = ReadFeatChoice(name, dataElement, selectorsComponent)
                    });
            }
            var distinctClassIds = classIds.Distinct().ToList();
            var classNames = new[] { snapshot.StartingClass }
                .Concat(snapshot.ClassLevels.Keys.Where(name => !name.Equals(snapshot.StartingClass, StringComparison.OrdinalIgnoreCase)))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToList();
            if (distinctClassIds.Count == classNames.Count && classIds.Count is >= 1 and <= 12)
            {
                snapshot.ClassLevels.Clear();
                for (var index = 0; index < distinctClassIds.Count; index++)
                    snapshot.ClassLevels[classNames[index]] = classIds.Count(id => id == distinctClassIds[index]);
                snapshot.Level = snapshot.ClassLevels.Values.Sum();
            }
            snapshot.HasFeatData = true;
        }
    }

    private List<(string Ability, int Value)> ReadAbilityBonuses(int dataElement, Component selectorsComponent)
    {
        if (!_components.TryGetValue("game.character_creation.v2.AbilityBonusSelector", out var bonusComponent))
            return [];
        var selectorsRecord = Record(selectorsComponent, dataElement);
        var start = ReadInt64(selectorsRecord + 16);
        var end = ReadInt64(selectorsRecord + 24);
        if (!ValidRange(start, end, 8))
            return [];

        var result = new List<(string Ability, int Value)>();
        for (var cursor = start; cursor < end; cursor += 8)
        {
            var selectorPointer = ReadInt64(Relative(cursor));
            var delta = selectorPointer - bonusComponent.Offset;
            if (delta < 0 || delta % bonusComponent.Size != 0)
                continue;
            var element = (int)(delta / bonusComponent.Size);
            if (element < 0 || element >= bonusComponent.Elements)
                continue;
            var record = Record(bonusComponent, element);
            var abilities = ReadAbilitySlots(ReadInt64(record + 24), ReadInt64(record + 32));
            var valuesStart = ReadInt64(record + 40);
            var valuesEnd = ReadInt64(record + 48);
            if (!ValidRange(valuesStart, valuesEnd, 4))
                continue;
            var values = new List<int>();
            for (var valueCursor = valuesStart; valueCursor < valuesEnd; valueCursor += 4)
                values.Add(ReadInt32(Relative(valueCursor)));
            for (var index = 0; index < Math.Min(abilities.Count, values.Count); index++)
                if (values[index] is > 0 and <= 4)
                    result.Add((abilities[index], values[index]));
        }
        return result;
    }

    private string ReadFeatChoice(string featName, int dataElement, Component selectorsComponent)
    {
        if (dataElement < 0 || dataElement >= selectorsComponent.Elements)
            return "";
        var selectorsRecord = Record(selectorsComponent, dataElement);
        var selectedAbilities = new List<string>();
        if (_components.TryGetValue("game.character_creation.v2.AbilitySelector", out var abilitySelectorComponent))
        {
            var start = ReadInt64(selectorsRecord);
            var end = ReadInt64(selectorsRecord + 8);
            if (ValidRange(start, end, 8))
            {
                for (var cursor = start; cursor < end; cursor += 8)
                {
                    var selectorPointer = ReadInt64(Relative(cursor));
                    var delta = selectorPointer - abilitySelectorComponent.Offset;
                    if (delta < 0 || delta % abilitySelectorComponent.Size != 0)
                        continue;
                    var element = (int)(delta / abilitySelectorComponent.Size);
                    if (element < 0 || element >= abilitySelectorComponent.Elements)
                        continue;
                    var record = Record(abilitySelectorComponent, element);
                    selectedAbilities.AddRange(ReadAbilitySlots(ReadInt64(record + 24), ReadInt64(record + 32)));
                }
            }
        }

        if (featName.Equals("Ability Improvement", StringComparison.OrdinalIgnoreCase) && selectedAbilities.Count > 0)
        {
            var counts = selectedAbilities
                .GroupBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
            if (counts.Count == 1 && counts.Values.Single() >= 2)
                return $"{counts.Keys.Single()} +2";
            var ordered = CharacterCalculator.AbilityNames.Where(counts.ContainsKey).Take(2).ToArray();
            if (ordered.Length == 2)
                return $"{ordered[0]} +1 / {ordered[1]} +1";
        }

        if (selectedAbilities.Count > 0)
            return selectedAbilities[0];

        foreach (var passive in ReadSelectedPassives(selectorsRecord))
        {
            var separator = passive.LastIndexOf('_');
            if (separator < 0)
                continue;
            var choice = AbilityFromName(passive[(separator + 1)..]);
            if (!string.IsNullOrEmpty(choice))
                return choice;
        }

        var definition = BuildOptions.FindFeat(featName);
        return definition?.Choices.Length == 1 ? definition.Choices[0] : "";
    }

    private List<string> ReadAbilitySlots(long start, long end)
    {
        if (!_components.TryGetValue("game.character_creation.v1.AbilityAddSlot", out var slotComponent)
            || !_components.TryGetValue("game.character_creation.v1.EAbility", out var abilityComponent)
            || !ValidRange(start, end, slotComponent.Size)
            || start < slotComponent.Offset
            || end > slotComponent.Offset + (long)slotComponent.Size * slotComponent.Elements)
            return [];
        var result = new List<string>();
        for (var cursor = start; cursor < end; cursor += slotComponent.Size)
        {
            var abilityPointer = ReadInt64(Relative(cursor));
            var abilityDelta = abilityPointer - abilityComponent.Offset;
            if (abilityDelta < 0 || abilityDelta % abilityComponent.Size != 0)
                continue;
            var abilityElement = (int)(abilityDelta / abilityComponent.Size);
            if (abilityElement < 0 || abilityElement >= abilityComponent.Elements)
                continue;
            var ability = AbilityFromValue(ReadInt32(Record(abilityComponent, abilityElement)));
            if (!string.IsNullOrEmpty(ability))
                result.Add(ability);
        }
        return result;
    }

    private List<string> ReadSelectedPassives(int selectorsRecord)
    {
        if (!_components.TryGetValue("game.character_creation.v2.PassiveSelector", out var passiveComponent))
            return [];
        var start = ReadInt64(selectorsRecord + 80);
        var end = ReadInt64(selectorsRecord + 88);
        if (!ValidRange(start, end, 8))
            return [];
        var result = new List<string>();
        for (var cursor = start; cursor < end; cursor += 8)
        {
            var selectorPointer = ReadInt64(Relative(cursor));
            var delta = selectorPointer - passiveComponent.Offset;
            if (delta < 0 || delta % passiveComponent.Size != 0)
                continue;
            var element = (int)(delta / passiveComponent.Size);
            if (element < 0 || element >= passiveComponent.Elements)
                continue;
            var record = Record(passiveComponent, element);
            var slotsStart = ReadInt64(record + 24);
            var slotsEnd = ReadInt64(record + 32);
            if (!ValidRange(slotsStart, slotsEnd, 16))
                continue;
            for (var slot = slotsStart; slot < slotsEnd; slot += 16)
            {
                var textStart = ReadInt64(Relative(slot));
                var textLength = ReadInt64(Relative(slot) + 8);
                if (textLength is > 0 and <= 128 && ValidRelative(textStart, (int)textLength))
                    result.Add(System.Text.Encoding.UTF8.GetString(_data, Relative(textStart), (int)textLength));
            }
        }
        return result;
    }

    private static string AbilityFromValue(int value) => value switch
    {
        1 => "STR", 2 => "DEX", 3 => "CON", 4 => "INT", 5 => "WIS", 6 => "CHA", _ => ""
    };

    private static string AbilityFromName(string value) => value.ToLowerInvariant() switch
    {
        "strength" => "STR", "dexterity" => "DEX", "constitution" => "CON",
        "intelligence" => "INT", "wisdom" => "WIS", "charisma" => "CHA", _ => ""
    };

    private void ApplyEquipment(
        SaveImportResult result,
        Dictionary<string, uint> characterOwners,
        IReadOnlyList<ItemRecord> catalogue,
        IReadOnlyList<SavedItemReference> savedItems)
    {
        if (!_components.TryGetValue("core.v0.EntityId", out var entityIds)
            || !_components.TryGetValue("game.inventory.v0.OwnerComponent", out var inventories)
            || !_components.TryGetValue("game.inventory.v1.ContainerComponent", out var containers)
            || !_components.TryGetValue("game.templates.v0.TemplateComponent", out var templates))
            return;

        var entitiesById = new Dictionary<string, List<uint>>(StringComparer.Ordinal);
        for (uint entity = 0; entity < entityIds.Elements; entity++)
        {
            var key = Convert.ToHexString(_data, checked(Record(entityIds, (int)entity)), 16);
            if (!entitiesById.TryGetValue(key, out var list))
                entitiesById[key] = list = [];
            list.Add(entity);
        }
        var inventoryElements = ElementByOwner(inventories);
        var containerElements = ElementByOwner(containers);
        var templateElements = ElementByOwner(templates);
        var savedByTemplate = savedItems
            .Where(item => item.CurrentTemplate.HasValue)
            .GroupBy(item => item.CurrentTemplate!.Value)
            .ToDictionary(group => group.Key, group => group.First());
        var catalogueById = catalogue
            .SelectMany(item => item.GameIds.Select(id => (Id: id, Item: item)))
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Id))
            .GroupBy(pair => pair.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Item, StringComparer.OrdinalIgnoreCase);

        foreach (var pair in characterOwners)
        {
            var snapshot = FindCharacter(result, pair.Key);
            if (snapshot is null || !inventoryElements.TryGetValue(pair.Value, out var inventoryElement))
                continue;
            var ownerRecord = Record(inventories, inventoryElement);
            var start = ReadInt64(ownerRecord);
            var end = ReadInt64(ownerRecord + 8);
            if (!ValidRange(start, end, 8) || end - start < 16)
                continue;

            // The second owned inventory is the character's equipment container.
            var equipmentPointer = ReadInt64(Relative(start + 8));
            if (!ValidRelative(equipmentPointer, 16))
                continue;
            var equipmentId = Convert.ToHexString(_data, Relative(equipmentPointer), 16);
            if (!entitiesById.TryGetValue(equipmentId, out var inventoryEntities))
                continue;
            var equipmentEntity = inventoryEntities.FirstOrDefault(containerElements.ContainsKey);
            if (!containerElements.TryGetValue(equipmentEntity, out var containerElement))
                continue;

            var containerRecord = Record(containers, containerElement);
            var valuesStart = ReadInt64(containerRecord + 16);
            var valuesEnd = ReadInt64(containerRecord + 24);
            if (!ValidRange(valuesStart, valuesEnd, 8))
                continue;

            snapshot.EquippedKeys.Clear();
            for (var cursor = valuesStart; cursor < valuesEnd; cursor += 8)
            {
                var slotPointer = ReadInt64(Relative(cursor));
                if (!ValidRelative(slotPointer, 8))
                    continue;
                var itemPointer = ReadInt64(Relative(slotPointer));
                if (!ValidRelative(itemPointer, 16))
                    continue;
                var itemId = Convert.ToHexString(_data, Relative(itemPointer), 16);
                if (!entitiesById.TryGetValue(itemId, out var itemEntities))
                    continue;
                foreach (var itemEntity in itemEntities)
                {
                    if (!templateElements.TryGetValue(itemEntity, out var templateElement))
                        continue;
                    var templateRecord = Record(templates, templateElement);
                    var templatePointer = ReadInt64(templateRecord);
                    var templateLength = ReadInt32(templateRecord + 8);
                    if (templateLength is <= 0 or > 64 || !ValidRelative(templatePointer, templateLength))
                        continue;
                    var templateText = System.Text.Encoding.UTF8.GetString(_data, Relative(templatePointer), templateLength).TrimEnd('\0');
                    if (!Guid.TryParse(templateText, out var templateId))
                        continue;
                    templateId = SwapGuidTail(templateId);
                    if (savedByTemplate.TryGetValue(templateId, out var saved))
                    {
                        if (catalogueById.TryGetValue(saved.StatsId, out var item))
                        {
                            snapshot.EquippedKeys.Add(item.ProgressKey);
                            result.PresentKeys.Add(item.ProgressKey);
                            break;
                        }
                    }
                }
            }
            snapshot.EquippedKeys.Sort(StringComparer.OrdinalIgnoreCase);
            snapshot.HasEquipmentData = true;
            result.MatchedItems += snapshot.EquippedKeys.Count;
        }
    }

    private Dictionary<uint, int> ElementByOwner(Component component)
    {
        var owners = ReadOwners(component.Index);
        var result = new Dictionary<uint, int>();
        for (var index = 0; index < Math.Min(component.Elements, owners.Length); index++)
            result.TryAdd(owners[index], index);
        return result;
    }

    private uint[] ReadOwners(int componentIndex)
    {
        if (!_ownerRanges.TryGetValue(componentIndex, out var range) || !ValidRange(range.Start, range.End, 4))
            return [];
        var owners = new uint[(range.End - range.Start) / 4];
        for (var index = 0; index < owners.Length; index++)
            owners[index] = ReadUInt32(Relative(range.Start + index * 4L));
        return owners;
    }

    private Dictionary<string, Component> ReadComponents()
    {
        var blockOffset = ReadInt64(16);
        var namesLength = ReadInt32(32);
        var namesStart = DataBase + blockOffset;
        var table = namesStart + namesLength;
        var remaining = _data.LongLength - table;
        if (remaining < 0 || remaining % 48 != 0)
            throw new InvalidDataException("invalid component table size");
        var count = remaining / 48;
        if (count is <= 0 or > 4096)
            throw new InvalidDataException("invalid component table");
        var result = new Dictionary<string, Component>(StringComparer.Ordinal);
        for (var index = 0; index < count; index++)
        {
            var entry = checked((int)(table + index * 48));
            var nameOffset = ReadInt64(entry);
            var nameLength = ReadInt32(entry + 8);
            var namePosition = namesStart + nameOffset;
            if (nameLength <= 0 || !ValidAbsolute(namePosition, nameLength))
                continue;
            var name = System.Text.Encoding.UTF8.GetString(_data, checked((int)namePosition), nameLength).TrimEnd('\0');
            var size = ReadInt32(entry + 24);
            var elements = checked((int)ReadInt64(entry + 32));
            var offset = ReadInt64(entry + 40);
            result[name] = new Component(index, size, elements, offset);
        }
        return result;
    }

    private Dictionary<int, OwnerRange> ReadOwnerRanges()
    {
        var level = Required("core.v0.Level");
        var record = Record(level, 0);
        var start = ReadInt64(record + 16);
        var end = ReadInt64(record + 24);
        if (!ValidRange(start, end, 32))
            throw new InvalidDataException("invalid owner map");
        var result = new Dictionary<int, OwnerRange>();
        for (var cursor = start; cursor < end; cursor += 32)
        {
            var position = Relative(cursor);
            var ownersStart = ReadInt64(position);
            var ownersEnd = ReadInt64(position + 8);
            var componentIndex = checked((int)ReadInt64(position + 16));
            result[componentIndex] = new OwnerRange(ownersStart, ownersEnd);
        }
        return result;
    }

    private Component Required(string name) =>
        _components.TryGetValue(name, out var component) ? component : throw new InvalidDataException($"missing {name}");

    private int Record(Component component, int element)
    {
        var position = DataBase + component.Offset + (long)component.Size * element;
        if (component.Size <= 0 || element < 0 || element >= component.Elements || !ValidAbsolute(position, component.Size))
            throw new InvalidDataException("component record outside buffer");
        return checked((int)position);
    }

    private int Relative(long offset)
    {
        var position = DataBase + offset;
        if (!ValidAbsolute(position, 1))
            throw new InvalidDataException("relative pointer outside buffer");
        return checked((int)position);
    }

    private bool ValidRange(long start, long end, int stride) =>
        start >= 0 && end >= start && end - start <= int.MaxValue
        && (end - start) % stride == 0 && ValidRelative(start, (int)(end - start));
    private bool ValidRelative(long offset, int length) => ValidAbsolute(DataBase + offset, length);
    private bool ValidAbsolute(long offset, int length) => offset >= 0 && length >= 0 && offset + length <= _data.LongLength;
    private int ReadInt32(int offset) => BinaryPrimitives.ReadInt32LittleEndian(_data.AsSpan(offset, 4));
    private uint ReadUInt32(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(_data.AsSpan(offset, 4));
    private long ReadInt64(int offset) => BinaryPrimitives.ReadInt64LittleEndian(_data.AsSpan(offset, 8));

    private Guid ReadLarianGuid(int offset)
    {
        var bytes = _data.AsSpan(offset, 16).ToArray();
        for (var index = 8; index < 16; index += 2)
            (bytes[index], bytes[index + 1]) = (bytes[index + 1], bytes[index]);
        return new Guid(bytes);
    }

    private static Guid SwapGuidTail(Guid value)
    {
        var bytes = value.ToByteArray();
        for (var index = 8; index < 16; index += 2)
            (bytes[index], bytes[index + 1]) = (bytes[index + 1], bytes[index]);
        return new Guid(bytes);
    }

    private static SaveCharacterSnapshot? FindCharacter(SaveImportResult result, string name) =>
        result.Characters.FirstOrDefault(character => character.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    private static readonly Dictionary<string, string> FightingStyleNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["FightingStyle_Archery"] = "Archery",
        ["FightingStyle_Defense"] = "Defence",
        ["FightingStyle_Dueling"] = "Duelling",
        ["FightingStyle_GreatWeaponFighting"] = "Great Weapon Fighting",
        ["FightingStyle_Protection"] = "Protection",
        ["FightingStyle_TwoWeaponFighting"] = "Two-Weapon Fighting"
    };

    private static readonly Dictionary<Guid, string> FeatNames = new()
    {
        [Guid.Parse("d215b9ad-9753-4d74-8ff9-24bf1dce53d6")] = "Ability Improvement",
        [Guid.Parse("cdcbc538-883b-401c-a8ed-1373fb6d1720")] = "Actor",
        [Guid.Parse("f57bd72c-be64-4855-9e3a-bb7d7665e656")] = "Alert",
        [Guid.Parse("d674aa33-8633-4b67-8623-b6788f0d5fc4")] = "Athlete",
        [Guid.Parse("eab25714-15d6-4e26-b809-3fad832d0484")] = "Charger",
        [Guid.Parse("94a78b4c-a8f2-404f-8cdc-2d454c13cb97")] = "Crossbow Expert",
        [Guid.Parse("661eee63-ff91-4f29-9f21-3a974c9d6fe5")] = "Defensive Duellist",
        [Guid.Parse("f692f7b5-ffd5-4942-91a1-a71ebb2f5e7c")] = "Dual Wielder",
        [Guid.Parse("71b65667-0eac-4e62-b878-fa862e88ebbf")] = "Dungeon Delver",
        [Guid.Parse("56c3c247-35cf-4ffd-86dd-7d249cc1808f")] = "Durable",
        [Guid.Parse("cec2d95b-451c-40f8-8e17-9e547d363e8e")] = "Elemental Adept",
        [Guid.Parse("c09815f7-282b-4ccf-bd89-a51caa1b550f")] = "Great Weapon Master",
        [Guid.Parse("7bc235ac-7eeb-49d3-8249-c3313d87af75")] = "Heavily Armoured",
        [Guid.Parse("0de08fff-ab18-442f-a0b1-f53e7be04c03")] = "Heavy Armour Master",
        [Guid.Parse("b441c722-e4d4-4702-861a-039bfd77c124")] = "Lightly Armoured",
        [Guid.Parse("d84c7f36-8c5b-4b17-b95a-e1da725f9004")] = "Lucky",
        [Guid.Parse("a533fde7-ee0a-46ce-92e2-9763201a54d2")] = "Mage Slayer",
        [Guid.Parse("4f744a6e-8589-4a46-89ab-a95415a73245")] = "Magic Initiate: Bard",
        [Guid.Parse("28e5fed3-bd41-4b74-ab48-b8e824ad3443")] = "Magic Initiate: Cleric",
        [Guid.Parse("1e1a9a4d-38f4-4a05-b080-d32c2b872250")] = "Magic Initiate: Druid",
        [Guid.Parse("93d41226-d7af-495c-b023-08a6af077962")] = "Magic Initiate: Sorcerer",
        [Guid.Parse("1e0ac3c4-5bb5-42d7-941c-9de58d919732")] = "Magic Initiate: Warlock",
        [Guid.Parse("26c6990b-d9f0-41a2-8108-209789fafc18")] = "Magic Initiate: Wizard",
        [Guid.Parse("455fc2d5-1c77-40e7-a010-0b51044ae74b")] = "Martial Adept",
        [Guid.Parse("17ac3605-9a8a-41f3-9504-ffc17fffa03e")] = "Medium Armour Master",
        [Guid.Parse("0a3b07bf-a806-4c77-9c8f-6e7c0965f9dd")] = "Mobile",
        [Guid.Parse("681d5307-f0ed-4c94-8cf0-db0c51116f56")] = "Moderately Armoured",
        [Guid.Parse("60dfd716-3ba8-4611-90ee-018b59775b1d")] = "Performer",
        [Guid.Parse("fdf0be80-cc1e-4501-bd2e-7a1ea737362c")] = "Polearm Master",
        [Guid.Parse("b13c4744-1d45-42da-b92c-e09f598ab1c3")] = "Resilient",
        [Guid.Parse("f3370916-6b35-4c5b-af36-19ca888cb43e")] = "Ritual Caster",
        [Guid.Parse("e061a323-3430-4cff-88d3-5eae7a1779a4")] = "Savage Attacker",
        [Guid.Parse("816b1554-9384-49e9-aaa0-05dd622e60f7")] = "Sentinel",
        [Guid.Parse("010f717e-c6e2-45cf-bbf9-298a72db4cad")] = "Sharpshooter",
        [Guid.Parse("3fe71254-d1b2-44c7-886c-927552fe5f2e")] = "Shield Master",
        [Guid.Parse("019564a0-f136-4139-94ea-040f94bbaf19")] = "Skilled",
        [Guid.Parse("c02f22c9-9a06-4001-b917-4d5cf09be399")] = "Spell Sniper",
        [Guid.Parse("be0889d2-f9aa-472d-b942-592bff0f1ef3")] = "Tavern Brawler",
        [Guid.Parse("e8d1e7f6-d841-48ff-a83c-f1aaa16597ff")] = "Tough",
        [Guid.Parse("ed4e367d-d136-4285-abac-077147e84cf2")] = "War Caster",
        [Guid.Parse("b153e75c-27a2-4412-95cd-60b477121679")] = "Weapon Master"
    };
}
