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
        var characterRoundTrip = false;
        try
        {
            var store = new ProgressStore(progressDirectory);
            items[0].Found = true;
            var testCharacter = new CharacterState { Race = "Drow", ClassName = "Wizard", Difficulty = "Tactician", Level = 7, Intelligence = 18 };
            store.Save(items, testCharacter);
            var loadedState = store.LoadState();
            progressRoundTrip = loadedState.FoundKeys.Contains(items[0].ProgressKey);
            characterRoundTrip = loadedState.Character.Race == "Drow"
                                 && loadedState.Character.ClassName == "Wizard"
                                 && loadedState.Character.Difficulty == "Tactician"
                                 && loadedState.Character.Level == 7
                                 && loadedState.Character.Intelligence == 18;
            items[0].Found = false;
        }
        finally
        {
            Directory.Delete(progressDirectory, true);
        }

        var cloak = items.First(item => item.Name == "Cloak of Displacement");
        var baselineCharacter = new CharacterState();
        var baselineStats = CharacterCalculator.Calculate(baselineCharacter, items);
        cloak.Equipped = true;
        var displacedStats = CharacterCalculator.Calculate(baselineCharacter, items);
        cloak.Equipped = false;
        var displacementMathApplied = displacedStats.Threats[0].AttackHitChance < baselineStats.Threats[0].AttackHitChance
                                      && displacedStats.ActiveEffects.Any(effect => effect.Kind == ItemEffectKind.EnemyAttackDisadvantage);
        var tacticianStats = CharacterCalculator.Calculate(new CharacterState { Difficulty = "Tactician" }, items);
        var explorerStats = CharacterCalculator.Calculate(new CharacterState { Difficulty = "Explorer" }, items);
        var difficultyMathApplied = tacticianStats.Threats[0].AttackBonus == baselineStats.Threats[0].AttackBonus + 2
                                    && tacticianStats.Threats[0].SpellDc == baselineStats.Threats[0].SpellDc + 2
                                    && explorerStats.Proficiency == baselineStats.Proficiency + 2
                                    && explorerStats.HitPoints == baselineStats.HitPoints * 2;

        var report = new
        {
            Passed = items.Count == 556 && uniqueProgressKeys == items.Count && loadedImages == items.Count && progressRoundTrip && characterRoundTrip && displacementMathApplied && difficultyMathApplied && FontManager.IsAlegreyaLoaded,
            ItemCount = items.Count,
            ActCounts = items.GroupBy(item => item.Act).ToDictionary(group => group.Key, group => group.Count()),
            UniqueProgressKeys = uniqueProgressKeys,
            LoadedImages = loadedImages,
            ItemsWithNameLinks = items.Count(item => item.Links.ContainsKey("Name")),
            ItemsWithNotes = items.Count(item => item.Notes.Count > 0),
            ProgressRoundTrip = progressRoundTrip,
            CharacterRoundTrip = characterRoundTrip,
            DisplacementMathApplied = displacementMathApplied,
            BaselineAct1AttackChance = baselineStats.Threats[0].AttackHitChance,
            CloakAct1AttackChance = displacedStats.Threats[0].AttackHitChance,
            DifficultyMathApplied = difficultyMathApplied,
            EmbeddedAlegreyaLoaded = FontManager.IsAlegreyaLoaded
        };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    }
}
