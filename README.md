# BG3 Item Explorer

A portable, offline Windows app for the Act 1, Act 2 and Act 3 items in the supplied **BG3 Item Index Cheat Sheet**. English is the default interface language; Dutch is available from the language selector.

## Features

- 20/80 layout: search, filters and sorting on the left; item table and full details on the right.
- Search by name, properties, location, description and notes.
- Filters for act, rarity, type, area/location, notes and collected status.
- Sort by name, rarity, type, act, location or status.
- Offline preview images for all 556 item records.
- Clickable source links to BG3 Wiki.
- Mark every item as found/collected.
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

De taal kan linksboven op **Nederlands** worden gezet. Alle zoek-, filter-, sorteer-, voortgangs- en detailbedieningen worden dan vertaald. De iteminhoud zelf blijft in de oorspronkelijke Engelse brontekst.

## Sources and licensing note

Item data comes from the user-supplied BG3 Item Index Cheat Sheet. Images and outgoing source links come from [BG3 Wiki](https://bg3.wiki/). Reuse of wiki material may be governed by CC BY-SA 4.0, CC BY-NC-SA 4.0 and/or applicable fan-content terms; see the [BG3 Wiki copyright policy](https://bg3.wiki/wiki/bg3wiki:Copyrights).

Alegreya and its license file are sourced from the official [Google Fonts repository](https://github.com/google/fonts/tree/main/ofl/alegreya) and distributed under the SIL Open Font License 1.1.

This project is intended as a free, non-commercial fan tool. Baldur's Gate 3 belongs to Larian Studios; Dungeons & Dragons and related marks belong to Wizards of the Coast.
