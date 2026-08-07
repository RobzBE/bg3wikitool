using System.Text.Json;

namespace BG3ItemExplorer;

internal static class AppDiagnostics
{
    public static async Task WriteSaveImportReportAsync(List<ItemRecord> items, string savePath, string reportPath)
    {
        var imported = await SaveGameService.ImportAsync(savePath, items);
        var report = new
        {
            imported.SavePath,
            imported.SaveName,
            imported.WriteUtc,
            imported.MatchedPresentItems,
            imported.MatchedItems,
            PresentKeys = imported.PresentKeys.OrderBy(value => value),
            imported.Warnings,
            Characters = imported.Characters.Select(character => new
            {
                character.Name,
                character.Race,
                character.StartingClass,
                character.Subclass,
                character.Level,
                character.IsMulticlass,
                character.ClassLevels,
                character.Subclasses,
                character.Abilities,
                character.EquippedKeys
            })
        };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    }

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
        var templatesRoundTrip = false;
        var saveLinkRoundTrip = false;
        var saveDiscoveryApplied = false;
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
                Subclasses = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Wizard"] = "Evocation School" },
                FightingStyles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Fighter"] = "Defence" },
                Feats = [new FeatSelection { Name = "Ability Improvement", Choice = "INT +2" }],
                ActiveBuffs = ["Mage Armour"],
                PermanentBonuses = [new PermanentBonusSelection { Name = "Auntie Ethel's Hair", Choice = "INT" }]
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
                                 && loadedState.Character.GetSubclass("Wizard") == "Evocation School"
                                 && loadedState.Character.FightingStyles.GetValueOrDefault("Fighter") == "Defence"
                                 && loadedState.Character.Feats.Any(feat => feat.Name == "Ability Improvement" && feat.Choice == "INT +2")
                                 && loadedState.Character.HasBuff("Mage Armour")
                                 && loadedState.Character.HasPermanentBonus("Auntie Ethel's Hair")
                                 && loadedState.Character.PermanentBonusChoice("Auntie Ethel's Hair") == "INT"
                                 && !string.IsNullOrWhiteSpace(loadedState.Character.TemplateId);
            var templates = Enumerable.Range(1, 4).Select(index => new CharacterState { Name = $"Test Hero {index}" }).ToList();
            items[1].Equipped = true;
            var localSaveLink = new SaveLinkState
            {
                WatchDirectory = Path.Combine(progressDirectory, "Story"),
                LinkedSavePath = Path.Combine(progressDirectory, "Story", "QuickSave_1", "QuickSave_1.lsv"),
                AutoSync = true,
                LastImportedWriteUtc = new DateTime(2026, 8, 7, 12, 0, 0, DateTimeKind.Utc)
            };
            store.Save(items, templates, 2, localSaveLink);
            var loadedTemplates = store.LoadState();
            templatesRoundTrip = loadedTemplates.Characters.Count == 4
                                 && loadedTemplates.ActiveCharacterIndex == 2
                                 && loadedTemplates.Characters.Select(character => character.Name).SequenceEqual(["Test Hero 1", "Test Hero 2", "Test Hero 3", "Test Hero 4"])
                                 && loadedTemplates.Characters[2].EquippedKeys.Contains(items[1].ProgressKey);
            saveLinkRoundTrip = loadedTemplates.SaveLink.LinkedSavePath == localSaveLink.LinkedSavePath
                                && loadedTemplates.SaveLink.WatchDirectory == localSaveLink.WatchDirectory
                                && loadedTemplates.SaveLink.AutoSync;

