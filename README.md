# Valheim Mods

A library of Valheim mods I've made.

## Mods

| Mod | Description | Nexus Mods |
| --- | --- | --- |
| [Chest Peek](ChestPeek) | Hover over a chest to see what's inside without opening it | [Mod 4341](https://www.nexusmods.com/valheim/mods/4341) |
| [Creature Marker](CreatureMarker) | Puts a colored arrow above nearby creatures, or at the edge of the screen when they're off-screen | [Mod 4342](https://www.nexusmods.com/valheim/mods/4342) |
| [Light Color](LightColor) | Press L while looking at a sconce, mounted Dverger circlet, portal or other light to set its color, brightness and range. It also works on a worn Dverger circlet | [Mod 4352](https://www.nexusmods.com/valheim/mods/4352) |
| [Quick Fill](QuickFill) | Hold Shift and press E to fill a smelter, kiln, oven or fire in one go | [Mod 4343](https://www.nexusmods.com/valheim/mods/4343) |
| [Swap Gear](SwapGear) | Swap what you're wearing with what's on an armor stand | [Mod 4344](https://www.nexusmods.com/valheim/mods/4344) |
| [Too Low](TooLow) | Shows a "Too low" message when your pickaxe hits the 8m digging limit | [Mod 4345](https://www.nexusmods.com/valheim/mods/4345) |
| [Weight Display](WeightDisplay) | Shows your current and maximum carry weight under the minimap | [Mod 4346](https://www.nexusmods.com/valheim/mods/4346) |
| [Who's Online](WhosOnline) | Lists the other players on the server when you join | [Mod 4347](https://www.nexusmods.com/valheim/mods/4347) |

## Download

Download from [Releases](../../releases) or from each mod's Nexus Mods page. Each mod is tagged and released separately, e.g. `ModName-v1.0.0`, with two downloads:

- `<Mod>.dll` - copy it into `BepInEx\plugins`
- `<Mod>-<Version>.zip` - for mod managers, or extract it into your Valheim folder

## Building

Requires the .NET SDK and Valheim with [BepInExPack for Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) installed - the projects reference the game's and BepInEx's DLLs from the install folder.

```
dotnet build Valheim.slnx -c Release
```

If Valheim isn't installed at `C:\Program Files (x86)\Steam\steamapps\common\Valheim`, pass the path with `-p:ValheimPath="D:\Games\Valheim"` or set a `ValheimPath` environment variable.

Each DLL is written to `<Mod>\bin\Release\netstandard2.1\`.

## Releasing

```
.\build-release.ps1 ModName
.\build-release.ps1 ModName -Publish
```

Builds the mod and writes `release\<Mod>-<Version>.zip`, with the version read from the mod's `Plugin.cs`. `-Publish` also creates a GitHub release tagged `<Mod>-v<Version>` from the current commit with the zip and the DLL attached, using that version's section of the mod's `CHANGELOG.md` as the notes - commit and push first.

```
.\build-bbcode.ps1
```

Converts every mod's `README.md` to `release\<Mod>.bbcode` for pasting into its Nexus Mods description, with images loaded from GitHub and a link to the mod's source added at the end. Every mod is converted each time, since a new mod's Nexus link goes in every other mod's description - `build-release.ps1` runs it too.
