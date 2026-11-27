# Viking Villages - Events

Every event a village can have on a day, and how a village picks one. [LOGIC.md](LOGIC.md) has everything else the villages do, [PROGRESSION.md](PROGRESSION.md#events) the events of each biome, and [COMMANDS.md](COMMANDS.md) the command for trying them out.

Times of day are shares of the game's day, as the game counts it: 0 is midnight, 0.25 dawn, about 6 in the morning, 0.5 noon and 0.75 nightfall, about 6 in the evening.

## How an Event Is Picked

- A village rolls for its event the first time it's loaded on a day, on the game that runs it - at midnight while you're there, or when you first come near it later in the day. It only rolls once it has living Vikings loaded and isn't being disbanded
- The roll is among the events of the village's biome that are off their cooldown. Each takes up its own chance of the roll in turn, and a roll past all of them is no event that day
- The event picked starts at a random time within its window - or from now to the end of its window when the village is rolled partway through it. Rolled after its window has closed, the village has no event that day
- An event waits past its time while it can't start yet, like the Traveling Viking during a raid. One that still can't start by the end of its window is missed
- The roll is kept only for as long as the game is running, and only by the game that runs the village. A village no player is near has no events, and one you leave before its event starts misses it - unless you're back before its window closes
- The day an event starts is saved on the village, and its cooldown counts from that day, so a missed event doesn't count toward its cooldown. A cooldown of 6 days means the event can start again on the 6th day after the day it last started

## Events

### Traveling Viking

| Chance a day | Cooldown | Village biomes | Arrives |
| --- | --- | --- | --- |
| 10% | 6 days | Meadows to the Plains | Between noon (0.5) and two game hours before dusk (0.627, about 3 in the afternoon) |

A Viking from a biome at least three harder than the village's comes by. Not while a raid is on the village.
