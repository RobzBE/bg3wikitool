# BG3 Item Explorer

A portable, offline Windows app for the Act 1, Act 2 and Act 3 items in the supplied **BG3 Item Index Cheat Sheet**. English is the default interface language; Dutch is available from the language selector.

## Features

- 20/60/20 layout: search and filters on the left, item table/details in the centre, and a live character sheet on the right.
- Search by name, properties, location, description and notes.
- Filters for act, rarity, type, area/location, notes, collected status and items equipped by the active character template.
- Sort by name, rarity, type, act, location or status.
- Offline preview images for all 556 item records.
- Clickable source links to BG3 Wiki.
- Mark every item as found/collected.
- Equip every item independently from its found status; mutually exclusive gear slots are handled automatically and two rings are allowed.
- Four named character templates preserve their own race/classes, abilities, feats, fighting styles, active effects and equipment. The item grid has one equip checkbox per template, so complete party gearsets can be planned side by side.
- Every template has a self-contained `BG3T1` share ID. **Copy link** exports the entire character and gear setup; **Import** restores it in another copy of the portable tool without requiring a server account.
- Persistent race, starting class, per-class multiclass level distribution, difficulty and level-1 ability scores. Total character level is capped at 12; Explorer automatically keeps the build single-classed.
- Multiclass calculations use total character level for proficiency, the starting class for saving-throw proficiencies and first-level HP, each class for its subsequent HP, and BG3's reduced multiclass armour proficiencies. The displayed spell attack/DC uses the active class with the strongest relevant casting ability.
- A separate Build tab provides class-dependent fighting styles, all 41 BG3 feats, feat choices and feat slots. Slots follow each individual class level (4/8/12), including Fighter 6 and Rogue 10.
- Active spells and combat conditions can be toggled. Concentration is mutually exclusive, upcast variants such as Aid and Magic Weapon are grouped, and unmet prerequisites are shown instead of silently granting a bonus.
- Permanent bonuses from all three acts can be selected, including ability choices and stat effects such as Auntie Ethel's Hair, Potion of Everlasting Vigour, Mirror of Loss, Forbidden Knowledge, Anointed in Splendour, Sweet Stone Features and the Tharchiate Codex blessing.
- Stat-relevant class/feat/spell rules include, among others, Defence and Archery styles, Paladin Aura of Protection, Barbarian/Monk movement, Mage Armour, Shield, Shield of Faith, Blur, Haste, Barkskin, Longstrider, Warding Bond, Heroes' Feast, Aid, Magic Weapon, Bless, Tough, Alert, Resilient, Shield Master and armour-training feats.
- Live offense and defense statistics including AC, Spell Save DC, critical-hit thresholds, HP, initiative, movement and all six saving throws. Hovering AC, Spell DC or Critical shows its complete source-by-source calculation.
- Worst-case and representative-average enemy success chances per act for weapon attacks, spell attacks and DEX/CON/WIS spell effects. Worst case uses Grym (Act 1), the Apostle of Myrkul (Act 2), and the Dominated Red Dragon plus Netherbrain (Act 3). Average uses the act's expected proficiency plus a representative primary attack/casting ability baseline; both pass through the same difficulty, AC, save, advantage and item-effect mathematics.
- Attack-roll probabilities preserve the natural-roll rules: 1 always misses and 20 always hits, unless critical-hit immunity turns the natural 20 into a regular roll that can still miss sufficiently high AC. Ordinary combat saving throws do not automatically fail/succeed on a natural 1/20; the defensive spell-effect simulation follows that distinction.
- Bless, Resistance and Sweet Stone are calculated from their exact d20 plus d4 probability distributions and combine correctly with saving-throw advantage/disadvantage.
- Dynamic item-effect analysis for AC, ability changes, attack/save bonuses, advantage/disadvantage, critical-hit immunity and threshold reductions, damage reduction and resistances.
- Conditional effects can be toggled. For example, Cloak of Displacement automatically applies enemy Disadvantage at the start of a turn and can be disabled after the wearer takes damage.
- Explorer, Balanced, Tactician and Honour calculations, including Explorer proficiency/HP changes and the Tactician/Honour +2 enemy attack/save-DC modifier.
- Portable progress file: `BG3-Item-Explorer-progress.json` is saved beside the exe.
- English and Dutch UI; English is selected on every launch.
- Alegreya is embedded under the SIL Open Font License.
- The supplied Baldur's Gate III image is embedded as the executable and window icon.

## Build

Requires the .NET 10 SDK on the development computer:

```powershell
dotnet build .\BG3ItemExplorer.csproj -c Release
dotnet publish .\BG3ItemExplorer.csproj -c Release -r win-x64 --self-contained true
```

The published single-file executable contains the .NET runtime, item database, images and fonts. The target Windows x64 computer needs no installation or internet connection. Opening a source link does require a browser connection.

## Nederlands

De taal kan linksboven op **Nederlands** worden gezet. Alle zoek-, filter-, sorteer-, voortgangs-, character-sheet- en detailbedieningen worden dan vertaald. De iteminhoud zelf blijft in de oorspronkelijke Engelse brontekst.

## Sources and licensing note

Item data comes from the user-supplied BG3 Item Index Cheat Sheet. Images and outgoing source links come from [BG3 Wiki](https://bg3.wiki/). Dice mathematics and permanent bonus modelling were checked against the BG3 Wiki pages for [Dice rolls](https://bg3.wiki/wiki/Dice_rolls) and [Permanent bonuses](https://bg3.wiki/wiki/Permanent_bonuses). Reuse of wiki material may be governed by CC BY-SA 4.0, CC BY-NC-SA 4.0 and/or applicable fan-content terms; see the [BG3 Wiki copyright policy](https://bg3.wiki/wiki/bg3wiki:Copyrights).

Alegreya and its license file are sourced from the official [Google Fonts repository](https://github.com/google/fonts/tree/main/ofl/alegreya) and distributed under the SIL Open Font License 1.1.

This project is intended as a free, non-commercial fan tool. Baldur's Gate 3 belongs to Larian Studios; Dungeons & Dragons and related marks belong to Wizards of the Coast.
