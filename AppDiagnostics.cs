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
            var testCharacter = new CharacterState
            {
                Race = "Drow",
                ClassName = "Fighter",
                Difficulty = "Tactician",
                Level = 7,
                Intelligence = 18,
                ClassLevels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["Fighter"] = 2, ["Wizard"] = 5 },
                FightingStyles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Fighter"] = "Defence" },
                Feats = [new FeatSelection { Name = "Ability Improvement", Choice = "INT +2" }],
                ActiveBuffs = ["Mage Armour"]
            };
            store.Save(items, testCharacter);
            var loadedState = store.LoadState();
            progressRoundTrip = loadedState.FoundKeys.Contains(items[0].ProgressKey);
            characterRoundTrip = loadedState.Character.Race == "Drow"
                                 && loadedState.Character.ClassName == "Fighter"
                                 && loadedState.Character.Difficulty == "Tactician"
                                 && loadedState.Character.Level == 7
                                 && loadedState.Character.Intelligence == 18
                                 && loadedState.Character.GetClassLevel("Fighter") == 2
                                 && loadedState.Character.GetClassLevel("Wizard") == 5
                                 && loadedState.Character.FightingStyles.GetValueOrDefault("Fighter") == "Defence"
                                 && loadedState.Character.Feats.Any(feat => feat.Name == "Ability Improvement" && feat.Choice == "INT +2")
                                 && loadedState.Character.HasBuff("Mage Armour");
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
        var worstCaseThreatsApplied = baselineStats.Threats[0].AttackEnemy == "Grym"
                                      && baselineStats.Threats[0].AttackBonus == 11
                                      && baselineStats.Threats[1].AttackEnemy == "Apostle of Myrkul"
                                      && baselineStats.Threats[2].AttackEnemy == "Dominated Red Dragon"
                                      && baselineStats.Threats[2].SpellEnemy == "Netherbrain"
                                      && baselineStats.Threats[2].SpellDc == 23;
        var naturalRollBoundsApplied = CharacterCalculator.AttackHitChance(100, 0) == 5
                                       && CharacterCalculator.AttackHitChance(100, 0, criticalHitImmune: true) == 0
                                       && CharacterCalculator.AttackHitChance(-100, 0) == 95
                                       && CharacterCalculator.ApplyRollMode(5, false, true) == 0.25
                                       && CharacterCalculator.ApplyRollMode(95, true, false) == 99.75;
        var multiclassState = new CharacterState
        {
            ClassName = "Fighter",
            Level = 5,
            Intelligence = 18,
            ClassLevels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["Fighter"] = 1, ["Wizard"] = 4 }
        };
        var multiclassStats = CharacterCalculator.Calculate(multiclassState, items);
        var explorerMulticlassState = new CharacterState
        {
            ClassName = "Fighter",
            Difficulty = "Explorer",
            Level = 4,
            ClassLevels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["Fighter"] = 2, ["Wizard"] = 2 }
        };
        CharacterCalculator.Calculate(explorerMulticlassState, items);
        var cappedState = new CharacterState
        {
            ClassName = "Fighter",
            Level = 16,
            ClassLevels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["Fighter"] = 8, ["Wizard"] = 8 }
        };
        cappedState.NormalizeClassLevels(allowMulticlass: true);
        var legacyState = new CharacterState { ClassName = "Wizard", Level = 7 };
        CharacterCalculator.Calculate(legacyState, items);
        var heavyArmour = items.First(item => item.Type.Contains("Heavy", StringComparison.OrdinalIgnoreCase));
        heavyArmour.Equipped = true;
        var wizardFighterStats = CharacterCalculator.Calculate(
            new CharacterState
            {
                ClassName = "Wizard",
                Level = 2,
                ClassLevels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["Wizard"] = 1, ["Fighter"] = 1 }
            },
            items);
        var fighterWizardStats = CharacterCalculator.Calculate(
            new CharacterState
            {
                ClassName = "Fighter",
                Level = 2,
                ClassLevels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["Fighter"] = 1, ["Wizard"] = 1 }
            },
            items);
        heavyArmour.Equipped = false;
        var multiclassMathApplied = multiclassState.TotalLevel == 5
                                    && multiclassStats.Proficiency == 3
                                    && multiclassStats.HitPoints == 36
                                    && multiclassStats.SpellClass == "Wizard"
                                    && multiclassStats.SpellSaveDc == 15
                                    && multiclassStats.Saves["STR"] == 6
                                    && multiclassStats.Saves["INT"] == 4
                                    && explorerMulticlassState.GetClassLevel("Fighter") == 4
                                    && explorerMulticlassState.GetClassLevel("Wizard") == 0
                                    && cappedState.TotalLevel == 12
                                    && legacyState.GetClassLevel("Wizard") == 7
                                    && wizardFighterStats.NonProficientGear.Contains(heavyArmour.Name)
                                    && !fighterWizardStats.NonProficientGear.Contains(heavyArmour.Name);

        var alertState = new CharacterState
        {
            ClassName = "Fighter",
            Level = 4,
            ClassLevels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["Fighter"] = 4 },
            Feats = [new FeatSelection { Name = "Alert" }]
        };
        var alertStats = CharacterCalculator.Calculate(alertState, items);
        var toughState = new CharacterState
        {
            ClassName = "Fighter",
            Level = 4,
            ClassLevels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["Fighter"] = 4 },
            Feats = [new FeatSelection { Name = "Tough" }]
        };
        var toughStats = CharacterCalculator.Calculate(toughState, items);
        var mageArmourState = new CharacterState
        {
            ClassName = "Wizard",
            Level = 1,
            Dexterity = 14,
            ClassLevels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["Wizard"] = 1 },
            ActiveBuffs = ["Mage Armour"]
        };
        var mageArmourStats = CharacterCalculator.Calculate(mageArmourState, items);
        var blessState = new CharacterState { ActiveBuffs = ["Bless"] };
        var blessStats = CharacterCalculator.Calculate(blessState, items);
        var mirrorStats = CharacterCalculator.Calculate(new CharacterState { ActiveBuffs = ["Mirror Image (3 images)"] }, items);
        var sanctuaryStats = CharacterCalculator.Calculate(new CharacterState { ActiveBuffs = ["Sanctuary"] }, items);
        var resistanceStats = CharacterCalculator.Calculate(new CharacterState { ActiveBuffs = ["Resistance"] }, items);
        var featAndBuffMathApplied = BuildOptions.Feats.Length == 41
                                     && BuildOptions.FeatSlotCount(new CharacterState
                                     {
                                         ClassName = "Fighter",
                                         Level = 10,
                                         ClassLevels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["Fighter"] = 6, ["Wizard"] = 4 }
                                     }) == 3
                                     && alertStats.Initiative == 7
                                     && toughStats.HitPoints == 44
                                     && mageArmourStats.ArmourClass == 15
                                     && blessStats.SavingThrowBonusDie == 4
                                     && blessStats.Threats[0].SpellEffectChance < baselineStats.Threats[0].SpellEffectChance
                                     && mirrorStats.ArmourClass == baselineStats.ArmourClass + 9
                                     && sanctuaryStats.Threats.All(threat => threat.AttackHitChance == 0 && threat.SpellAttackHitChance == 0)
                                     && resistanceStats.AttackBonusDie == 0
                                     && resistanceStats.SavingThrowBonusDie == 4;

        var report = new
        {
            Passed = items.Count == 556 && uniqueProgressKeys == items.Count && loadedImages == items.Count && progressRoundTrip && characterRoundTrip && displacementMathApplied && difficultyMathApplied && worstCaseThreatsApplied && naturalRollBoundsApplied && multiclassMathApplied && featAndBuffMathApplied && FontManager.IsAlegreyaLoaded,
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
            WorstCaseThreatsApplied = worstCaseThreatsApplied,
            NaturalRollBoundsApplied = naturalRollBoundsApplied,
            MulticlassMathApplied = multiclassMathApplied,
            FeatAndBuffMathApplied = featAndBuffMathApplied,
            EmbeddedAlegreyaLoaded = FontManager.IsAlegreyaLoaded
        };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    }
}
