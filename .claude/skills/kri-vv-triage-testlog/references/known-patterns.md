# Known patterns

What test runs show that's been looked into and isn't a bug. Triage leaves these out, unless a run shows what an entry says would make it one after all. Each entry says how to recognize it, why it's fine, and when it isn't

## Hitches once a game day, at the same time of day

- **Looks like:** one `Hitch:` warning of roughly 250-600ms in each game day, at the same game time each day to within a minute or two (07:42 in one run, around 08:30 in another), and the snapshot taken then shows a slowest frame of that size. One more as the village is generated at the start of the run. The overview lists every hitch's game time under its snapshots
- **Why it's fine:** it's the game's autosave, every 1800s, which is one game day - so it lands at the same time of day all run long, whichever time that is in a given run
- **An issue when:** hitches come at other times of day, run past a second, or grow longer through the run

## Things given up on for longer and longer

- **Looks like:** `Given up:` lines for the same item or piece at the same spot, leaving it alone for 30, then 60, 90 and 120 minutes
- **Why it's fine:** that's VillageGiveUps working as meant, for things the vikings can't reach - like coins lying on a roof or something across water
- **An issue when:** the thing lies on open ground they walk over (check the spot against other lines there, like `Loot:` reasons or vikings standing close by), or many different things in the same part of the village are given up on, which points at that part being cut off

## Exceptions from other mods

- **Looks like:** an `Exception` with no `VikingVillages.` or `Kriona.` frame in its stack, counted again in every snapshot and filling the totals' `Game: exceptions` - like `MissingFieldException: Field not found: bool .InstanceRenderer.m_frustumCull` from `ValheimMods.Shared.GpuGrass` after a game update
- **Why it's fine:** it isn't this repo's code. Triage lists it under "Outside this repo", so the user knows to update or report that mod
- **An issue when:** a `VikingVillages.` or `Kriona.` frame is anywhere in the stack, even below another mod's

## Vikings waiting at doors

- **Looks like:** `Door:` lines by the hundreds, vikings waiting at a door for another crossing it or standing in the doorway, most of them at the doors of the busiest houses
- **Why it's fine:** vikings take turns at doors, and every wait is written
- **An issue when:** one viking waits at the same door for minutes, two wait on each other, or a viking named as standing in a doorway stays there - and `Stuck:` lines for the vikings waiting there back that up

## An empty village held by hostiles for game days

- **Looks like:** after a wipe, snapshot after snapshot with `Vikings loaded: 0`, `Respawns: N waiting, next due` and `Enemies within 10m: ... holding back the vikings due for` a growing number of minutes, the same few creatures (swamp skeletons, the village's own dead risen as zombies) in each, until a `Came back past enemies: N vikings together` line
- **Why it's fine:** the dead come due one a game day, and while hostiles have held the village they wait until enough are due to outnumber them, then come together (LOGIC.md's Coming Back). Creatures with no target idle around where they spawned or fell for as long as they like, so they don't leave on their own. The snapshot says why a due viking hasn't come yet - no spot to come from, or how many of how many are due
- **An issue when:** the group that comes loses its first fight, the snapshot gives no reason or a reason that doesn't fit, or the count of vikings due stops growing a day at a time

## No cooking where there's no meat

- **Looks like:** no `Cook` or `Meal` jobs in the totals and `Eaten` no higher than the food the village was stocked with, in a biome whose wild prey drops no food - like the Swamp, whose leeches and draugr drop bloodbags and entrails
- **Why it's fine:** cooking needs raw food in the chests and every cauldron meal the chests could make needs something they don't hold, so a village whose pen is empty has nothing to cook. The snapshot's `Food:` part shows the raw food, livestock and wild prey behind it
- **An issue when:** the `Food:` part shows raw food in the chests, livestock in the pens or wild prey that drops food near while food is low, and no `Hunt (food)`, `Slaughter` or `Cook` jobs follow
