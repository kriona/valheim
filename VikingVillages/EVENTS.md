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

A PC Viking from a biome at least three harder than the village's stops by for the night - from the Mountains to the Deep North for a Meadows village, and only the Ashlands or the Deep North for a Plains one, each biome as likely. They don't come while a raid is on the village or an enemy holds back its newcomers, and come from outside the fence, out of your sight, walking in the way a newcomer does.

**Who they are**

- Rolled up as a Viking of their own biome, with its gear, skills, health and odds, but never as the archer. They have a name no Viking of the village goes by, and no byname
- They arrive having just eaten three of their biome's foods, and carrying 25-30 more (see [Starting Food](PROGRESSION.md#starting-food))
- They're only visiting, so they're never one of the village's own - they take no bed, keep no watch, go on no quests and don't count toward its Vikings. One who dies isn't mourned or brought back, and with Zombie Apocalypse rises as a zombie like any PC Viking
- They never learn, use or teach a forsaken power
- You can talk to them, and they greet you and pass on your news, but you can't rename them or open their inventory
- With `debugmode` on, they have "(Traveler)" after their name in pink

**Where they come from and go**

They come from one village and are on their way to another, picked at random from every village in the world - those you've found and those with only a name settled for them yet - and 10 far-off villages no village in the world goes by. The far-off villages stay the same in a world, and when one comes to be a village's name, the next unused name takes its place. When either of their villages has Vikings, they know a few of them by name and mention them now and then.

**Their visit**

- By day they gather around the fire, rest by it, talk with the village's Vikings and pet the village's animals, but do none of its chores. They eat their own food when they're hurt or to keep their foods up
- They fight off anything that threatens the village, like its own PC Vikings, and keep guard through a raid
- At dusk they gather at the fire with the rest, and sit by it the whole night - on a sitting log or other seat when there's one free - right through any storm
- In the morning, 1-2 game hours after the village wakes, they go to the nearest village chest with food in it, take 3-4 of the food it holds most of and leave 3-4 of their own, then say goodbye, walk out past the village's edge and head off out of your sight. They're gone once no player is within 40 meters of them
- A traveler can't reach a chest in a game hour sets off without trading
- The village talks about them while they're there and for 2 game days after they were due to leave, but stops once they die (see [Travelers](DIALOG.md#travelers))

**When you leave**

A traveler waits where they are while no player is near, like the village's own Vikings. Come back before they were due to leave and the visit goes on. Come back after it, and they've traded their food at a chest and gone, a few seconds after the village loads.
