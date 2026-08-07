namespace BG3ItemExplorer;

internal sealed class CharacterState
{
    public string TemplateId { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Character";
    public string Race { get; set; } = "Human";
    public string ClassName { get; set; } = "Fighter";
    public string SubclassName { get; set; } = "";
    public Dictionary<string, string> Subclasses { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string Difficulty { get; set; } = "Balanced";
    public int Level { get; set; } = 1;
    public Dictionary<string, int> ClassLevels { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public int Strength { get; set; } = 16;
    public int Dexterity { get; set; } = 14;
    public int Constitution { get; set; } = 14;
    public int Intelligence { get; set; } = 10;
    public int Wisdom { get; set; } = 10;
    public int Charisma { get; set; } = 10;
    public bool ImportedCurrentAbilities { get; set; }
    public List<string> EquippedKeys { get; set; } = [];
    public List<string> DisabledConditionalEffects { get; set; } = [];
    public List<string> EnabledConditionalEffects { get; set; } = [];
    public Dictionary<string, string> FightingStyles { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<FeatSelection> Feats { get; set; } = [];
    public List<string> ActiveBuffs { get; set; } = [];
    public List<PermanentBonusSelection> PermanentBonuses { get; set; } = [];

    public int TotalLevel
    {
        get
        {
            var assignedLevels = ClassLevels?.Values.Sum() ?? 0;
            return Math.Clamp(assignedLevels > 0 ? assignedLevels : Level, 1, 12);
        }
    }

    public int GetClassLevel(string className) =>
        ClassLevels is not null && ClassLevels.TryGetValue(className, out var level) ? level : 0;

    public bool HasClass(string className) => GetClassLevel(className) > 0;

    public string GetSubclass(string className)
    {
        if (Subclasses is not null && Subclasses.TryGetValue(className, out var subclass))
            return subclass;
        return className.Equals(ClassName, StringComparison.OrdinalIgnoreCase) ? SubclassName : "";
    }

    public void SetSubclass(string className, string? subclass)
    {
        Subclasses ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(subclass))
            Subclasses.Remove(className);
        else
            Subclasses[className] = subclass.Trim();
        if (className.Equals(ClassName, StringComparison.OrdinalIgnoreCase))
            SubclassName = string.IsNullOrWhiteSpace(subclass) ? "" : subclass.Trim();
    }

    public void NormalizeClassLevels(bool allowMulticlass)
    {
        if (!CharacterCalculator.Classes.Contains(ClassName, StringComparer.OrdinalIgnoreCase))
            ClassName = "Fighter";

        ClassLevels ??= new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var normalized = CharacterCalculator.Classes.ToDictionary(
            className => className,
            className => Math.Clamp(GetClassLevel(className), 0, 12),
            StringComparer.OrdinalIgnoreCase);

        var requestedTotal = normalized.Values.Sum();
        if (requestedTotal == 0)
        {
            normalized[ClassName] = Math.Clamp(Level, 1, 12);
            requestedTotal = normalized[ClassName];
        }

        if (!allowMulticlass)
        {
            foreach (var className in CharacterCalculator.Classes)
                normalized[className] = 0;
            normalized[ClassName] = Math.Clamp(requestedTotal, 1, 12);
        }
        else
        {
            if (normalized[ClassName] == 0)
            {
                if (requestedTotal >= 12)
                {
                    var donor = CharacterCalculator.Classes.LastOrDefault(className =>
                        !className.Equals(ClassName, StringComparison.OrdinalIgnoreCase) && normalized[className] > 0);
                    if (donor is not null)
                        normalized[donor]--;
                }
                normalized[ClassName] = 1;
            }

            var excess = normalized.Values.Sum() - 12;
            foreach (var className in CharacterCalculator.Classes.Reverse())
            {
                if (excess <= 0)
                    break;
                var minimum = className.Equals(ClassName, StringComparison.OrdinalIgnoreCase) ? 1 : 0;
                var reduction = Math.Min(excess, normalized[className] - minimum);
                normalized[className] -= reduction;
                excess -= reduction;
            }
        }

        ClassLevels = normalized
            .Where(pair => pair.Value > 0)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        Level = ClassLevels.Values.Sum();
        Subclasses ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(SubclassName) && !Subclasses.ContainsKey(ClassName))
            Subclasses[ClassName] = SubclassName;
        if (string.IsNullOrWhiteSpace(GetSubclass("Fighter"))
            && ClassName.Equals("Fighter", StringComparison.OrdinalIgnoreCase)
            && GetClassLevel("Fighter") >= 3
            && ActiveBuffs?.Contains("Champion: Improved Critical Hit", StringComparer.OrdinalIgnoreCase) == true)
            Subclasses["Fighter"] = "Champion";
        var normalizedSubclasses = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var className in ClassLevels.Keys)
        {
            if (GetClassLevel(className) < BuildOptions.SubclassLevel(className))
                continue;
            var choices = BuildOptions.SubclassesByClass.GetValueOrDefault(className, []);
            var selected = choices.FirstOrDefault(choice => choice.Equals(GetSubclass(className), StringComparison.OrdinalIgnoreCase))
                           ?? choices.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(selected))
                normalizedSubclasses[className] = selected;
        }
        Subclasses = normalizedSubclasses;
        SubclassName = GetSubclass(ClassName);
        FightingStyles ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Feats ??= [];
        EquippedKeys ??= [];
        DisabledConditionalEffects ??= [];
        EnabledConditionalEffects ??= [];
        ActiveBuffs ??= [];
        ActiveBuffs = ActiveBuffs.Where(name => BuildOptions.FindBuff(name) is not null).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var availableClassOptions = BuildOptions.AvailableClassOptions(this).Select(option => option.BuffName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        ActiveBuffs.RemoveAll(name => BuildOptions.IsClassOption(name) && !availableClassOptions.Contains(name));
        PermanentBonuses ??= [];
        PermanentBonuses = PermanentBonuses
            .Where(selection => PermanentBonusCatalog.Find(selection.Name) is not null)
            .GroupBy(selection => selection.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
        foreach (var selection in PermanentBonuses)
        {
            var definition = PermanentBonusCatalog.Find(selection.Name)!;
            if (definition.Choices.Length > 0 && !definition.Choices.Contains(selection.Choice, StringComparer.OrdinalIgnoreCase))
                selection.Choice = definition.Choices[0];
        }
        if (string.IsNullOrWhiteSpace(TemplateId))
            TemplateId = Guid.NewGuid().ToString("N");
        var slots = BuildOptions.FeatSlotCount(this);
        if (Feats.Count > slots)
            Feats.RemoveRange(slots, Feats.Count - slots);
    }

    public bool HasFeat(string featName) =>
        Feats?.Any(feat => feat.Name.Equals(featName, StringComparison.OrdinalIgnoreCase)) == true;

    public bool HasBuff(string buffName) =>
        ActiveBuffs?.Contains(buffName, StringComparer.OrdinalIgnoreCase) == true;

    public bool HasPermanentBonus(string bonusName) =>
        PermanentBonuses?.Any(bonus => bonus.Name.Equals(bonusName, StringComparison.OrdinalIgnoreCase)) == true;

    public string PermanentBonusChoice(string bonusName) =>
        PermanentBonuses?.FirstOrDefault(bonus => bonus.Name.Equals(bonusName, StringComparison.OrdinalIgnoreCase))?.Choice ?? "";

    public bool IsEffectActive(ItemEffect effect)
    {
        if (!effect.Conditional)
            return true;
        if (EnabledConditionalEffects.Contains(effect.Id, StringComparer.OrdinalIgnoreCase))
            return true;
        if (DisabledConditionalEffects.Contains(effect.Id, StringComparer.OrdinalIgnoreCase))
            return false;
        return effect.DefaultActive;
    }

    public void SetEffectActive(string effectId, bool active)
    {
        DisabledConditionalEffects.RemoveAll(value => value.Equals(effectId, StringComparison.OrdinalIgnoreCase));
        EnabledConditionalEffects.RemoveAll(value => value.Equals(effectId, StringComparison.OrdinalIgnoreCase));
        if (active)
            EnabledConditionalEffects.Add(effectId);
        else
            DisabledConditionalEffects.Add(effectId);
    }

    public int GetAbility(string ability) => ability switch
    {
        "STR" => Strength,
        "DEX" => Dexterity,
        "CON" => Constitution,
        "INT" => Intelligence,
        "WIS" => Wisdom,
        "CHA" => Charisma,
        _ => 10
    };

    public void SetAbility(string ability, int value)
    {
        switch (ability)
        {
            case "STR": Strength = value; break;
            case "DEX": Dexterity = value; break;
            case "CON": Constitution = value; break;
            case "INT": Intelligence = value; break;
            case "WIS": Wisdom = value; break;
            case "CHA": Charisma = value; break;
        }
    }
}

internal static class GearRules
{
    public static string SlotFor(ItemRecord item)
    {
        var type = item.Type;
        if (type.Contains("Helmet", StringComparison.OrdinalIgnoreCase)) return "Head";
        if (type.Contains("Gloves", StringComparison.OrdinalIgnoreCase)) return "Hands";
        if (type.Contains("Boots", StringComparison.OrdinalIgnoreCase)) return "Feet";
        if (type.Contains("Armour", StringComparison.OrdinalIgnoreCase) || type.Equals("Clothing", StringComparison.OrdinalIgnoreCase)) return "Body";
        if (type.Equals("Cloak", StringComparison.OrdinalIgnoreCase)) return "Cloak";
        if (type.Equals("Amulet", StringComparison.OrdinalIgnoreCase)) return "Amulet";
        if (type.Equals("Ring", StringComparison.OrdinalIgnoreCase)) return "Ring";
        if (type.Equals("Shield", StringComparison.OrdinalIgnoreCase)) return "Off Hand";
        if (type.Contains("Bow", StringComparison.OrdinalIgnoreCase) || type.Contains("Crossbow", StringComparison.OrdinalIgnoreCase)) return "Ranged";
        return "Main Hand";
    }

    public static void Equip(List<ItemRecord> allItems, ItemRecord item)
    {
        var slot = SlotFor(item);
        if (slot == "Ring")
        {
            var rings = allItems.Where(candidate => candidate.Equipped && SlotFor(candidate) == "Ring" && candidate != item).ToList();
            if (rings.Count >= 2)
                rings[0].Equipped = false;
        }
        else
        {
            foreach (var candidate in allItems.Where(candidate => candidate.Equipped && candidate != item && SlotFor(candidate) == slot))
                candidate.Equipped = false;
        }
        item.Equipped = true;
    }

    public static void EquipForCharacter(List<ItemRecord> allItems, CharacterState character, ItemRecord item)
    {
        character.EquippedKeys ??= [];
        var equippedKeys = character.EquippedKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var slot = SlotFor(item);
        var equippedInSlot = allItems
            .Where(candidate => candidate != item && equippedKeys.Contains(candidate.ProgressKey) && SlotFor(candidate) == slot)
            .ToList();
        if (slot == "Ring")
        {
            if (equippedInSlot.Count >= 2)
                equippedKeys.Remove(equippedInSlot[0].ProgressKey);
        }
        else
        {
            foreach (var candidate in equippedInSlot)
                equippedKeys.Remove(candidate.ProgressKey);
        }
        equippedKeys.Add(item.ProgressKey);
        character.EquippedKeys = equippedKeys.OrderBy(key => key).ToList();
    }
}
