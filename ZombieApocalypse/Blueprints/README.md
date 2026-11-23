# House Blueprints

Every `.blueprint` file in this folder is a house design that village farms are built with - half of a farm's farmhouses, picked at random, are built from one of these instead. `build-release.ps1` packages the files beside the DLL in `BepInEx\plugins\ZombieApocalypse\Blueprints`, and the mod reads them from there.

The files are in PlanBuild's format, which PlanBuild and Infinity Hammer both save and load.

## Setting Up

1. Install PlanBuild (it needs Jötunn) for a test world - a client with PlanBuild can't join a server without it
2. In `BepInEx\config\marcopogo.PlanBuild.cfg`, turn off "Add player prefix to file name", so a design saves as `farmhouse_small.blueprint` rather than `<Player>_farmhouse_small.blueprint`
3. Link the mod's installed `Blueprints` folder to this one, so the game reads and PlanBuild saves the files here, from a Command Prompt run as administrator:
   ```
   mklink /J "<Valheim>\BepInEx\plugins\ZombieApocalypse\Blueprints" "C:\dev\valheim\ZombieApocalypse\Blueprints"
   ```
4. Set PlanBuild's "Save directory" to that installed folder, `BepInEx/plugins/ZombieApocalypse/Blueprints` - PlanBuild already searches the whole Valheim folder for blueprints, so its blueprint list (the End key) shows these with the rest

## Seeing What's Made

- PlanBuild's blueprint list (End key) or the Blueprints tab on its Blueprint Rune
- The `kri_vv_blueprints` console command, which lists each design with its pieces, size, doors, beds and chests, and anything wrong with it - unknown prefabs, pieces that aren't building pieces, no door (not used) or no bed

## Making a Design

- Build with the game's own pieces only, on flat ground, furniture and all - beds, chests, a table and chairs, a workbench. A design comes with its own furniture rather than being furnished
- Give it at least one door, which it's turned to face the bonfire by - a design with no door isn't used
- Put PlanBuild's center marker on the ground at the doorway, so placing it again to edit lines up the same way. The mod lays a design out around the middle of its pieces with its lowest piece on the ground, wherever its center was
- Select it with the Blueprint Rune and save it with a short name
- Run `kri_vv_blueprints reload` to check it - farms generated after use it

## Changing a Design

1. Place it from the Blueprint Rune, holding Ctrl to build it straight away (admins can, or a server that allows direct building)
2. Change it with the hammer
3. Select it and save it again under the same name

## How It's Built in a Village

- It stands in the farmhouses' ring around the bonfire, on ground that rises and falls no more than a meter under it, at the height of the highest of it
- Its pieces start undamaged, and the Vikings repair and rebuild them like the rest of their village
- It counts as a farmhouse - Vikings live in it, and it counts toward the village's 3 core houses
