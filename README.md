# Valheim Mods

A library of Valheim mods I've made.

## Mods

| Mod | Description | Nexus Mods | Thunderstore |
| --- | --- | --- | --- |
| [Chest Peek](ChestPeek) | Hover over a chest to see what's inside without opening it, and spot full or empty chests at a glance. | [Mod 4341](https://www.nexusmods.com/valheim/mods/4341) | [Link](https://thunderstore.io/c/valheim/p/Kriona/ChestPeek/) |
| [Craft More](CraftMore) | Hold Ctrl to craft a full stack, or Alt to craft as many as you have the materials for. Gain experience for each craft rather than for each batch. | [Mod 4427](https://www.nexusmods.com/valheim/mods/4427) | [Link](https://thunderstore.io/c/valheim/p/Kriona/CraftMore/) |
| [Creature Marker](CreatureMarker) | Puts a colored arrow above nearby creatures, traders, fish, birds, leviathans and other players, or at the edge of the screen when they're off-screen, with optional name, star level and distance, and a matching dot on the map. | [Mod 4342](https://www.nexusmods.com/valheim/mods/4342) | [Link](https://thunderstore.io/c/valheim/p/Kriona/CreatureMarker/) |
| [Light Color](LightColor) | Press L on an object that gives off light (e.g., sconce, campfire, portal, mounted Dverger circlet) to set its color, brightness, and range. Also makes sign text glow with a colored halo, lit letters or an outline. Servers can limit it to admins. | [Mod 4352](https://www.nexusmods.com/valheim/mods/4352) | [Link](https://thunderstore.io/c/valheim/p/Kriona/LightColor/) |
| [Quick Fill](QuickFill) | Hold Shift and press the use key (E) to fill a station (e.g., smelter, kiln, oven, fire) in one go. Mouse targets on stations are also larger. | [Mod 4343](https://www.nexusmods.com/valheim/mods/4343) | [Link](https://thunderstore.io/c/valheim/p/Kriona/QuickFill/) |
| [Safe Sneak](SafeSneak) | Stops you from walking off ledges while you're sneaking (like in Minecraft). | [Mod 4866](https://www.nexusmods.com/valheim/mods/4866) | [Link](https://thunderstore.io/c/valheim/p/Kriona/SafeSneak/) |
| [Snap Points](SnapPoints) | Gives chests, resource stacks, tables, benches, banners, armor stands, item stands and portals snap points. Any other build piece can be given snap points (or have its snap points taken away) via config. | [Mod 4370](https://www.nexusmods.com/valheim/mods/4370) | [Link](https://thunderstore.io/c/valheim/p/Kriona/SnapPoints/) |
| [Swap Gear](SwapGear) | Swap what you're wearing with what's on an armor stand. | [Mod 4344](https://www.nexusmods.com/valheim/mods/4344) | [Link](https://thunderstore.io/c/valheim/p/Kriona/SwapGear/) |
| [Too Low](TooLow) | Shows a "Too low" message when your pickaxe hits the 8m digging limit. | [Mod 4345](https://www.nexusmods.com/valheim/mods/4345) | [Link](https://thunderstore.io/c/valheim/p/Kriona/TooLow/) |
| [Tweak Stats](TweakStats) | Change the stats of any item, recipe, creature, build piece or status effect - weapon damage, attack stamina, armor, block power, food, durability, weight, stack sizes, crafting costs, creature health and drops, building health and more. | [Mod 4363](https://www.nexusmods.com/valheim/mods/4363) | [Link](https://thunderstore.io/c/valheim/p/Kriona/TweakStats/) |
| [Weight Display](WeightDisplay) | Shows your current and maximum carry weight, followed by how many inventory slots are in use, just below the minimap. | [Mod 4346](https://www.nexusmods.com/valheim/mods/4346) | [Link](https://thunderstore.io/c/valheim/p/Kriona/WeightDisplay/) |
| [Who's Online](WhosOnline) | Lists the other players on the server when you join in a popup and in chat. | [Mod 4347](https://www.nexusmods.com/valheim/mods/4347) | [Link](https://thunderstore.io/c/valheim/p/Kriona/WhosOnline/) |

## Download

Install with a mod manager such as r2modman or the Thunderstore Mod Manager from each mod's Thunderstore page, or download from [Releases](../../releases) or each mod's Nexus Mods page. Each mod is tagged and released separately, e.g. `ModName-v1.0.0`, with two downloads:

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
.\build-release.ps1 ModName -GitHubOnly
.\build-release.ps1 ModName -ThunderstoreOnly
.\build-release.ps1 ModName -NexusOnly
```

Builds the mod, with the version read from its `Plugin.cs`, and writes two zips:

- `release\packages\<Mod>-<Version>.zip` - for GitHub, Nexus Mods and installing by hand
- `release\thunderstore\Kriona-<Mod>-<Version>.zip` - the Thunderstore package, with a `manifest.json` (its description from the table above, and a dependency on BepInExPack for Valheim), the mod's `icon.png`, its `README.md` with images and links pointing at GitHub, and its `CHANGELOG.md`. When a Thunderstore token is set, the manifest and icon are checked with Thunderstore's validators

`-Publish` then publishes the version to every site - commit and push first:

- GitHub - a release tagged `<Mod>-v<Version>` from the current commit with the zip and the DLL attached, using that version's section of the mod's `CHANGELOG.md` as the notes. The mod's older releases are removed, keeping their tags, so the Releases page only lists the newest version of each mod - older downloads stay on Nexus Mods and Thunderstore
- Thunderstore - the Thunderstore zip, with the categories in `<Mod>\thunderstore-categories.txt` (one slug per line) plus the current game update's and `ai-generated`, which are set in `build-release.ps1`
- Nexus Mods - the zip as a new version of the mod's main file, moving the old version to Old Files, with the changelog section added. Every mod's summary (its description in the table above) and description are then pushed if they've changed since the last push

`-GitHubOnly`, `-ThunderstoreOnly` and `-NexusOnly` publish to just that site - e.g. to release on GitHub while another site's API is down, then catch that site up later. A site that already has the version is skipped, so a failed run can be repeated, and secrets, changelog, categories and Nexus Mods IDs are all checked before anything is published.

Publishing needs `gh` signed in and these in the environment or a `.env` file in this folder (it's gitignored):

```
# A service account token from the Kriona team's settings on Thunderstore
THUNDERSTORE_TOKEN=...
# A personal API key from https://www.nexusmods.com/settings/api-keys
NEXUS_API_KEY=...
```

```
.\build-bbcode.ps1
.\build-bbcode.ps1 -Publish
```

Converts every mod's `README.md` to `release\bbcode\<Mod>.bbcode` for its Nexus Mods description, with images loaded from GitHub and a link to the mod's source and the "Other Mods I Use" list below added at the end. Every mod is converted each time, since a new mod's Nexus link goes in every other mod's description - `build-release.ps1` runs it too. `-Publish` pushes each mod's summary and description to Nexus Mods when they've changed, without a release - e.g. after editing a README or adding a mod's Nexus Mods link.

## Other Mods I Use

- [Build Camera](https://old.thunderstore.io/c/valheim/p/Azumatt/Build_Camera_Custom_Hammers_Edition/) by Azumatt
- [Clock](https://www.nexusmods.com/valheim/mods/85) by aedenthorn
- [Quiet Portals](https://www.nexusmods.com/valheim/mods/1981) by Neobotics
- [Remember Server Password](https://old.thunderstore.io/c/valheim/p/MagiCorp/RememberServerPassword/) by MagiCorp
- [Tools in Water](https://www.nexusmods.com/valheim/mods/3771) by Lilizzll
