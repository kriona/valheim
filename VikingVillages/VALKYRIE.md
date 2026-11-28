# Viking Villages - Valkyries

A valkyrie is the one who brings you to the world, and once you've befriended her she'll carry you on her back. The [README](README.md) has the short version of Viking Villages, and [LOGIC.md](LOGIC.md) everything else it does.

- [Controls](#controls)
- [Finding Valkyries](#finding-valkyries)
- [Talking](#talking)
- [Feeding](#feeding)
- [Riding](#riding)
- [Flying](#flying)
- [On the Ground](#on-the-ground)
- [Roosting](#roosting)
- [Fighting](#fighting), with what she does [in the air](#in-the-air), [hit and run](#hit-and-run), [her banish](#banish) and [her grab](#the-grab)

## Controls

Every control is one of the game's own, so it follows however you've bound it - the keys here are the game's defaults.

Getting on and off:

| Control | Default | What it does |
|---|---|---|
| Use, looking at her | E | Talks to her - the first time, she tells you her name. While she's angry with you, you can't |
| Use, looking at her back | E | Gets on her, once she's befriended and while she isn't angry with you |
| I, looking at her | I | Opens her inventory - her pack - beside yours, looking at her from in front or at her back from behind, once she's befriended and while she isn't angry with you. Both show it as "Inventory", as a Viking's does. I again closes it (see [Feeding](#feeding)) |
| Use, looking at nothing else | E | Gets you off her. Off her in the air more than 10 meters up, she catches you and carries you down (see [Riding](#riding)) |

Riding her:

| Control | Default | On the ground | In the air |
|---|---|---|---|
| Forward | W | Walks her the way you look, only while it's held | Flies her the way you look, and she keeps going once you let go |
| Backward | S | Stops her | Stops her, hovering |
| Left and right | A and D, or the arrow keys | Turns her to go that way - a quarter turn on its own, an eighth with forward | The same, flying |
| Run | Left Shift | Runs her | Flies her fast |
| Jump | Space | Takes off | Climbs |
| Crouch | Left Ctrl | - | Comes down, and lands her within 2 meters of the ground |
| Block, held | Right mouse | Turns her on the spot to face where you look | Climbs or comes down the way you look, at full speed from about 30 degrees up or down - jump and crouch win over it |
| Attack | Left mouse | Strikes with her claws | Swoops, raking with her claws as she flies past |
| Secondary attack | Middle mouse | Spins - but not while she's friendly where it would hit buildings | The same |

Let go of everything in the air and she hovers. Jump and attacking never get you off her, as they would any other mount.

## Finding Valkyries

- A traveling Viking from the Mistlands sometimes brings a valkyrie, who flies them in, roosts on a roof while they stay, and stays on with the village once her traveler dies (see [Their Animals](EVENTS.md#their-animals) in EVENTS.md)
- `kri_vv_spawnvalkyrie [color] [amount]` spawns a Valkyrie in the specified color, `spawn VikingValkyrie` spawns one in a random color
- The `tame` command befriends her (see [COMMANDS.md](COMMANDS.md))
- Each valkyrie is one of eight colors, picked at random as she's first spawned, each as likely: red, white, yellow, sky blue, pink, black, purple or orange, with every red of her body - the markings on her feathers and the rest - painted over in it.

## Talking

She's somewhere between a mount and a monster - a whole creature with her own mind. Taming her befriends her - she's willing to carry you then, and is friendlier.

- Every valkyrie has a name of her own from the moment she's spawned - one of the valkyries' names (see [Valkyries](NAMES.md#valkyries) in NAMES.md) - but keeps it to herself. Until she tells you, she's plain Valkyrie to you
- The use key talks to her, tamed or not, unless she's angry with you - then there's no prompt to, and she won't be talked to. You can't pet her or rename her, and her name shows no taming, wild or tamed, over her or on the mount panel while you ride her - only "Hungry" after it while she is
- The first time you talk to her, she tells you her name, and you know her by it from then on - each player learns it for themselves. She never speaks first
- Befriending her counts as her telling you her name: in place of the game's "Valkyrie has been tamed", the player nearest her, within 30 meters, is told "{name} has been befriended" and knows her by it from then on
- She says little. Wild, she's terse and neutral - "{name}.", "I am {name}.", "{name}. Remember it." when she tells you her name, then "Yes?", "Speak.", "What is it?", "Hm." or "I am listening." Tamed, she's terse but friendly - "I am {name}. Well met.", "{name}. Good to know you.", "Call me {name}.", then "Yes, friend?", "I'm here.", "Ready when you are.", "Good to see you." or "Shall we fly?"
- Hungry and befriended, she answers you about it in place of anything else, once she's told you her name - "I'm hungry, friend." or "Food, soon?" Wild, she keeps her hunger to herself

## Feeding

- She never picks food up off the ground the way other tame creatures do. She eats from her inventory - 5 columns and 2 rows of her own, which you open with I while looking at her once she's befriended (see [Controls](#controls)), and move things in and out of as you would a chest's
- Hungry, she eats one thing from her inventory, the first she'd eat in it, and is fed for 10 minutes, with the game's soothed effect. She eats anything a Viking would eat - whatever a player eats for health - and the raw version of any cooked food, like raw meat, whatever a cooking station turns into food
- She's spawned fed, with 5 raw Lox meat and 5 raw Seeker meat in her inventory
- "Hungry" shows after her name while she is - over her, and on the mount panel while you ride her
- A traveler's valkyrie's inventory is the traveler's, and isn't yours to open until she stays on with the village
- When she dies, everything in her inventory drops where she fell
- Being fed never befriends her, the way keeping other creatures fed tames them - wild, she shows none of the game's taming either
- She never breeds

## Riding

- She needs no saddle to be ridden - once she's befriended, you ride her on her bare back. Look at her back for the prompt to ride her. Until she's befriended, or while she's angry with you, you can't get on
- A traveler's valkyrie is still theirs to ride, not yours, until she stays on with the village
- When riding her, hold jump to take off and climb, and crouch to descend - she lands once she's within 2 meters of the ground. Steer and run as on any mount, flying slow or fast. Hover by letting go of everything
- On the ground she walks only while you hold forward (or left, right or run), and stops as soon as you let go. In the air she keeps going the way she was, as any mount does
- Left and right - the arrow keys, or your side keys and stick - turn her to go that way, on the ground or in the air: a quarter turn from where you look on their own, an eighth with forward
- Flying, hold block (right mouse) and she climbs or comes down the way you look - looking up or down more than a little, at full speed once you look about 30 degrees up or down. Jump and crouch win over it, and looking down far enough lands her
- Riding her, attack has her strike with her claws and secondary attack has her spin, on the ground or in the air - she turns to face where you look first. Each waits as long between uses as her own fighting does: 3 seconds for her claws and 10 for her spin
- In the air, her claws are a swoop instead, which rakes at once without rearing back first, so it lands on what you aimed at before she's flown past. It deals her claws' damage, reaches 4 meters and waits as long as her claws between uses
- Jump and attacking don't get you off her, but your other ways off a mount still do, mid-air too
- While she's friendly with you, you pass through her body rather than bump into it, so neither getting off her beside a cliff nor her moving about can shove you off a ledge - her attacks still hit you if she's turned on you, and yours hit her. Once she's angry with you, she's solid to you again
- While you ride her, the mount panel shows her own icon - a portrait of her head and shoulders, taken from her model the first time you ride one - and never another creature's. Should it fail to be taken, the panel shows no icon at all
- Get off her in the air more than 10 meters above the ground and she catches you in her talons, the way the valkyrie carries you in at the start of the game, shouting "Got you.", "I have you.", "Hold still." or "Careful." the way Vikings call out danger - up for 8 seconds, seen from 40 meters, with nothing she says covering it - flies you straight down with her wings spread wide, gently beating, at 20 meters a second while she's more than 50 meters above the ground or the water, slowing evenly to 7 meters a second - as fast as you run - by 30 meters and keeping to that the rest of the way, and lets go once your feet are within 1.5 meters of the ground or the water, so you land unhurt - the fall from where she caught you doesn't count. She doesn't when you wear a feather cape or anything else takes away all your fall damage, or over water deep enough to swim in, which breaks your fall anyway, unless there's land within 300 meters - then, more than 10 meters above the water, she catches you and carries you to the nearest shore at 15 meters a second, coming down the same way but no lower than 10 meters above the water, climbing over anything in her way, and once she's over land or at the shore, brings you straight down there. She lets go at most 10 seconds after she'd have flown you down from where she caught you, wherever she is - so however high you get off her, she brings you all the way down. Carrying you down comes before everything else - she picks no fights and makes no attacks until she's let go of you, though one she'd already started when she caught you finishes

## Flying

- She takes off tipping forward from standing into flight with her wings beating, and her landing is the flare and touchdown of the valkyrie's landing at the end of the game
- She beats her wings as she climbs, hovers and flies slowly, and glides as she comes down or flies fast, leaning into climbs, dives and turns
- With no rider, she flies only to fight (see [In the Air](#in-the-air)), and otherwise comes straight down and lands - but in a village, onto a roof (see [Roosting](#roosting))
- However she's flown - by you, her AI or a Viking riding her - she climbs over what's in her way. She looks ahead for anything solid her body would fly into, farther the faster she's flying (3 to 12 meters), and rises over it at full climbing speed, keeping a little of her way forward, or none once it's within 1.5 meters. She doesn't while she's coming down, so you can still bring her down onto a slope
- However she's flown, she keeps out of water and tar: she comes down no nearer than 1.5 meters above the surface, waves and all, and nearer than that she's pushed back up, at full climbing speed once her feet touch it. She can't land on deep water. Should she end up in it while flying, she flies straight up out of it rather than swimming
- A Viking riding her flies her somewhere: she takes off, climbs to 15 meters above the ground and flies straight there, fast while it's more than 30 meters off. Once she's within 3 meters of it across the ground, she hovers over it or comes straight down and lands on it. While she's flown this way she neither lands on her own nor flies off to fight, though catching a falling rider still comes first
- Vikings she flies over - 3-40 meters below her and within 15 meters to the side - shout their surprise, one at a time every 5 seconds and each at most every 2 minutes, about her or about you riding her, by name when they know you. Her own traveler, and Vikings asleep or fighting, say nothing (see [DIALOG.md](DIALOG.md#speech))

## On the Ground

- She stands like the valkyrie at the end of the game, her feet on the ground, and waddles slowly, even running
- With nothing to do, she mostly stands where she is, taking a step or two every half minute or so
- Standing about, she stretches her wings wide every 20 to 45 seconds, spreading them out to her sides as she does gliding, beating them slowly and gently a moment and folding them again
- She calls out now and then while she's idle - half the time every 10 seconds
- She never slides down a slope she stands on, ridden or not - standing still, she holds her footing on slopes up to about 72 degrees

## Roosting

Any valkyrie in a village roosts on its roofs rather than standing about on the ground:

- Coming down with no rider in a village - after a fight, say - she flies to the nearest roof she can stand on, within 30 meters and the village, and lands there instead of coming straight down. On the ground in a village once she's had nothing to fight for 4 seconds after a fight, she flies up to one too. She looks again every 15 seconds when there's none
- A roof she can stand on is part of a building, at least 2.5 meters above the ground, sloping 30 degrees at most - a gentle roof or a flat top - with roof all around her 1.2 meters out at much the same height, and 5 meters clear above. Steep roofs and small huts are passed over, and she comes down on the ground as she would anywhere else when there's no roof for her
- On a roof, she stands where she is rather than wandering off it, still watching for enemies - one comes her way and she's off the roof to fight it, and back up on one once it's done
- Riding her, you land her where you like - she only roosts on her own, and getting off her in a village leaves her where you got off

## Fighting

- She's on the Dvergr's side: friendly to you until you hurt her, and then she turns on you and stays angry, the way the Dvergr do. Hurting a Dvergr or Dvergr Viking within 20 meters of her turns her on you too, and so does taking from the Dvergr's things near her. She fights the monsters the Dvergr fight
- She only takes up enemies near her: she sees 15 meters, hears an enemy only within 10 meters, and is alerted by an enemy she sees within 15 meters. One that hurts her she still turns on
- She answers a call for help the way the Vikings do - when a player or Viking is hurt by a monster, or a badly hurt Viking shouts for help - going after the attacker from as far as 40 meters off, when she has no rider, isn't carrying anyone and has nothing else to fight. Wildlife nobody's fighting she leaves alone
- She fights with her claws and spin, going straight for an enemy she spots without rearing up and screeching at it first:
  - Her claws deal 100 slash, 50 pierce, 50 chop and 20 spirit damage, and reach 3.5 meters from where she stands, as far as her talons get with her feet planted
  - Her spin deals 70 blunt, 130 pierce, 100 chop, 100 pickaxe and 40 spirit damage in a whirl of white. It hits everything within 8 meters of her feet, all the way around and up, and her AI spins at an enemy within 8.33 meters
  - Her claws, swoop and spin cut trees and mine rock as well as a bronze axe and pickaxe do - birches and the Swamp's ancient trees too - though not the Mistlands' Yggdrasil shoots, and never a standing oak
  - Her spin's chop and pickaxe damage tear down buildings, so while she's friendly - befriended, or not angry with you - she never spins where it would reach a building, a village's or yours, using her other attacks instead. Riding her, your secondary attack doesn't spin her there either: she says "Not here.", "Too close." or "Not near the halls." Angry, she spins wherever she likes
- Striking with her claws on the ground, she keeps her feet planted and rears back only a little
- She goes after the enemies that can strike her in the air first - the nearest she can see, within her 15 meters, with an attack that throws or shoots, like an archer or a fuling shaman, or a player holding a bow, crossbow, staff or spear - and only then the nearest of the rest. Greydwarves' thrown stones hurt her too little to count
- She's immune to spirit damage and never staggers, resists lightning, and takes fire and poison like any other damage. Chopping and mining don't hurt her
- She dies with a cry and falls limp from where she was. Her body lies 5 seconds, then vanishes in a burst of smoke, dropping 2-4 Celestial Feathers
- With [Zombie Apocalypse](https://thunderstore.io/c/valheim/p/Kriona/ZombieApocalypse/), a valkyrie who rises from her body comes back as a Zombie Valkyrie - a zombie Fallen Valkyrie, with its body, its fighting and its attacks - by Zombie Apocalypse's rules for any slain creature. Her body stays until she's up, never vanishing in smoke, and a tamed valkyrie rises only with its `TamedCreaturesRise` on

### In the Air

With no rider, she takes off to fight from the air:

- A target that flies - a Deathsquito, bat, drake or Fallen Valkyrie
- A target she can't walk to - up a cliff, on a roof or across water - checked every 2 seconds
- A target more than a few steps past where her claws reach on the ground - 7 meters from its edge - which she flies to rather than waddling
- A creature that can't strike at range, which she fights hit and run (see [Hit and Run](#hit-and-run))
- Now and then in any fight on the ground - every 10 seconds there's a 15% chance she takes to the air for 10 to 15 seconds

She stays up at least 4 seconds once she takes off, flying at least 3 meters above the ground, and lands once she can fight her target on the ground again, or 3 seconds after her fight's over. Her rider always decides for her instead.

In the air she has attacks of her own as well as her spin:

- Her claws, raked as she hovers, reaching 6 meters from below her, dealing her claws' damage
- Her swoop, dealing her claws' damage, every 6 seconds
- A spinning volley of feathers from 5 to 20 meters, every 10 seconds - 8 feathers loosed from her chest in a tight burst aimed at her target, up or down, 80 pierce damage each. Each flies straight at 45 meters a second, neither slowing nor falling, led at where her target will be when it gets there, strays at most 1.5 degrees, and hits what it passes within 0.3 meters of. One that misses stays stuck where it lands for 10 seconds
- A screech that knocks back hard, 20 slash and 20 pierce damage within 5 meters, every 9 seconds

She strikes with her claws, swoop and screech however close she gets - even against something as big as Moder - and keeps her feather volley for a distance. Between strikes she hovers by her target, facing it.

### Hit and Run

A creature that can't strike at range - one with no attack that throws or shoots, like a wolf, boar, troll or draugr with an axe, or a greydwarf, whose thrown stones hurt her too little to count - can only hurt her when she's within its reach. With no rider, she fights one from above it:

- She hovers out of its reach - above its head by as far as its longest attack reaches, and 2 meters more - and 6 meters off to one side, facing it, loosing her feather volley whenever it's ready
- Whenever her swoop is ready, she dives at it, swoops once it's within 4 meters, and climbs straight back out of reach. A dive that doesn't get there in 4 seconds is given up
- She never fights a player this way, since a player can carry a bow, nor anything else that throws or shoots - an archer, a fuling shaman - which she fights the way she does any other target

### Banish

With no rider, she banishes the undead around her whenever two or more undead enemies are within 10 meters of her, on the ground or in the air - the undead being creatures of the Undead faction, like draugr, skeletons, ghosts and Zombie Apocalypse's zombies, or any weak to spirit damage:

- She casts a spirit nova with her spin, her spin's white whirl twice as bright, and a white ring bursting out 10 meters around her
- The nova is the spirit nova Zombie Apocalypse gives the Dvergr's mages - 112 spirit damage to everything hostile it reaches, as often as they cast it, every 7 seconds - but reaching twice as far
- She has it whether or not Zombie Apocalypse is installed

### The Grab

With no rider, she snatches an enemy in her talons, carries it up and drops it:

- She grabs at most once every 15 seconds, from the ground or the air, when her target is within 4 meters of her and stands over solid ground - never over water or lava - so it always lands on something hard
- The grab is a quick strike dealing 10 slash and 10 spirit damage. If it lands, she takes hold of her target and flies straight up with it for 2 seconds - about 20 meters - then lets go
- Once she lets go, she hovers and watches it fall, striking at nothing, until it lands or dies - at most 3 seconds - so the fall does its work before she fights on
- A player who's rolling as the grab lands isn't taken hold of, but still takes its damage if it hits
- A creature that falls hurts as much as a player falling as far would - nothing up to 4 meters, rising evenly to 100 at 20 meters - and the fall counts as her attack. A player takes the game's own fall damage
- Once dropped, a creature or player can't be grabbed again by any valkyrie for 60 seconds

She grabs players only when she's angry with them, and never one wearing a Feather Cape. Of the creatures, she grabs only those as tall as a Fenring or shorter and not much wider than a Greydwarf Brute, that don't fly, and none that are bosses, oozes, serpents, ghosts, shadows or roots and tendrils fixed in the ground:

- Asksvin Hatchling
- Baby Seal
- Boar and Piggy
- Captive Fuling
- Charred Marksman, Charred Twitcher, Charred Warlock and Charred Warrior
- Chicken and Hen
- Cultist
- Deer
- Draugr and Draugr Elite
- Fallen Warrior
- Fenring
- Fuling and Fuling Shaman
- Greydwarf, Greydwarf Brute and Greydwarf Shaman
- Greyling
- Hare
- Leech
- Neck
- Rancid Remains
- Seeker and Seeker Brood
- Skeleton
- Surtling
- Tick
- Ulv
- Wolf and Wolf Cub