            var story = Path.Combine(progressDirectory, "discovery", "Story");
            var manual = Path.Combine(story, "Campaign-1", "Manual.lsv");
            var quick = Path.Combine(story, "QuickSave_1", "Quick.lsv");
            var auto = Path.Combine(story, "AutoSave_1", "Auto.lsv");
            Directory.CreateDirectory(Path.GetDirectoryName(manual)!);
            Directory.CreateDirectory(Path.GetDirectoryName(quick)!);
            Directory.CreateDirectory(Path.GetDirectoryName(auto)!);
            File.WriteAllBytes(manual, [1]);
            File.WriteAllBytes(quick, [2]);
            File.WriteAllBytes(auto, [3]);
            File.SetLastWriteTimeUtc(manual, new DateTime(2026, 8, 7, 10, 0, 0, DateTimeKind.Utc));
            File.SetLastWriteTimeUtc(quick, new DateTime(2026, 8, 7, 11, 0, 0, DateTimeKind.Utc));
            File.SetLastWriteTimeUtc(auto, new DateTime(2026, 8, 7, 12, 0, 0, DateTimeKind.Utc));
            saveDiscoveryApplied = SaveGameService.FindNewestSupportedSave(story) == quick
                                   && SaveGameService.FindWatchDirectory(quick) == story
                                   && SaveGameService.SaveKind(quick) == "QuickSave";
            items[0].Found = false;
            items[1].Equipped = false;
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
                                      && baselineStats.Threats[2].AttackEnemy == "Apostle of Myrkul"
                                      && baselineStats.Threats[4].AttackEnemy == "Dominated Red Dragon"
                                      && baselineStats.Threats[4].SpellEnemy == "Netherbrain"
                                      && baselineStats.Threats[4].SpellDc == 23;
        var averageThreatsApplied = baselineStats.Threats.Count == 6
                                    && baselineStats.Threats[1].Benchmark == "Average"
                                    && baselineStats.Threats[1].AttackBonus == 5
                                    && baselineStats.Threats[3].AttackBonus == 7
                                    && baselineStats.Threats[5].AttackBonus == 10
                                    && tacticianStats.Threats[1].AttackBonus == baselineStats.Threats[1].AttackBonus + 2;
        var abilitySaveChancesApplied = baselineStats.Threats.All(threat =>
                                            threat.SpellEffectChances.Count == 6
                                            && threat.CharacterSpellEffectChances.Count == 6
                                            && CharacterCalculator.AbilityNames.All(ability => threat.SpellEffectChances.ContainsKey(ability)
                                                && threat.CharacterSpellEffectChances.ContainsKey(ability)))
                                        && baselineStats.Threats[0].SpellEffectChances["DEX"]
                                           != baselineStats.Threats[0].SpellEffectChances["CON"];
        var characterBenchmarksApplied = baselineStats.Threats[0].TargetEnemy == "Grym"
                                         && baselineStats.Threats[0].TargetArmourClass == 18
                                         && baselineStats.Threats[0].CharacterWeaponHitChance == 40
                                         && baselineStats.Threats[0].CharacterSpellAttackHitChance == 25
                                         && baselineStats.Threats[4].CharacterSpellEffectChances["DEX"]
                                            == CharacterCalculator.SavingThrowFailureChance(6, baselineStats.SpellSaveDc, 0, true, false);
        var naturalRollBoundsApplied = CharacterCalculator.AttackHitChance(100, 0) == 5
                                       && CharacterCalculator.AttackHitChance(100, 0, criticalHitImmune: true) == 0
                                       && CharacterCalculator.AttackHitChance(-100, 0) == 95
                                       && CharacterCalculator.ApplyRollMode(5, false, true) == 0.25
                                       && CharacterCalculator.ApplyRollMode(95, true, false) == 99.75
                                       && CharacterCalculator.AttackHitChanceWithDice(100, 0, 0, false, false, 14) == 35
                                       && CharacterCalculator.AttackHitChanceWithDice(18, 5, 1, false, false) > 40;
        var savingThrowRulesApplied = CharacterCalculator.SavingThrowFailureChance(100, 15, 0, false, false) == 0
                                      && CharacterCalculator.SavingThrowFailureChance(-100, 15, 0, false, false) == 100
                                      && CharacterCalculator.SavingThrowFailureChance(0, 11, 0, false, false) == 50
                                      && CharacterCalculator.SavingThrowFailureChance(0, 11, 0, true, false) == 25
                                      && CharacterCalculator.SavingThrowFailureChance(0, 11, 0, false, true) == 75
                                      && CharacterCalculator.SavingThrowFailureChance(0, 11, 2, false, false)
                                         < CharacterCalculator.SavingThrowFailureChance(0, 11, 1, false, false);
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
        var subclassState = new CharacterState
        {
            ClassName = "Wizard",
            SubclassName = "Evocation School",
            Level = 5,
            ClassLevels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["Wizard"] = 2, ["Fighter"] = 3 },
            Subclasses = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Wizard"] = "Evocation School",
                ["Fighter"] = "Champion"
            },
            ActiveBuffs = ["Champion: Improved Critical Hit"]
        };
        var subclassStats = CharacterCalculator.Calculate(subclassState, items);
        var subclassOptionsApplied = BuildOptions.SubclassesByClass.Count == 12
                                     && BuildOptions.SubclassesByClass["Fighter"].Contains("Champion")
                                     && subclassState.GetSubclass("Wizard") == "Evocation School"
                                     && BuildOptions.AvailableClassOptions(subclassState).Any(option => option.BuffName == "Champion: Improved Critical Hit")
                                     && subclassStats.CriticalThreshold == 19;

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
        var permanentState = new CharacterState
        {
            TemplateId = "0123456789abcdef0123456789abcdef",
            Name = "Share Test",
            ClassName = "Fighter",
            Level = 4,
            ClassLevels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["Fighter"] = 4 },
            PermanentBonuses =
            [
                new PermanentBonusSelection { Name = "Auntie Ethel's Hair", Choice = "STR" },
                new PermanentBonusSelection { Name = "Potion of Everlasting Vigour" },
                new PermanentBonusSelection { Name = "Mirror of Loss", Choice = "STR" },
                new PermanentBonusSelection { Name = "Patriar's Memory" },
                new PermanentBonusSelection { Name = "Forbidden Knowledge" },
                new PermanentBonusSelection { Name = "Anointed in Splendour" },
                new PermanentBonusSelection { Name = "Sweet Stone Features" },
                new PermanentBonusSelection { Name = "The Tharchiate Codex: Blessing" }
            ]
        };
        var permanentStats = CharacterCalculator.Calculate(permanentState, items);
        var permanentBonusMathApplied = PermanentBonusCatalog.All.Length >= 35
                                        && permanentStats.Abilities["STR"] == 21
                                        && permanentStats.Abilities["CHA"] == 11
                                        && permanentStats.Saves["WIS"] == 3
                                        && permanentStats.AttackBonusD4Count == 1
                                        && permanentStats.SavingThrowBonusD4Count == 1
                                        && permanentStats.TemporaryHitPoints == 20;
        permanentState.EquippedKeys = [items[0].ProgressKey];
        permanentState.ActiveBuffs = ["Bless"];
        var shareLink = TemplateShareService.ExportLink(permanentState);
        var importedTemplate = TemplateShareService.Import(shareLink);
        var templateSharingApplied = shareLink.Contains("#template=BG3T1.", StringComparison.Ordinal)
                                     && importedTemplate.TemplateId == permanentState.TemplateId
                                     && importedTemplate.Name == permanentState.Name
                                     && importedTemplate.SubclassName == permanentState.SubclassName
                                     && importedTemplate.EquippedKeys.SequenceEqual(permanentState.EquippedKeys)
                                     && importedTemplate.HasBuff("Bless")
                                     && importedTemplate.PermanentBonusChoice("Mirror of Loss") == "STR";
        try
        {
            _ = TemplateShareService.Import("BG3T1.invalid");
            templateSharingApplied = false;
        }
        catch (Exception exception) when (exception is FormatException or InvalidDataException or JsonException)
        {
            // Expected: malformed share data must never be accepted.
        }
        var testShields = items.Where(item => item.Type.Equals("Shield", StringComparison.OrdinalIgnoreCase)).Take(2).ToList();
        var gearCharacterOne = new CharacterState { Name = "Gear One" };
        var gearCharacterTwo = new CharacterState { Name = "Gear Two" };
        GearRules.EquipForCharacter(items, gearCharacterOne, testShields[0]);
        GearRules.EquipForCharacter(items, gearCharacterOne, testShields[1]);
        GearRules.EquipForCharacter(items, gearCharacterTwo, testShields[0]);
        var templateGearSetsApplied = gearCharacterOne.EquippedKeys.Count == 1
                                      && gearCharacterOne.EquippedKeys.Contains(testShields[1].ProgressKey)
                                      && gearCharacterTwo.EquippedKeys.Count == 1
                                      && gearCharacterTwo.EquippedKeys.Contains(testShields[0].ProgressKey);

        var deadShot = items.First(item => item.Name == "The Dead Shot");
        deadShot.Equipped = true;
        var criticalStats = CharacterCalculator.Calculate(
            new CharacterState
            {
                ClassName = "Fighter",
                Level = 4,
                ClassLevels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["Fighter"] = 4 },
                Feats = [new FeatSelection { Name = "Spell Sniper" }]
            },
            items);
        deadShot.Equipped = false;
        var criticalMathApplied = criticalStats.CriticalThreshold == 19
                                  && criticalStats.SpellCriticalThreshold == 18
                                  && criticalStats.ActiveEffects.Any(effect => effect.Kind == ItemEffectKind.CriticalThresholdReduction)
                                  && criticalStats.CriticalBreakdown.Contains("The Dead Shot", StringComparison.OrdinalIgnoreCase)
                                  && criticalStats.CriticalBreakdown.Contains("Spell Sniper", StringComparison.OrdinalIgnoreCase);
        var parsedCriticalGear = items.SelectMany(ItemEffectParser.Parse)
            .Where(effect => effect.Kind == ItemEffectKind.CriticalThresholdReduction)
            .ToList();
        var criticalGearParsingApplied = parsedCriticalGear.Count == 9
                                         && parsedCriticalGear.Any(effect => effect.ItemName == "The Dead Shot" && !effect.Conditional)
                                         && parsedCriticalGear.Any(effect => effect.ItemName == "Shade-Slayer Cloak" && effect.Conditional)
                                         && parsedCriticalGear.Any(effect => effect.ItemName == "Unseen Menace" && effect.Conditional);
        var criticalLoadoutItems = new[]
        {
            deadShot,
            items.First(item => item.Name == "Bloodthirst"),
            items.First(item => item.Name == "Sarevok's Horned Helmet"),
            items.First(item => item.Name == "Shade-Slayer Cloak")
        };
        var criticalLoadout = new CharacterState
        {
            ClassName = "Fighter",
            SubclassName = "Champion",
            Level = 3,
            ClassLevels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["Fighter"] = 3 },
            ActiveBuffs = ["Champion: Improved Critical Hit", "Elixir of Viciousness"]
        };
        foreach (var item in criticalLoadoutItems)
        {
            GearRules.EquipForCharacter(items, criticalLoadout, item);
            item.Equipped = true;
        }
        var shadeEffect = ItemEffectParser.Parse(criticalLoadoutItems[3])
            .Single(effect => effect.Kind == ItemEffectKind.CriticalThresholdReduction);
        criticalLoadout.EnabledConditionalEffects.Add(shadeEffect.Id);
        var thresholdFourteenStats = CharacterCalculator.Calculate(criticalLoadout, items);
        foreach (var item in criticalLoadoutItems)
            item.Equipped = false;
        var thresholdFourteenApplied = criticalLoadout.EquippedKeys.Count == 4
                                       && GearRules.SlotFor(deadShot) == "Ranged"
                                       && thresholdFourteenStats.CriticalThreshold == 14;
        var calculationsExplained = baselineStats.ArmourClassBreakdown.Contains($"= {baselineStats.ArmourClass}", StringComparison.Ordinal)
                                    && baselineStats.SpellSaveDcBreakdown.Contains($"= {baselineStats.SpellSaveDc}", StringComparison.Ordinal)
                                    && baselineStats.ArmourClassBreakdown.Length > 20
                                    && baselineStats.SpellSaveDcBreakdown.Length > 20;

        var report = new
        {
            Passed = items.Count == 556 && uniqueProgressKeys == items.Count && loadedImages == items.Count && progressRoundTrip && characterRoundTrip && templatesRoundTrip && saveLinkRoundTrip && saveDiscoveryApplied && templateGearSetsApplied && displacementMathApplied && difficultyMathApplied && worstCaseThreatsApplied && averageThreatsApplied && abilitySaveChancesApplied && characterBenchmarksApplied && naturalRollBoundsApplied && savingThrowRulesApplied && multiclassMathApplied && subclassOptionsApplied && featAndBuffMathApplied && permanentBonusMathApplied && templateSharingApplied && criticalMathApplied && criticalGearParsingApplied && thresholdFourteenApplied && calculationsExplained && FontManager.IsAlegreyaLoaded,
            ItemCount = items.Count,
            ActCounts = items.GroupBy(item => item.Act).ToDictionary(group => group.Key, group => group.Count()),
            UniqueProgressKeys = uniqueProgressKeys,
            LoadedImages = loadedImages,
            ItemsWithNameLinks = items.Count(item => item.Links.ContainsKey("Name")),
            ItemsWithNotes = items.Count(item => item.Notes.Count > 0),
            ProgressRoundTrip = progressRoundTrip,
            CharacterRoundTrip = characterRoundTrip,
            TemplatesRoundTrip = templatesRoundTrip,
            SaveLinkRoundTrip = saveLinkRoundTrip,
            SaveDiscoveryApplied = saveDiscoveryApplied,
            TemplateGearSetsApplied = templateGearSetsApplied,
            DisplacementMathApplied = displacementMathApplied,
            BaselineAct1AttackChance = baselineStats.Threats[0].AttackHitChance,
            CloakAct1AttackChance = displacedStats.Threats[0].AttackHitChance,
            DifficultyMathApplied = difficultyMathApplied,
            WorstCaseThreatsApplied = worstCaseThreatsApplied,
            AverageThreatsApplied = averageThreatsApplied,
            AbilitySaveChancesApplied = abilitySaveChancesApplied,
            CharacterBenchmarksApplied = characterBenchmarksApplied,
            NaturalRollBoundsApplied = naturalRollBoundsApplied,
            SavingThrowRulesApplied = savingThrowRulesApplied,
            MulticlassMathApplied = multiclassMathApplied,
            SubclassOptionsApplied = subclassOptionsApplied,
            FeatAndBuffMathApplied = featAndBuffMathApplied,
            PermanentBonusMathApplied = permanentBonusMathApplied,
            TemplateSharingApplied = templateSharingApplied,
            CriticalMathApplied = criticalMathApplied,
            CriticalGearParsingApplied = criticalGearParsingApplied,
            CriticalGearSources = parsedCriticalGear.Count,
            ThresholdFourteenApplied = thresholdFourteenApplied,
            CriticalWeaponThreshold = criticalStats.CriticalThreshold,
            CriticalSpellThreshold = criticalStats.SpellCriticalThreshold,
            CriticalReductionEffects = criticalStats.ActiveEffects.Count(effect => effect.Kind == ItemEffectKind.CriticalThresholdReduction),
            CriticalBreakdown = criticalStats.CriticalBreakdown,
            CalculationBreakdownsApplied = calculationsExplained,
            EmbeddedAlegreyaLoaded = FontManager.IsAlegreyaLoaded
        };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    }
}
