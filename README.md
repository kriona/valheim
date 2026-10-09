# Valheim Mods

A library of Valheim mods I've made.

## Download

Download the zips from [Releases](../../releases) or from each mod's Nexus Mods page. Each mod is tagged and released separately, e.g. `ModName-v1.0.0`.

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

Builds the mod and writes `release\<Mod>-<Version>.zip`, with the version read from the mod's `Plugin.cs`. `-Publish` also creates a GitHub release tagged `<Mod>-v<Version>` from the current commit, using that version's section of the mod's `CHANGELOG.md` as the notes - commit and push first.
