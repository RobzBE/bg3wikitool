using System.Text.Json;

namespace BG3ItemExplorer;

internal static class AppDiagnostics
{
    public static void WriteSelfTestReport(List<ItemRecord> items, string reportPath)
    {
        var uniqueProgressKeys = items.Select(item => item.ProgressKey).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var loadedImages = 0;
        using (var images = new ItemImageRepository())
        {
            foreach (var item in items)
            {
                using var image = images.Load(item.ImageKey);
                if (image is not null && image.Width > 0 && image.Height > 0)
                    loadedImages++;
            }
        }

        var progressDirectory = Path.Combine(Path.GetTempPath(), "BG3ItemExplorer-self-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(progressDirectory);
        var progressRoundTrip = false;
        try
        {
            var store = new ProgressStore(progressDirectory);
            items[0].Found = true;
            store.Save(items);
            progressRoundTrip = store.Load().Contains(items[0].ProgressKey);
            items[0].Found = false;
        }
        finally
        {
            Directory.Delete(progressDirectory, true);
        }

        var report = new
        {
            Passed = items.Count == 556 && uniqueProgressKeys == items.Count && loadedImages == items.Count && progressRoundTrip && FontManager.IsAlegreyaLoaded,
            ItemCount = items.Count,
            ActCounts = items.GroupBy(item => item.Act).ToDictionary(group => group.Key, group => group.Count()),
            UniqueProgressKeys = uniqueProgressKeys,
            LoadedImages = loadedImages,
            ItemsWithNameLinks = items.Count(item => item.Links.ContainsKey("Name")),
            ItemsWithNotes = items.Count(item => item.Notes.Count > 0),
            ProgressRoundTrip = progressRoundTrip,
            EmbeddedAlegreyaLoaded = FontManager.IsAlegreyaLoaded
        };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    }
}
