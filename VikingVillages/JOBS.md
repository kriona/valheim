# Viking Villages - Jobs

Everything a living Viking does in their village, with the numbers it goes by. [LOGIC.md](LOGIC.md) covers what their work adds up to and how they fight, get around and look after themselves, [QUESTS.md](QUESTS.md) what they do once they've left on a quest, and [DIALOG.md](DIALOG.md) what they say as they go about it.

Times of day are shares of the game's day, as the game counts it: 0.25 is dawn, about 6 in the morning, 0.5 noon and 0.75 nightfall, about 6 in the evening. Dusk is from 0.71, about 5 in the afternoon, until night. Seconds are real seconds - at the default day of 30 real minutes, each real minute is 48 game minutes.

## Every Job

Everything a living Viking does, most important first. What's higher up stops what's lower down: the jobs marked Always drop whatever a Viking is doing, by day a Viking picks the first of the numbered jobs they can do, and with no work they roll between the ones at the bottom, by weight - see [How a Job Is Picked](#how-a-job-is-picked) for the whole order.

| Job | When | What they do |
| --- | --- | --- |
| [Run out of flames](#run-out-of-flames) | Always | Run clear of a fire's flames |
| [Step away from poison](#step-away-from-poison) | Always | With Zombie Apocalypse, keep 3 m from poison clouds and corpses that might leave one |
| [Keep clear of roots](#keep-clear-of-roots) | Always | Back away from hostile roots, calling an archer to shoot them |
| [Put themselves out](#put-themselves-out) | Always | Run into water when on fire |
| [Drop what slows them](#drop-what-slows-them) | Always | Drop what they can't carry when they have to fight or run |
| [Run home](#run-home) | Always | Run to the village's core when losing a fight or in danger outside |
| [Fight](#fight) | Always | Go after threats and help fellows who are attacked |
| [Wait for the dead](#wait-for-the-dead) | Always | Stand ready by a corpse about to rise |
| [Get off a roof](#get-off-a-roof) | Always | Walk down to the ground |
| [Get out of a pen](#get-out-of-a-pen) | Always | Leave a pen they have no work in |
| [Get out of the water](#get-out-of-the-water) | Always | Walk to dry ground |
| [Walk back](#walk-back) | Always | Return when more than 75 m past the village's edge |
| [Mourn the dead](#mourn-the-dead) | Day or night | Grieve by a fallen Viking of their village, then put away their gear |
| [Keep guard](#keep-guard) | During a raid | Stand around the fire, facing out, with their weapon drawn |
| [Lighten the load](#lighten-the-load) | Day or night, first | Put away or drop what they can't carry |
| [Pick up kill loot](#pick-up-kill-loot) | Day or night, next | Pick up what an enemy they fought dropped |
| [Put away what a quest brought home](#put-away-what-a-quest-brought-home) | Day or night, next | Store what a quest brought back |
| [Keep watch](#keep-watch) | Night | Patrol the village with a torch and wake it to danger |
| [Sleep](#sleep) | Night | Go to bed, a chair or their house's floor |
| [Torches](#torches) | Dusk and night | Hold a torch, and craft one when short |
| [Teach a forsaken power](#teach-a-forsaken-power) | Dusk | Use a power beside a Viking who doesn't know it |
| [Gather at the fire](#gather-at-the-fire) | Dusk | Stand or sit in a ring around the fire |
| [Leave on a quest](#leave-on-a-quest) | Day, first | Pack food, potions and a tool, then head out |
| [Eat](#eat) | Day 1 | Eat from the chests when hurt |
| [Rest by the fire](#rest-by-the-fire) | Day 2 | Sit by the fire when hurt until rested |
| [Keep their foods up](#keep-their-foods-up) | Day 3 | Eat while there's food to spare |
| [Keep warm](#keep-warm) | Day 4 | Sit by a fire or go inside in freezing weather |
| [Put away loot](#put-away-loot) | Day 5 | Put what they carry in the chests |
| [Pick up loot](#pick-up-loot) | Day 6 | Gather items lying around the village |
| [Cook](#cook) | Day 7 | Cook raw meat at the cooking stations |
| [Cook a meal](#cook-a-meal) | Day 8 | Make a meal at the cauldron |
| [Take up better gear](#take-up-better-gear) | Day 9 | Put on better gear from the chests |
| [Fill their quiver](#fill-their-quiver) | Day 10 | Take arrows from the chests |
| [Make paths](#make-paths) | Day 11 | Hoe paths to new buildings, bridging water |
| [Repair the village](#repair-the-village) | Day 12 | Fix damaged pieces |
| [Clear snow](#clear-snow) | Day 13 | In the Deep North, knock heavy snow off pieces and the ground |
| [Repair their gear](#repair-their-gear) | Day 14 | Fix worn gear at the workbench or forge |
| [Rebuild the fire](#rebuild-the-fire) | Day 15 | Build back the village's fire |
| [Take down a sitting log](#take-down-a-sitting-log) | Day 16 | Take down the logs by an old fire for their wood |
| [Cut trees](#cut-trees) | Day 17 | Fell brush trees when the village is out of wood |
| [Rebuild the village](#rebuild-the-village) | Day 18 | Build back destroyed pieces |
| [Stack the fire](#stack-the-fire) | Day 19 | Put wood on the fire |
| [Craft arrows](#craft-arrows) | Day 20 | Make the best arrows the village can |
| [Craft a torch](#craft-a-torch) | Day 21 | Make a torch when theirs is missing or worn |
| [Craft a second weapon](#craft-a-second-weapon) | Day 22 | Make a crude bow, or an axe or club |
| [Craft better gear](#craft-better-gear) | Day 23 | Make a weapon, shield or armor better than their own |
| [Upgrade their gear](#upgrade-their-gear) | Day 24 | Raise a piece of their gear one quality |
| [Work toward the goal](#work-toward-the-goal) | Day 25 | Fuel and fill smelters, and craft materials for the village's goal |
| [Stock tools](#stock-tools) | Day 26 | Make axes and pickaxes for the chests |
| [Line a weapon with silver](#line-a-weapon-with-silver) | Day 27 | With Zombie Apocalypse, line a weapon with silver |
| [Silver arrows](#silver-arrows) | Day 28 | With Zombie Apocalypse, take or craft silver arrows |
| [Build a beehive](#build-a-beehive) | Day 29 | Build a hive with a queen bee |
| [Talk](#talk) | Day, weight 25 | Chat with another Viking |
| [Pet a boar](#pet-a-boar) | Day, weight 10 | Pet one of the village's livestock |
| [Stock the chests with wood](#stock-the-chests-with-wood) | Day, weight 15 | Gather wood from the ground into the chests |
| [Forage](#forage) | Day, weight 15, or 60 short of food | Pick honey, berries, mushrooms and flint |
| [Hunt](#hunt) | Day, weight 15, or 60 short of food | Hunt prey near the village, from the Plains on in parties |
| [Get rested again](#get-rested-again) | Day, weight 15 | Sit by the fire until rested |
| [Stroll](#stroll) | Day, weight 40 | Walk to an open spot in the village |
| [Stand about](#stand-about) | Day, weight 25 | Stand and look around |

## How a Job Is Picked

A Viking does one job at a time, and picks the next once it's done. What they pick goes in this order, the first that fits:

1. [Lighten the load](#lighten-the-load) when they carry more than they can - day or night, before anything else
2. [Keep guard](#keep-guard) while a raid is on the village
3. [Pick up kill loot](#pick-up-kill-loot) for 45 seconds after an enemy they fought dies
4. [Put away what a quest brought home](#put-away-what-a-quest-brought-home)
5. At night: [craft a torch](#torches) when theirs won't see them through, then [keep watch](#keep-watch) for the watch, then [sleep](#sleep) once it's their bedtime, or else [gather at the fire](#gather-at-the-fire)
6. At dusk: [take food off the cooking stations](#cook), [craft a torch](#torches), [teach a forsaken power](#teach-a-forsaken-power), then [gather at the fire](#gather-at-the-fire)
7. By day: the first of the [day's work](#day-work) they can do, in order
8. By day, with no work: one of the [rolled jobs](#rolled-jobs) at random, by weight

On top of all of this come the things they do [whatever their job](#whatever-their-job) - running out of flames, fighting, running home and the like - which drop the job they're on. Once a fight is over, they pick a job again from the top.

They look again while they're at a job:

- Every 2 seconds, for a body of the village's to [mourn](#mourn-the-dead)
- Every 5 seconds, by day, a Viking standing about, strolling or petting a boar looks for work, and drops what they're doing for it - a Viking talking isn't pulled away for work. By day, one standing about, strolling, petting or picking up loot also drops it to take done food off a cooking station before it burns
- Every 5 seconds, by day, a Viking who's freezing at any job but eating, resting, keeping watch or lightening their load drops it to [keep warm](#keep-warm)
- Every 5 seconds, for loot after a fight - see [Pick up kill loot](#pick-up-kill-loot) and [Pick up loot](#pick-up-loot)
- Every 5 seconds at dusk, a Viking gathered at the fire looks for someone to [teach a forsaken power](#teach-a-forsaken-power)

Dusk ends most of the day's work: hunting, foraging, stocking wood, picking up loot (but not after a fight), cutting trees, crafting weapons and gear, upgrading, smelting, silver-lining, making paths and building beehives stop as dusk comes. Repairing, rebuilding, stacking the fire, cooking, crafting arrows and talking are finished first.

### A Few at Once

Some jobs only a few Vikings of a village do at once, counted across every player's game:

| Job | How many at once |
| --- | --- |
| Stock the chests with wood | 1 for every 4 Vikings, or part of 4 |
| Forage | 1 for every 4 Vikings, or part of 4 |
| Hunt | 1 for every 5 Vikings, or part of 5 - a hunting party's members aside |
| Pet a boar | 1 for every 6 Vikings, or part of 6 |
| Build a beehive, make paths, cut trees, smelt | 1 |
| Craft a second weapon, craft better gear, craft for the goal, stock tools | 1 between them |
| Upgrade gear, line a weapon with silver | 1 between them |

A cooking station, cauldron, workbench, forge, smelter or fire being stacked is held by the Viking at it, so no other Viking uses it until they're done - or 90 seconds after they stop keeping their hold on it, when they die, say. A piece to repair or rebuild, loot, wood on the ground and a tree are each worked on by one Viking at a time.

### Leaving the Village

In a village of more than 2 Vikings, a Viking only goes out of the village for wood, loot, trees, prey or forage while another Viking is at home - inside the village and not heading out. Otherwise they look for it inside the village only.

Out of the village, a Viking after wood, loot or forage who finds an enemy within 50 meters drops it and [runs home](#run-home). A single greyling with nothing else near doesn't count - they fight it.

### Chests

The Vikings use any chest in the village's rings or inside one of its houses that's open to everyone - not a cart, ship or tombstone, a chest a player built inside a ward, one that's open, or one whose player is within 6 meters of it. They walk to within 1.5 meters and reach in for 0.8 seconds, which also merges its part-stacks.

They keep their chests tidy:

- Each chest keeps a slot free for wood until it holds some, and one for food until it holds some
- Wood fills at most half of a chest's slots
- Anything else only tops up its stacks, or goes in as a new stack when the chest has none of it and the village's chests hold fewer than 2 stacks of it. They keep 3 axes and 4 pickaxes
- What doesn't fit anywhere is dropped outside the village to despawn, on the village's pile - see [Put away loot](#put-away-loot)

### Wood

The wood for rebuilding, stacking the fire, arrows, torches and crafting comes from the village's chests first, smallest stack first, then branches and wood on the ground within 60 meters past the village's edge - carrying up to 20 at a time. It's always plain wood: fine wood and core wood are harder to come by, so they never spend either as wood, keeping them for recipes that ask for them by name, and put any they find away as loot.

A Viking who finds no wood skips work that takes it for 60 seconds, unless they carry some or a chest has some - each Viking for themselves.

### Giving Up

Something a Viking can't get to - a chest, an item, a tree, prey - they leave alone for 60 seconds, and a piece to repair or clear snow off for 5 minutes. Once 3 tries by at least 2 Vikings have failed within 10 minutes, every Viking leaves it alone for 30 minutes, then an hour, and so on up to 2 hours each time it happens again.

A Viking who walks for 10 seconds without getting half a meter nearer gives up on where they were going. After 3 seconds held up, they open or close a door beside them, or jump a low obstacle like a pen's fence, up to 3 times - and after 1 second, they ask Vikings standing about, strolling, talking, petting a boar or standing at the fire within 2.5 meters ahead of them to step 2 meters aside.

A ward keeps the Vikings from repairing, rebuilding, clearing snow and taking down logs inside it, the same as it keeps out players it doesn't permit.

## Whatever Their Job

These come before any job, in this order, and drop the job they're on.

### Run Out of Flames

Within half a meter of a fire's flames, checked 5 times a second, they get up and run straight away from the fire's middle - even in a fight - until they're a meter clear. Not while sitting or lying.

### Step Away From Poison

With Zombie Apocalypse, a poison cloud, or a zombie's corpse that might leave one, within 3 meters has them run straight away from it - even in a fight - until they're 3 meters clear.

### Keep Clear of Roots

A hostile root within 6 meters has them back straight away from it, even in a fight. An archer keeps shooting it from there, outside its lash. One fighting hand to hand calls the nearest archer of the village to it once a root is within 15 meters - at most every 15 seconds - who goes after it, or with no archer in the village, warns the others to keep clear.

### Put Themselves Out

On fire and out of the water, they look every 2 seconds for water deep enough to soak them - at least 0.4 meters - within 24 meters, never the Ashlands' boiling sea, and run into it, even in a fight.

### Drop What Slows Them

Carrying more than they can with an enemy to fight or while running away, they drop the heaviest of what they can spare on the spot, only as much as takes them back under what they can carry, for a fellow to carry off later.

### Run Home

Home is the village's core, not its edge. A Viking away from it runs home:

- Foraging, when an enemy goes for them or they were hurt in the last second
- Out of the village for wood or loot, when an enemy goes for them - or for wood, loot or forage, when an enemy is within 50 meters
- Hunting, below half their health, or when an enemy that isn't their prey or a greyling goes for them
- Fighting, checked twice a second, the first of these:
  - Below a quarter of their health - or half, for a hunter
  - A living Viking within 15 meters is below a quarter of their health and away from home too - they run home with them
  - 4 or more enemies within 15 meters
  - No living Viking within 15 meters, and hurt in the last 5 seconds - unless they're hunting or it's a lone greyling

A lone greyling never sends them home: they fight it.

They run without fighting to the village's rally point - 4 meters from the fire, or a house's doorstep when the fire is fenced in - shouting for help every 6 seconds while an enemy that fights back is within 15 meters. One who ran for being alone stops as soon as a living Viking is within 15 meters; the rest keep running until they're home. At home, with none of the village within 6 meters, they run on to the nearest, and then turn to fight. One who can't get home - stuck for 10 seconds - stands and fights.

### Fight

They go after an enemy when it's a threat: inside the village, within 10 meters, alerted, after someone, or attacking a building. They leave alone:

- Animals that only run, like deer, hares and birds - unless it's the prey they're hunting, or a zombie
- Leeches that haven't hurt a player or Viking yet
- Sleeping monsters, and for one fighting hand to hand, those swimming or in water more than a meter deep
- The village's livestock, unless they're slaughtering it

Archers go for enemy archers first, as though they were a fifth nearer.

They come to help:

- A player or living Viking hurt by a monster calls every Viking within 30 meters, who drops what they're doing and goes after the attacker - unless they're already fighting a nearer enemy. So does a monster hitting a building in a village
- Once a second, a Viking with no enemy, awake and above three quarters of their health joins a living Viking they can see within 20 meters fighting an enemy within 30 meters of them

They give up on an enemy more than 40 meters past the village's edge - 70 for one that hurt them, that they were called against or joined against in the last 15 seconds, and 80 for the prey they're hunting. One fighting hand to hand goes around or over a pen's fence between them and an enemy within 8 meters, for up to 10 seconds, jumping it after 3 seconds getting no nearer.

They switch to their bow when 3 or more of their fellows are already at the enemy, they can't walk to it, it's flying or in deep water, or they're luring it.

They call an enemy out by name at most every 15 seconds each - 2 seconds apart from any other Viking's call, and not the same enemy again for a minute.

#### Tough Monsters

A monster is tough when it would take more of a Viking's hardest hits to bring it down than of its hardest hits to bring down the Viking, through their armor. It takes half again as many Vikings as those odds to fight it - rounded up. Until that many of the village above a quarter of their health are near it, they keep clear, as long as enough are near enough to come or an archer can shoot it:

- The Viking it's after lures it, keeping 1.5-4 meters beyond its reach, backing off at a run and turning 70° aside when cornered
- The rest keep 8 meters beyond its reach, or as far as the lure plus 3 meters, up to 22 - archers shooting, the others facing it
- They shout for help

Once enough have gathered, they all charge. With neither enough fellows nor an archer, they fight it anyway.

#### Hanging Back

A Viking at home below a quarter of their health hangs back until they're above 40%, as long as at least one fellow above half their health is within 15 meters and the enemies within 15 meters don't outnumber them - otherwise they fight on. Hanging back, they:

- Keep 5 meters from enemies that fight hand to hand and 20 from those that shoot or throw, stepping back 3 meters at a time, across the core when a step would leave it
- Run from any enemy within 20 meters that's after them, for 3 seconds after it stops coming
- Otherwise strike an enemy within 15 meters that's after someone else from behind - circling 4 meters out without crossing in front of it, then closing in once within 50° of its back
- Shout for help every 6 seconds

### Wait for the Dead

With nothing to fight and awake, a corpse about to rise within 25 meters - and no more than 40 meters past the village's edge - has them stand 3-6 meters from it with their weapon drawn, facing it. The first to stand ready by one of their own village says goodbye. As it rises, they go for it - one fighting hand to hand from 2.5 meters, an archer from where they stand. This comes before mourning and every job, keeping guard included.

### Get Off a Roof

With no danger near - not alerted, not waiting for the dead and not hurt in the last 10 seconds - a Viking on a roof walks to the nearest ground within 10 meters, checked every second. With none, they give up for 15 seconds.

### Get Out of a Pen

With no danger near, checked every 2 seconds, a Viking in a pen leaves it for the rally point, jumping the fence when there's no other way out - unless they're in it to forage, pet a boar, pick up loot or wood, stack a fire or slaughter.

### Get Out of the Water

Standing in water at two checks 3 seconds apart, having moved less than a meter, they walk to the nearest dry ground within 16 meters, outside any pen. With none, they give up for 20 seconds.

### Walk Back

More than 75 meters past the village's edge with nothing to fight, they run back to the rally point, stopping as soon as they're inside the village.

### Mourn the Dead

Every 2 seconds, a Viking with nothing to fight looks for the body of a Viking of their village that stays dead, within 25 meters, with no hostile creature within 20 meters of it. Not while keeping watch, asleep, hunting, cooking, lightening their load or during a raid, and only once for each body.

Up to 5 mourn each body. Each walks to 1.8 meters from it, 72° around from the last - or mourns from where they are when they can't get there but are within 6 meters - and stands by it for 15-25 seconds, kneeling, crying, despairing or bowing their head every 5-9 seconds. The first says goodbye with the dead Viking's name, and each after them does half the time.

Afterwards, they pick up what the dead Viking dropped, the way they [pick up kill loot](#pick-up-kill-loot), and put it away.

## Raids

A raid is on a village while the game's event reaches over its edge and is running - always, for raids that don't pause, or while a player is near.

Each Viking notices it once they're awake. The night watch, at dusk or night, roars and wakes the whole village to it. Otherwise a Viking calls the raid out by name, once every 5 minutes for each village. They drop whatever they're doing, unless they're keeping guard or watch, eating, lightening their load or resting by the fire while hurt.

Until the raid is over, a Viking only:

- Lightens their load
- Keeps watch, at night, for the watch
- Eats, when below three quarters of their health with food they can eat in a chest - or else rests by the fire
- Keeps guard - everyone else

No loot is picked up, nothing put away, no one taught a power, no quest leaves, and no one mourns.

### Keep Guard

They stand around the fire they gather at, weapon drawn, facing out and looking around 60° either way every 2-4 seconds:

- Archers in the evening's ring - 4-5.5 meters from a bonfire, 2.5-3.5 from any other fire
- Those fighting hand to hand 3 meters beyond them - 8.5-10 meters from a bonfire, 6.5-7.5 from any other fire

Each Viking keeps to a side of the fire of their own, give or take 10°, trying up to 12 spots fanned out from it - out of the water, houses and pens. With no fire, or no spot found, they stand anywhere open in the core. Every 20-40 seconds they move to a new spot.

## Night and Dusk

### Gather at the Fire

From dusk until their bedtime - not exactly at nightfall - they stand in a ring around the fire, facing it.

They go to the gathering fire nearest their home with room - the village's own fire, or one you made a gathering fire, not fenced into a pen - or with none with room, the one least crowded for its size. A bonfire has room for 17, any other fire 10, standing 1.75 meters apart, and when more come, the ring widens. With no fire at all, they stand at their house's doorstep.

35% of them sit down, each at a moment of their own between 0.725 and 0.74 of the day, as the torches come out - a free seat first, like a log, bench or chair within 3 meters of the ring, or else the ground, facing a neighbor sitting within 2 meters, or the fire. One below three quarters of their health sits at once, in the fire's warmth. The village's own fire has 2 sitting logs.

In a thunderstorm, they gather inside their house instead, standing on its floor until bedtime. Light rain doesn't send them in.

### Teach a Forsaken Power

At dusk, a Viking who knows a forsaken power another of the village doesn't - awake, in the village, and not being taught by anyone else - and whose power is ready again, walks to within 3 meters of them and uses it, so everyone within its 10 meters who didn't know it learns it. They teach the power of the latest boss the other doesn't know, stand facing them for 2 seconds, and go back to the gathering. They give up when dusk is over, after a minute, or when they can't get there. Not during a raid.

### Torches

Everyone holds a torch from 0.72 of the day until dawn, unless they're within 8 meters of a gathering fire or resting - and the watch from dusk. A Viking wearing a Dverger circlet of their own needs none.

At dusk or night, a Viking with no torch crafts one - and the watch, when their torches won't burn through the rest of their shift, checked every 5 seconds. They only use what they carry or the chests hold, never gathering anything in the dark, and with nothing to make one from try again a minute later.

By day, a Viking with no torch, or only one worn down to half, crafts one - see [Craft a torch](#craft-a-torch).

### Keep Watch

One Viking at random for every 10 in the village keeps watch each night - one for up to 10 Vikings, two for 11-20, and so on, counting only those loaded. The watch is picked as dusk comes, from those who haven't kept watch that night, and only before nightfall - a village that first loads after nightfall has no watch that night.

The watch gathers at the fire like everyone else and starts their round as they'd go to bed, 0-60 seconds after nightfall. They walk from the doorstep of each house in the village's core - or 2 meters outside a house with no doors - to the outer edge of the village fire's ring and back around, starting at the nearest, stopping 3-6 seconds at each and looking around every 1.5-3 seconds.

- **Noises:** listening every second, the watch goes to look where they heard a player they couldn't see within 10 meters, calling out first - for up to 30 seconds - saying something when they find them, or that there's nothing there, then looks around for 3 seconds and goes back to their round. They leave that player's noises alone for 3 minutes after
- **Alarms:** the watch roars at an enemy they fight within 30 meters of the village's edge, at most every 20 seconds, standing for 1.5 seconds. Every Viking of the village wakes and goes after the nearest enemy within 30 meters of its edge, then goes back to bed once there's nothing left to fight
- **Freezing weather:** a watch whose gear doesn't resist frost keeps watch from a spot in the warmth of the fire they'd gather at, or with no fire burning, inside their house - still listening, and back on their round once the weather clears
- **Midnight:** each watch wakes a Viking who hasn't kept watch that night, walking to within 2 meters of them and facing them for 2.5 seconds - or handing over from where they are when they can't get there, or after 90 seconds anyway - and goes to bed, after up to a minute by the fire. With no one left to wake, they keep watch on
- **A watch who dies:** before nightfall, another is picked in their place. At night, only a Viking who sees it happen - awake, not keeping watch already that night, within 30 meters - takes the watch, saying so. A watch who dies before waking the next leaves them sleeping

The watch ends at dawn.

### Sleep

Every Viking but the watch goes to bed at a bedtime of their own, 0-60 seconds after nightfall - most within about 15 seconds. One woken in the night goes straight back.

They take their own bed first, then a bed in their own house, then the nearest - any bed in a house of the village's core, or a bed that brought a Viking anywhere in the village, that no player has claimed and no other Viking holds. The bed they take becomes their own. With no bed, they sit on a free chair in their own house, or lie on its floor at least 2 meters from its doors.

- Held up on the way to a bed, they wait 3 seconds and try again twice, then sleep on the floor
- A player taking their bed gets them up to find another
- Pushed more than a meter off their spot on the floor, they go back to it
- Asleep, they see nothing, only hear within 25 meters, and don't greet, mourn or join fights

They wake 1-61 seconds after dawn - most within about 16 - and half of them stretch. One who slept in a bed wakes rested, for the game's rested time and more for the bed's comfort, and one who slept in a chair or on the floor in a fire's warmth can too. When players sleep through the night, the Vikings wake the same way.

## Day Work

By day, a Viking does the first of these they can, in this order - but for lightening the load, picking up kill loot and putting away what a quest brought home, which come before any job, day or night.

### Leave on a Quest

A quest of their village setting out with them on it - between 0.24 and 0.6 of the day, with no enemy within 30 meters of the village - drops whatever they're doing. Only lightening their load comes first. They:

1. Take the quest's tool from the nearest chest that has it, or go without
2. Pack food for as long as they're gone - 3 foods for each half hour, up to 9, no more than a third of them of one kind while there are others - up to 2 healing potions, and what the quest spends, like treasure to sell. For a quest that kills creatures they take their strongest food, and for the rest the food they'd keep their foods up on (see [Keep Their Foods Up](#keep-their-foods-up)). They take no more than half of any stack of food or potions, from each chest once
3. The first of them says when they'll be back
4. Run out of the village to a spot outside it, and are gone

After 90 seconds they leave from wherever they are. See [QUESTS.md](QUESTS.md) for the rest.

### Eat

Below three quarters of their health, with food they can eat in a chest, they eat. They eat the food in the nearest chest that heals fastest, or with two alike, the one that adds the most health - one food each time, eating again as long as they still want to, up to three different foods, and each again once half worn off, the way you can. With none they can eat, they look again a minute later.

### Rest by the Fire

Below three quarters of their health and not rested, with a gathering fire in the village, they sit in its warmth - a free seat first - until they're rested. With nowhere warm to sit, or not resting after a minute sitting - cold, wet or burning - they give up for a minute. They stop at dusk.

In any rain, the Swamp's too, unless their spot is under a roof, they sit on their house's floor instead until they're above three quarters of their health, the rain stops, or dusk.

Below half their health with nothing to fight, they use the Elder's power, when they know it and it's ready.

### Keep Their Foods Up

While the chests hold three foods for each Viking or more, they keep their foods up for the health they add - with less, the food is left for the wounded. They eat a food once one of their three places is free, or one of theirs has a tenth of it left. Of all the chests' food they can eat, they eat the weakest that still raises them above their least max health (see [LOGIC.md](LOGIC.md#eating)) - a food adding more than a third of what that holds over the base 25 - from the nearest chest that has it, so the strongest food is left for the wounded and for fights. With none that does, they eat the strongest of the rest.

### Keep Warm

In freezing weather, unless their gear resists frost, they sit in the warmth of the gathering fire nearest their home with room, until the weather clears. With no fire burning, still freezing 10 seconds after they get to it, or within a minute of failing to warm up there, they stand inside their house instead. They stop at dusk.

### Put Away Loot

Loot they carry goes to the chest that takes it best - one whose part-stacks take all of it, then one with a part-stack, then one that has some, then the nearest. With no chest with room:

- Wood or food is put down where they stand
- Anything else is carried out of the village and dropped there to despawn, marked so no Viking puts it away again - on the village's pile, beside what its Vikings dropped outside before while any of it is still there, or else 3-50 meters past its edge. Either way it's at least 3 meters from any of your bases, with no enemy within 50 meters. In a village they can't leave yet, or with nowhere to drop it - an enemy by the pile, say - they hold on to it and try again 15 seconds later, and after 3 minutes with nowhere to drop it, put it down where they stand

Better gear among it is put on at once.

Loot comes ahead of cooking, so it doesn't lie about while the cooks are busy. Done food is still taken off a cooking station before it burns, by a Viking at loot work too.

### Pick Up Loot

An item that's come to rest within 10 meters past the village's edge - and lain there 2 seconds, or 30 for one a player dropped - is picked up when a chest has room for it, or it's something the Vikings don't use and they can carry it out. Not arrows, bolts, fish, food set out as a piece, or what a Viking dropped outside. They take as much as they can carry, then the next within 15 meters, then put it away.

For 2 minutes after a fight, picking up loot also comes before standing about, strolling, petting a boar, talking, gathering at the fire and resting for the night - and before repairing, rebuilding, stacking the fire, stocking wood, crafting arrows and repairing gear, which the Viking drops for it - going on through dusk and night. Not for a Viking below three quarters of their health or keeping watch.

### Cook

They cook at the village's cooking stations - within 12 meters past its edge or inside a house - nearest first, one Viking at a station and the other stations within 4 meters of it, the same fire:

1. A station with done food on it, lit or not
2. A lit station with raw food on it
3. A lit station with room, when they carry raw meat or a chest has some

They take done food off first, putting raw food on 0.8 seconds a piece, fetching as much from a chest as the stations hold, then wait by them for it to cook - each piece raising their cooking skill. What they take off may give a bonus piece, the way it does for you. They don't put on raw food the cauldron's meals need. Food over a fire gone out is left there.

From dusk, they finish what's on the stations without fetching more - see [How a Job Is Picked](#how-a-job-is-picked).

### Cook a Meal

Every 15 seconds, by day, they look at the village's cauldron - usable the way it is for you, under a roof - for the meal it can make from what the chests hold that adds the most health and stamina, among those the chests hold fewer than 10 of. They fetch what it takes, work at the cauldron for 3 seconds, and put the meal away. What the meals need of the raw food in the chests - one cauldron's worth for each - is kept back from the cooking stations.

### Pick Up Kill Loot

For 45 seconds after an enemy they fought dies, a Viking who isn't below three quarters of their health or carrying more than they can picks up what it dropped within 8 meters of where it died - however far from the village - when the village has a chest for it and no enemy is within 50 meters, then whatever else they can carry within 15 meters. This comes first, before any job, the night watch included, and goes on through dusk and night.

### Put Away What a Quest Brought Home

Back from a quest, they put away what they found, the food and potions they didn't use and the tool they took, before anything else - even at night. By day, raw meat goes to a lit cooking station with room first. A Dverger circlet they brought home for themselves, they wear.

### Lighten the Load

Carrying more than they can, with something to spare, they lighten their load before anything else, day or night - before gathering at the fire, keeping watch or going to bed, and before keeping guard in a raid. What they can spare is anything they carry for the village, or of their own that they don't keep - they always keep the gear they wear, their best melee weapon and bow, the arrows for it, their shield, torches and a Dverger circlet. They put the heaviest of it in the nearest chest with room within 20 meters - only as much as takes them back under what they can carry - or with no chest, drop it where they stand, until they're no longer slowed. In a fight they drop it on the spot - see [Drop What Slows Them](#drop-what-slows-them).

### Take Up Better Gear

A chest holding gear better than their own - never broken - they put on, carrying what it replaces to a chest:

- Armor with more armor, or for an empty slot - and in freezing weather, a piece that resists frost when nothing they wear does
- A shield that blocks more
- A weapon that does more damage than their best of its kind, melee or ranged
- A utility item with a worn effect, for an empty utility slot
- A Dverger circlet, when they have none

### Fill Their Quiver

An archer missing at least a batch of their 100 arrows - 20 for wood arrows - with arrows for their bow in a chest, or with arrows in a chest better than their best, takes the 100 best of their own and the chest's, putting back their own that don't make the cut while the chest has room, and shoots their best first.

### Make Paths

A building of yours that grew the village with no path to the rest of it - while the village has a workbench, no other Viking is making paths and it isn't a thunderstorm - gets a path from its door to the nearest other house's. It comes ahead of the village's upkeep, since only one Viking makes paths at a time and the rest see to the upkeep.

1. They get a hoe from a chest, or craft one at the workbench, going out for its stone when the village has too little
2. They hoe the path every 1.5 meters, swinging every 1.2 seconds, clearing branches, stones and bushes as they go, the hoe wearing down as yours does
3. Water no wider than the village's bridges span - 14 meters for wood - is crossed on a bridge like theirs, wood, or grausten in the Ashlands and the Deep North, that they gather the wood for, up to 20, and hammer up when they reach the bank. The village repairs and rebuilds it like its other pieces
4. A broken hoe is repaired at the workbench. Once it's done, they repair the hoe and put it in a chest, even their own

A Viking called away partway - by a fight, the cold, dusk or the like - puts the hoe away, and whoever takes the path up next, them or another, carries on from the spot and bridge it was left at. A house with no way through is given up on.

### Repair the Village

A piece of the village below full health - within 12 meters past its edge or inside a house, those you built included - they repair, the nearest first, with one swing of a hammer, taking nothing. Not reaching it within 90 seconds, they leave it for 5 minutes.

### Clear Snow

In the Deep North, a piece in the village's rings - those you built included - with heavy snow on it is cleared with one swing of a hammer, knocking all its snow off, the nearest first. Then the nearest deep snow on the village's ground, outside its houses, cleared within 4 meters with one swing.

### Repair Their Gear

A piece of their gear below three quarters of its durability that the village's workbench or forge can repair - under a roof, and lit when it needs fire - sends them to that station, where they repair every worn piece it can, most worn first, 1.5 seconds each, raising their crafting skill.

### Rebuild the Fire

The village's bonfire or campfire, destroyed, is built back with one swing, taking nothing. Gathering fires you made aren't.

### Take Down a Sitting Log

The village took up a new fire, and one of the sitting logs it built by the old one still stands - they knock it down with one swing and keep its wood, which builds it back around the new fire.

### Cut Trees

When the village needs wood badly - an archer of the village down to fewer than 20 arrows, or its workbench or forge needing building back - and the Viking found no wood lately, carries none and the chests have none, one Viking at a time cuts up to 4 brush trees: small trees that drop their wood when they fall, within 60 meters past the village's edge, the nearest first. Not while below three quarters of their health.

1. They use their own axe, or take one from a chest
2. With none, they leave it to a Viking with an axe who's standing about, strolling, talking or petting a boar, who stops to take it on - waiting for up to 30 seconds
3. With no one to take it, they craft a stone axe, punching the trees for its wood when there's none - with a punch doing 4 chopping damage - and try crafting it again after each tree falls

They swing every 1.2 seconds, raising their woodcutting skill, leaving a tree for a minute after 20 swings or 50 punches. The wood lies where the trees fell for the work that needs it to gather, and an axe they borrowed or crafted goes back in a chest.

### Rebuild the Village

A piece of the village that was destroyed is built back, the lowest first, and a house with nothing left standing last, each with one swing - taking as much wood as the piece took to build, whatever it's made of, up to the 20 they carry.

### Stack the Fire

The village's fire, or a gathering fire, that isn't full and has no other Viking at it, they stack with up to 12 wood in one go, a piece every 0.8 seconds, from outside its flames.

### Craft Arrows

An archer missing at least a batch of their 100 arrows crafts more, when they have fewer than 40, or the chests and what they carry hold 40 more wood than a batch takes - and no chest has arrows for them. They make the arrows that do the most damage that the village has the station and everything but the wood for - flint, fire, bronze and the like - or else wood arrows at the workbench, 8 wood for 20. Silver arrows are left for [silver arrows](#silver-arrows).

While they have an arrow left, they first go out for what the best arrows the village's stations can make are short of - flint, feathers or resin, say - rather than settle for worse. They craft batch after batch, each raising their crafting skill and perhaps giving a bonus the way crafting does for you, until their quiver's full, they run out of something, or they can't get to the station - going on past dusk.

### Craft a Torch

By day, a Viking with no torch, or only one worn down to half, crafts one where they stand - going out for the resin when the chests have none, hunting greylings for it. With two or more, or a Dverger circlet of their own, they don't. See [Torches](#torches) for dusk and night.

### Craft a Second Weapon

One Viking at a time:

- With no bow, when the village's chests and what they carry hold the wood for a crude bow and a batch of wood arrows, they craft a crude bow at the workbench - going out for leather scraps when the chests are short
- With only a bow, they craft a flint axe when the village has any flint, going out for the rest - or a stone axe when there's stone in the chests or to be found, going out for it - or else a club, from wood

### Craft Better Gear

Every 15 seconds, while no other Viking is arming themselves, they look for the best weapon, shield and armor better than their own that the village's stations, upgraded far enough, and chests can make, wood included. They craft the first of a melee weapon, a bow, a shield, then chest, legs, head and shoulder armor, and put it on, carrying what it replaces to a chest. They don't go out for anything for it.

### Upgrade Their Gear

A piece of theirs below its top quality, lowest quality first, whose station is upgraded far enough for the next quality, with everything that quality takes in the chests, they take to the station and raise one quality, at full durability. One Viking at a time.

### Work Toward the Goal

The village picks a goal every minute - the next piece of gear for its weakest Viking: an upgrade of what they wear, lowest quality first, else the least valuable thing they'd take up of each kind, else with Zombie Apocalypse a silver lining for a weapon. The Vikings never build or upgrade stations themselves, so a goal that needs a station the village doesn't have is passed over. The steps toward it that are quests go out as [quests](QUESTS.md), and what can be gathered is gathered by the jobs that do. The rest is done here, checked every 15 seconds:

1. **Fuel:** a smelter with ore in it and too little fuel for it is topped up from the chests
2. **Smelting:** the ore the goal needs goes in the nearest smelter or kiln of its kind - usable the way it is for you - with the fuel for all of it, a piece every 0.6 seconds, fuel first. A kiln's wood keeps 20 back for the village. The bars it drops are picked up as loot
3. **Crafting:** what the goal's gear is made from, like bronze, is crafted and put in a chest - going out for what it's short of

The gear itself is then made by [crafting better gear](#craft-better-gear) or [upgrading](#upgrade-their-gear). One Viking at a time smelts.

### Stock Tools

Every 20 seconds, while no other Viking is arming themselves, they count the village's axes and pickaxes - in the chests and on every Viking - and craft for a chest:

1. With fewer than 3 axes, the best axe the village can make, never a battleaxe - or a stone axe, going out for its stone
2. From the Black Forest on, with no axe that cuts fine wood, the best that does
3. From the Black Forest on, with fewer than 4 pickaxes, the best pickaxe the village can make

### Line a Weapon With Silver

With Zombie Apocalypse, a weapon of their own that it can line with silver, the one they wield first, is lined at its forge once the chests hold the silver and ectoplasm it takes - without going out for them, and keeping its quality and durability. One Viking at a time, sharing it with upgrading.

### Silver Arrows

With Zombie Apocalypse, checked every 15 seconds, an archer whose bow would hurt the dead more with silver arrows, down to fewer than 20, takes up to 50 from a chest - or crafts them, when the chests hold all but the wood, until they have 50. Silver arrows don't count toward their 100.

### Build a Beehive

With fewer than 6 beehives in the village, a queen bee carried or in a chest, the wood for one, and no thunderstorm, one Viking at a time builds one: they take the queen, get 10 wood, and build it in the open, on flat ground in the village's core that its bees take to, off the paths, out of houses and pens and at least 5 meters from any other hive. The village repairs and rebuilds it like its other pieces.

## Rolled Jobs

With no work, a Viking picks one of these at random, by weight among those they can do:

| Job | Weight | When |
| --- | --- | --- |
| Talk | 25 | Another Viking of the village within 30 meters is strolling or standing about |
| Pet a boar | 10 | The village has livestock, it isn't raining, and fewer are petting than one for every 6 Vikings |
| Stock the chests with wood | 15 | A chest has room for wood, they've found wood in the last minute, and fewer are stocking than one for every 4 Vikings |
| Forage | 15, or 60 while the chests hold fewer than three foods for each Viking | A beehive has honey, or there's something to forage, and fewer are foraging than one for every 4 Vikings |
| Get rested again | 15 | Not rested, before 0.6 of the day, no rain at all, and the village has a gathering fire |
| Hunt | 15, or 60 while the chests hold fewer than three foods for each Viking | Not below three quarters of their health, prey to hunt, and fewer hunting than one for every 5 Vikings |
| Stroll | 40 | It isn't raining |
| Stand about | 25 | Always |

So with nothing else to do, a Viking talks, strolls or stands about - 25, 40 and 25 in 90. The Swamp's own rain, which hardly lets up, doesn't stop them strolling, petting or talking outside, but it does keep them from getting rested again.

### Talk

They pick a Viking at random within 30 meters who's strolling or standing about, who stops to talk, and both walk to within 1.8 meters for 10-30 seconds - counting from when they set out. They take turns of 3-6 seconds, each with a line, gesturing at three in ten of them, about a newcomer, a quest one of them came back from or is out, or anything else - the time of day, the weather, a fight in the last 5 minutes, being hurt, their beards, the village and its dead, what's in the chests, the village their portal leads to. See [DIALOG.md](DIALOG.md).

In the rain, they look for one standing under a roof first, or else take the other inside their own house to talk, clear of its doors, with 30 seconds to get there. With no house, they don't talk in the rain.

### Pet a Boar

One of the village's livestock - the animals of its farm's pens, so a village you started has none - picked at random inside the village. They walk to it, jumping into its pen, reach out, and stand facing it for 2.5 seconds as the hearts show.

### Stock the Chests With Wood

They gather up to 20 wood from the ground - branches and wood within 60 meters past the village's edge, never from another chest - as much as the chest has room for, and put it away there. Finding none, they leave it for a minute.

### Forage

They pick, the nearest first, within 60 meters past the village's edge and with no danger within 50 meters of it:

- Honey from a beehive of the village's, first every time
- Food that grows back, like berries and mushrooms - not crops you planted
- What the cauldron's meals take that grows back, like thistle and dandelions, when the village has a cauldron
- Flint
- Stone, only when they're after it for a hoe or axe

They only pick what they can carry and the chests have room for, jumping into pens for it, picking the way you do - a bonus by their skill, scaled by the world's resource rate, raising their farming skill. They stop after 10, honey included, or at dusk, and put it away. With Yagluth's power, they use it as they start.

They also forage, or hunt, for what crafting is short of - see [Craft Arrows](#craft-arrows), [Craft a Second Weapon](#craft-a-second-weapon) and [Make Paths](#make-paths).

### Hunt

An archer, or anyone in a village with no archer, hunts. What counts as prey for them is their biome's, every earlier biome's and the next biome's, standing on ground no harder than their own biome's:

| Biome | Prey | Only for archers |
| --- | --- | --- |
| Meadows | Boar, necks | Deer |
| Black Forest | Greylings | |
| Swamp | Draugr | Leeches |
| Mountains | Wolves | Drake hatchlings |
| Plains | Lox | Deathsquitos |
| Mistlands | Hares | |
| Ashlands | Asksvin | Voltures |
| Deep North | Moose, seals | |

Zombie prey counts too, young ones never, and archers also hunt birds. They take the nearest within 60 meters past the village's edge with no enemy within 50 meters of it - so a dangerous one like a lox or a wolf only with no other monster near it, its own kind included - or for an archer, a bird nearer than it.

1. They run there, and within 60 meters crouch and stalk it, swinging around to come at its back when that's 30-90° around
2. Fighting hand to hand, they take it on from 8 meters
3. An archer stops as far off as their arrows fly true - 15-50 meters - and shoots every 2.5 seconds, aiming ahead of it and over it for the drop, until it comes within 10 meters or after 8 arrows, then closes in
4. A bird they shoot at from 20 meters, every 2.5 seconds, for up to 6 arrows, and pick up its feathers
5. Once it's dead, they pick up what it dropped within 8 meters, for 20 seconds and as long as they keep finding more, fighting it if it rises

Prey that gets more than 80 meters past the village's edge, or onto harder ground, they leave for a minute, and look for more. They fight any greyling on the way, run from any other enemy within 50 meters, and run home below half their health. They give up after 3 minutes - but not while picking up the kill's drops - and at dusk. With Eikthyr's power, they use it as they leave the village.

#### Hunting Parties

From the Plains on, prey that isn't only for archers - the earlier biomes' too - is hunted by a party, archers or not, alone only the prey that is. A Viking with 2 or more fellows within 30 meters strolling or standing about, above three quarters of their health, leads up to 4:

1. They gather within 6 meters of the leader, for up to 30 seconds
2. The leader stalks the prey to 20 meters, the rest following within 5
3. They all attack at once. The one the prey goes after keeps just out of its reach while the rest fight it
4. Only the leader picks up the drops, the others going back to their day

The party breaks up below 3 before the fight, or when the prey gets away, and with the leader gone, another leads.

### Get Rested Again

Not rested, before 0.6 of the day, with no rain at all - the Swamp's own included - and a gathering fire in the village, they sit in its warmth the way they [rest by the fire](#rest-by-the-fire), a free seat first, until they're rested. Rain catching them out in the open, or dusk, ends it, and with nowhere warm to sit, or not resting after a minute sitting, they give up for a minute.

### Stroll

They walk to an open spot in the village's core - outside its houses and pens, out of the water and away from the fire. By day, a Viking who'd stand about inside a house strolls out instead, out of the way of its chests and door.

### Stand About

For 4-12 seconds, looking a new way every 3-6 seconds. In the rain, inside their house, at least 2 meters from its doors.
