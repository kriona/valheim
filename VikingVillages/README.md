# Viking Villages

NOTE: this mod has not been released, there are no existing villages, and breaking changes just mean I need to delete my test worlds.

The Meadows' abandoned farms aren't abandoned anymore. Living Vikings move in, keep their village in repair, go about their lives, fight off whatever comes for them and come back after they die.

Viking Villages works on its own, and with [Zombie Apocalypse](https://thunderstore.io/c/valheim/p/Kriona/ZombieApocalypse/) the two know about each other - see With Zombie Apocalypse below.

This page gives a short tour of each feature. Every detail of how it works is in [LOGIC.md](LOGIC.md), and every job the Vikings do, in the order they pick them, is in [JOBS.md](JOBS.md). How living Vikings, Dvergr Vikings and Zombie Apocalypse's zombie Vikings compare is in [VIKINGS.md](VIKINGS.md).

## Villages

- Every farm in the Meadows becomes a village, home to 2 living Vikings for each farmhouse. Farms come in a range of sizes, bigger near harder biomes, and are built around their oaks rather than clearing them (see [Farms](LOGIC.md#farms))
- Its houses are fixed up and furnished - beds, chests stocked with food and supplies, tables, and a workbench and forge upgraded for the village's biome - and the abandoned houses nearby are taken in, as homes or as outposts. Half the farmhouses can be built from your own PlanBuild blueprints (see [Houses, Core and Outposts](LOGIC.md#houses-core-and-outposts) and [Furnishing](LOGIC.md#furnishing))
- The ground is flattened and dirt paths join the houses, crossing rivers on bridges, and run on to any village close by (see [Ground, Paths and Bridges](LOGIC.md#ground-paths-and-bridges))
- A bonfire burns in the middle of the village, with cooking stations, a cauldron and sitting logs around it, pushing back the fog (see [The Fire](LOGIC.md#the-fire))
- A village from the Black Forest on keeps a cart that only its Vikings pull, parked beside a house - fetched home when it's left outside, and built again when it's lost (see [The Cart](LOGIC.md#the-cart))
- A village by the sea has a boat moored that's yours to take, a village in the Ashlands or the Deep North has a portal home to an easier one, and a village in the Mistlands is lit against the mist (see [Boats](LOGIC.md#boats), [Portals Home](LOGIC.md#portals-home) and [Lights Against the Mist](LOGIC.md#lights-against-the-mist))
- The boars in its pens are the Vikings' tame livestock, which help defend the village (see [Livestock](LOGIC.md#livestock))
- Each village has a Norse name of its own, pinned on your map and shown as you walk in, and you can rename it (see [Village Names](LOGIC.md#village-names))
- A village counts as a player base - no random spawns inside it, and nothing dropped there despawns - and you can show how far it reaches as rings on the ground (see [Village Rings](LOGIC.md#village-rings))

## Living Vikings

- Each is a Viking of their village's biome - from the Meadows' rags and flint up to the Deep North's best gear, the harder the biome the likelier a strength belt and Haldor's wider and deeper pockets - and their skills rise as they use them, the way yours do (see [Who They Are](LOGIC.md#who-they-are), [Skills](LOGIC.md#skills), [Gear](LOGIC.md#gear) and [PROGRESSION.md](PROGRESSION.md))
- They have Norse names of their own, each village has a leader, and you can rename them (see [Names](LOGIC.md#names))
- They greet you as you arrive, talk to you and each other, remember you, and become old friends over time (see [Meeting and Talking](LOGIC.md#meeting-and-talking) and [DIALOG.md](DIALOG.md))
- Open a Viking's inventory to hand them gear, take it back, and see their skills, health and status effects (see [Their Inventory](LOGIC.md#their-inventory))
- A stray hit with PvP off doesn't turn the village on you, and a Viking you angered forgives you once they've slept on it (see [Hurting a Viking](LOGIC.md#hurting-a-viking))
- A Viking who dies comes back in time, so a village wiped out fills up again (see [Coming Back and Joining](LOGIC.md#coming-back-and-joining))

## Viking AI

- They fight the zombies and monsters that threaten them rather than everything they see, and rush to help anyone attacked nearby (see [Picking Fights](LOGIC.md#picking-fights))
- They don't fight a losing fight: a Viking outnumbered away from home runs back for help, and one badly hurt hangs back and strikes from behind (see [Away From Home](LOGIC.md#away-from-home) and [Hanging Back](LOGIC.md#hanging-back))
- They pick the weapon that hurts each enemy most, switching between bow and melee as a fight goes, and use power attacks when they're a smart move - a Viking with an atgeir keeps enemies staggered for the others (see [Weapons](LOGIC.md#weapons))
- They gang up on monsters too tough for one of them, luring and shooting them until enough have gathered to charge, and leave roots to the archers (see [Tough Monsters and Roots](LOGIC.md#tough-monsters-and-roots))
- They keep out of flames and off deadly drops, open doors, jump fences, keep off roofs and get themselves unstuck (see [Hazards](LOGIC.md#hazards) and [Getting Around](LOGIC.md#getting-around))
- They carry torches at night, get wet and cold like you, eat and rest to heal, gather at the fire at dusk, sleep in their beds and keep a night watch (see [Torches and Circlets](LOGIC.md#torches-and-circlets), [Weather](LOGIC.md#weather), [Eating](LOGIC.md#eating) and [Dusk and Night](LOGIC.md#dusk-and-night))
- They form up around the fire when a raid comes, and mourn their dead (see [Raids](LOGIC.md#raids) and [The Dead](LOGIC.md#the-dead))
- They use the forsaken powers they know, get yours when you use one near them, and teach them to each other (see [Forsaken Powers](LOGIC.md#forsaken-powers))
- Each biome changes how they get by - they keep off the Ashlands' lava, see only a little way in the Mistlands' mist, warm up by the fire in the Mountains' frost and hunt in parties from the Plains on (see [By Biome](LOGIC.md#by-biome))

## Village Work

- They repair and rebuild their village, gathering wood and cutting trees for it (see [Repairing and Rebuilding](LOGIC.md#repairing-and-rebuilding) and [Wood](LOGIC.md#wood))
- They pick up loot and keep their chests tidy (see [Loot and Chests](LOGIC.md#loot-and-chests))
- They cook what they hunt, and make meals at the cauldron (see [Cooking](LOGIC.md#cooking))
- They stalk and hunt prey, forage and keep bees (see [Hunting](LOGIC.md#hunting) and [Foraging and Bees](LOGIC.md#foraging-and-bees))
- They craft arrows, torches, tools, weapons and armor, repair and upgrade their gear, and smelt ore toward the next piece of gear the village needs (see [Crafting and Gear](LOGIC.md#crafting-and-gear) and [The Village's Goal](LOGIC.md#the-villages-goal))
- What they can't get at home, they go out on quests for, out of sight - hunting, mining, raiding crypts and caves, killing Eikthyr and trading with Haldor for circlets, strength belts and pockets - coming home hurt, or sometimes not at all (see [Quests](LOGIC.md#quests) and [QUESTS.md](QUESTS.md))

## Growing a Village

- A bed you build in a village brings it a new Viking, and a building with beds grows the village to take it in, with a path hoed out to it (see [Beds and Growing the Village](LOGIC.md#beds-and-growing-the-village))
- Start a village of your own at a bonfire you built, and disband it when you're done with it (see [Starting and Disbanding a Village](LOGIC.md#starting-and-disbanding-a-village))
- Make your own fires gathering fires, so a large village spreads out over them at night (see [Gathering Fires](LOGIC.md#gathering-fires))
- See everything in the village's chests at once from its fire (see [Village Stock](LOGIC.md#village-stock))
- The Vikings use the chests you build in their village too, and you can keep them out of any chest you choose (see [Village Chests](LOGIC.md#village-chests))

| Key | Look at | What it does |
| --- | --- | --- |
| E | A living Viking | Talk to them |
| Shift+E | A living Viking | Rename them |
| I | A living Viking | Open their inventory |
| I | A village's bonfire or campfire | Show the village's stock |
| N | A village's bonfire or campfire | Rename the village |
| V | A village's bonfire or campfire | Show or hide the village's rings |
| V | Another fire in a village | Make it a gathering fire, or an ordinary fire again |
| V | A chest in a village | Keep the Vikings out of it, or let them use it again |
| V | A bonfire you built | Start a village |
| Shift+V | The bonfire of a village you started | Disband the village |
| Shift+E | Your own bed | Unclaim it |

## Vikings With No Village

A Viking with no village - one you spawn in the wild, or one leaving a village you disbanded - sets out for the village they can reach soonest, on foot, through portals and across the sea in boats they build, which you can ride along on (see [Vikings With No Village](LOGIC.md#vikings-with-no-village)).

## News

Word of what you do gets around - the bosses you kill, your deaths, your firsts and achievements - and the Vikings of every village pass it on, while the villages hear about each other too (see [News](LOGIC.md#news) and [DIALOG.md](DIALOG.md)).

## Dvergr Vikings

Some Vikings have taken work with the Dvergr in the Mistlands, keeping to their outposts and patrols (see [Dvergr Vikings](LOGIC.md#dvergr-vikings)).

## With Zombie Apocalypse

With [Zombie Apocalypse](https://thunderstore.io/c/valheim/p/Kriona/ZombieApocalypse/) installed too, a Viking who dies rises as a zombie with their name, gear and skills, and the Vikings know it - they stand ready over the dead, keep clear of poison clouds, talk of the dead walking, and line their weapons with silver (see [With Zombie Apocalypse](LOGIC.md#with-zombie-apocalypse)). Any version of Viking Villages works with any version of Zombie Apocalypse.

## Configuration

`BepInEx\config\kriona.VikingVillages.cfg` is created on first run, and changes take effect as soon as the file is saved. You do not need to restart Valheim or rejoin the world.

In multiplayer, every player uses the server's settings while connected to it, and gets its changes as soon as they're saved. A player's own settings come back when they leave the server.

Viking Gear:

- `HelmetChance` - percent chance that a living Viking also wears their armor set's helmet - from the Mountains on, every one wears one (default `50`)
- `CapeChance` - percent chance that a living Viking also wears their armor set's cape - from the Mountains on, every one wears one (default `50`)
- `ShieldChance` - percent chance that a living Viking carries a shield, unless their weapon takes both hands (default `40`)

Dvergr Vikings:

- `GearDropChance` - percent chance that a Dvergr Viking drops each piece of their gear when they die (default `5`)
- `GearDropMax` - most pieces of gear a Dvergr Viking drops (default `2`)

To leave the villages abandoned, uninstall the mod.

## Console Commands

Viking Villages adds console commands for trying it out and testing it - spawning villages and boats, starting quests, sending Vikings off to other villages and logging test runs. Every one of them, and what it does, is listed in [COMMANDS.md](COMMANDS.md). With `debugmode` on, looking at a Viking or a village's bonfire shows what it's doing (see [Debug Mode](LOGIC.md#debug-mode)).

## Installation

With a mod manager, install Viking Villages from [Thunderstore](https://thunderstore.io/c/valheim/p/Kriona/VikingVillages/) using r2modman or the Thunderstore Mod Manager (which install BepInExPack for Valheim).

By hand:

1. Install BepInExPack for Valheim from [Thunderstore](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) or [Nexus Mods](https://www.nexusmods.com/valheim/mods/3605)
2. Extract the zip into your Valheim folder, or copy `VikingVillages.dll` and its `Blueprints` folder into `BepInEx\plugins\VikingVillages`

## Multiplayer

In multiplayer, the server and every player need the same version of the mod. A server with it turns away a player without it, or with another version, and says why. On a server without it, the mod stays off and the player can still join - and its console commands don't work there, and its Vikings and village anchors can't be spawned there, since no one else would have them.

## TODO

- make it so not every village is a Viking village
- dodge skill
- trading interface? instead of "you can do everything with my inventory"
- hiring guards?
- take over cleared draugr villages?
- build new houses
- chieftains
