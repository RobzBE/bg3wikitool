namespace BG3ItemExplorer;

internal sealed class CharacterState
{
    public string Race { get; set; } = "Human";
    public string ClassName { get; set; } = "Fighter";
    public string Difficulty { get; set; } = "Balanced";
    public int Level { get; set; } = 1;
    public int Strength { get; set; } = 16;
    public int Dexterity { get; set; } = 14;
    public int Constitution { get; set; } = 14;
    public int Intelligence { get; set; } = 10;
    public int Wisdom { get; set; } = 10;
    public int Charisma { get; set; } = 10;
    public List<string> EquippedKeys { get; set; } = [];
    public List<string> DisabledConditionalEffects { get; set; } = [];
    public List<string> EnabledConditionalEffects { get; set; } = [];

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
}
