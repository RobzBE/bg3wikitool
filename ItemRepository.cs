using System.Reflection;
using System.Text.Json;

namespace BG3ItemExplorer;

internal static class ItemRepository
{
    public static List<ItemRecord> LoadEmbedded()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith("Data.items.json", StringComparison.OrdinalIgnoreCase));
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("De itemdatabase ontbreekt in het programma.");
        var items = JsonSerializer.Deserialize<List<ItemRecord>>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        return items is { Count: > 0 }
            ? items
            : throw new InvalidOperationException("De itemdatabase is leeg.");
    }
}
