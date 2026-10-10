# Valheim Mods

Each subdirectory is a BepInEx plugin project built with `dotnet build <Project>.csproj -c Release`, or all of them with `dotnet build Valheim.slnx -c Release`.

## Project Structure
- `Directory.Build.props` in the root holds the settings and references shared by every project - a `.csproj` only lists references beyond those, and the assembly name and namespace come from the project file's name
- Start a new plugin by copying QuickFill, renaming the `.csproj` and renaming QuickFill to the new name in `Plugin.cs` and `.vscode/tasks.json`, then add it with `dotnet sln Valheim.slnx add <Name>\<Name>.csproj` and to the table in `README.md`
- Each mod has a `README.md` (also used as the Nexus Mods description) and a `CHANGELOG.md` with a `## <version>` section per release - `build-release.ps1 <Name>` packages the zip, and `-Publish` uses that section as the GitHub release notes
- Every mod's Nexus Mods description ends with an "Other Mods" list that `build-bbcode.ps1` builds from the root `README.md` table, leaving out mods with no Nexus Mods link - once a new mod's Nexus Mods page exists, add its link to that table, run `build-bbcode.ps1` and paste the new `release\<Mod>.bbcode` into every other mod's Nexus Mods description, or the new mod won't be listed on them
- `Plugin.cs` holds the `BaseUnityPlugin` with the GUID `kriona.<Name>`, the display name and a version starting at `1.0.0`, and calls `harmony.PatchAll()`
- Harmony patches go in a separate `*Patches.cs` file, one static class per patch
- `assembly_valheim` is referenced with `Publicize="true"`, so private game members can be patched and used directly without reflection
- Look up game code by decompiling into the scratchpad: `ilspycmd -t <Class> "C:\Program Files (x86)\Steam\steamapps\common\Valheim\valheim_Data\Managed\assembly_valheim.dll"`
- Look up how a prefab is built - its hierarchy, components, lights, particle colors, materials and textures - with `python -P tools/prefab-dump.py find|tree|materials|particles <name>`, which finds the prefab's asset bundle itself. It needs UnityPy (`python -m pip install --user UnityPy`), and `-P` rather than `-I` so the user install is found
- `TweakStats/NAMES.md` is generated from the game's files by `python -P tools/tweakstats-names.py` - run it again after a game update rather than editing the file

## Line Endings
- All files use LF, set by `.gitattributes` and `.editorconfig`

## Build Output
- Build output stays in each project's `bin\Release\netstandard2.1\` folder
- Don't copy built DLLs into the Valheim `BepInEx\plugins` directory - the user installs mods themselves
- Don't add post-build copy steps or output paths that point at the plugins directory to any `.csproj`
