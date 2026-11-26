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
