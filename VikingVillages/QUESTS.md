# Viking Villages - Quests

Everything a village's quests do, with the numbers they go by. The [README](README.md) has the short version, and [DIALOG.md](DIALOG.md) what the Vikings say about them.

## How a Quest Is Picked

A village looks at its quest every 10 seconds, on the client that runs it, and sends one out when it has none out, it rested a quarter of a game day since the last came back, no enemy is within 30 meters, and it's between waking (about 6 in the morning) and about 2:30 in the afternoon (60% of the way through the day). A quest picked later waits for the next morning. Hunting, foraging and cutting trees are always home before night falls at 6 in the evening, doing only as much as the daylight left allows - and wait for the next morning too without daylight enough for the least they're worth going for: 3 prey each, 4 plants each, or finding the trees and a minute of cutting.

It picks the first of these that it can go on:

1. The next step of the village's goal, when that step is a quest - ore for bronze or iron, core wood, an antler from Eikthyr, with Zombie Apocalypse ectoplasm for lining a weapon with silver, and so on
2. Hunting or foraging, taking turns, when the chests hold fewer than three foods for each Viking and nothing's been found around the village for half a day, or the chests hold less than one food for each Viking - or as soon as it runs low when the village can't be left
3. Cutting trees for 50 wood when it's out of wood and finds none to gather - or as soon as it runs low when the village can't be left
4. Selling treasure to the nearest trader once its amber, pearls, rubies and silver necklaces are worth 100 coins
5. Buying a Dverger circlet, from the Swamp on, at most once every 7 game days, when a Viking still uses torches and the coins and treasure will pay for it

A village can only go on a quest when its biome isn't easier than the quest's or the item's, it has the stations the quest needs, and it can spare the Vikings with 2 left at home. The healthiest go, those with the quest's tool first.

## The Quests

Vikings is how many go - the second number from the Plains on - and up to how many go when the village has more of the tool, or for a hunt, when the village can spare them. Work is how long they're at it once they're there, before the trip there and back, in real time - which is the world's time - and Game time is the same in the game's hours, at the default day of 30 real minutes, each real minute 48 game minutes. Danger is the share of a Viking's health it would lose in half an hour of the work, with the group the quest takes and gear of the quest's biome - see [How Hurt They Come Home](#how-hurt-they-come-home) for what that comes to.

| Quest | Biome from | Vikings | Needs | Goes to | Work | Game time | Danger | Brings back |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Hunt | Meadows | 1-3, 3-5 from the Plains - the extras only when the village can spare them | Daylight for 3 prey each, home before night | Ground of the biome they hunt in - see [Hunting](#hunting) - at least 150 m away when it's the village's own | 90 s a prey, for the hunter with the most | 1 h 12 min a prey | 20% | 3-10 prey each - see [Hunting](#hunting) |
| Forage | Meadows | 1, 2 from the Plains | Daylight for 4 plants each, home before night | Ground of the biome they forage in - see [Foraging](#foraging) - at least 150 m away when it's the village's own | 75 s a plant, up to 15 min | 1 h a plant, up to 12 h | 10% | 4-12 plants each - see [Foraging](#foraging) |
| Cut trees | Meadows | 1, 2 from the Plains, up to 3 | An axe that cuts the tree, and daylight for finding the trees and a minute of cutting, home before night | Ground of the tree's biome - the Meadows' birches for fine wood, the Black Forest's pines for core wood, the Swamp's ancient trees for ancient bark, and the village's own biome for wood | 2 min, and as long as their axes take | 1 h 36 min, and as long as their axes take | 10% | See [Cutting trees](#cutting-trees) |
| Mine copper | Black Forest | 2, up to 4 | A pickaxe that breaks copper, a smelter and a kiln | Ground of the Black Forest | 3 min, and as long as their pickaxes take | 2 h 24 min, and as long as their pickaxes take | 20% | See [Mining](#mining) |
| Mine tin | Black Forest | 2, up to 4 | A pickaxe that breaks tin, a smelter and a kiln | Ground of the Black Forest | 3 min, and as long as their pickaxes take | 2 h 24 min, and as long as their pickaxes take | 15% | See [Mining](#mining) |
| Mine silver | Mountains | 3, up to 5 | A pickaxe that breaks silver, a smelter and a kiln | Ground of the Mountains | 10 min, and as long as their pickaxes take | 8 h, and as long as their pickaxes take | 50% | See [Mining](#mining) |
| Raid sunken crypts | Swamp | 3, up to 5 | A pickaxe that breaks muddy scrap piles, a torch or circlet, a smelter and a kiln | The nearest sunken crypts | 30 min, and as long as their pickaxes take | 24 h, and as long as their pickaxes take | 60% | See [Sunken crypts](#sunken-crypts) |
| Raid burial chambers | Black Forest | 2, up to 3 | A torch or circlet | The nearest burial chambers | 15 min | 12 h | 50% | See [Burial chambers](#burial-chambers) |
| Hunt ghosts | Black Forest | 2, up to 3 | A torch or circlet, and Zombie Apocalypse, whose silver-lining takes ectoplasm | The nearest burial chambers | 15 min | 12 h | 50% | See [Ghosts](#ghosts) |
| Hunt trolls | Black Forest | 3 | A torch or circlet | The nearest troll caves | 15 min | 12 h | 80% | See [Trolls](#trolls) |
| Hunt goblins | Plains | 3, up to 5 | A blast furnace and a kiln | The nearest fuling villages | 20 min | 16 h | 80% | See [Fulings](#fulings) |
| Kill Eikthyr | Meadows | 2 | 2 deer trophies, which they leave on his altar to summon him - hunted for first when the village has fewer | Eikthyr's altar | 5 min | 4 h | 80% | See [Eikthyr](#eikthyr) |
| Find bees | Meadows | 1 | Fewer than 6 beehives in the village | Ground of the Meadows | 10 min | 8 h | 15% | See [Bees](#bees) |
| Sell treasure | Meadows | 2 | | Whichever trader they get to soonest of those the village can sell to - Hildir from the Meadows on, Haldor from the Black Forest on and the Bog Witch from the Swamp on | 2 min | 1 h 36 min | 5% | See [Traders](#traders) |
| Buy a circlet | Swamp | 2 | Coins and treasure enough | Haldor | 2 min | 1 h 36 min | 5% | See [Traders](#traders) |

## What They Bring Back

Every number is how many they bring home on average, worked out from the game's own drop tables the way the quests roll them, at the world's default resource rate - a world with more or less resources brings home more or less of everything. A thing they bring home less than once a quest is shown as how often, like "1 in 12". The numbers are for Vikings with no skill: each hunter brings home more by their own bow skill, and each forager or bee finder by their own farming skill, up to half as much again at 100 - so a hunter at 50 brings home a quarter more than the lists show, whoever else went.

### Hunting

A hunt goes after what the village wants, in a biome no harder than the village's whose prey drops it, and takes only the prey that drops it - a Plains village short of leather scraps goes to the Meadows for boar, and never brings home anything of the Mountains. When more than one biome has it, like feathers, which every biome with birds has, they go to whichever they get to soonest. Hunting for food, it goes to the hardest biome no harder than the village's whose prey has meat, and takes only those - the Black Forest and the Swamp have none, so their villages hunt in the Meadows. Each hunter takes 3-10 prey - as many as they can carry, as long as they're home before night. Some prey take more or less time to hunt than others: the Meadows' boar, necks and deer two thirds of a prey's time, a lox 8/3 and the Deep North's moose and seals 2 - so a hunter with daylight for 8 prey takes 12 in the Meadows, 3 lox or 4 moose and seals - and those that take longer are taken that much less often on the road. Each hunter's kills raise the skill of their bow, or with none, of the weapon they carry, the same as for every quest that fights. The numbers are for each hunter, with daylight for 8 prey, the most a hunt setting out in the morning has - fewer late in the day, or when their packs fill first - so the whole hunt brings home that many times the hunters who went: 1-3 of them, or 3-5 from the Plains on, like 45-75 lox meat for a Plains village.

For food:

A hunt for food takes the biome's prey with meat at random, each kind as often as the food it drops - a deer, with 2 meat, twice as often as a boar, with 1, so where there's more than one kind, the meatier ones make up most of the kill. Each biome below says how many of each kind make up the 8 prey.

- Meadows, Black Forest or Swamp village - in the Meadows, about 6.5 deer, 3.2 boar and 2.3 necks
  - Deer meat - 13
  - Deer hide - 13
  - Boar meat - 3.2
  - Leather scraps - 3.2
  - Deer trophy - 3.2
  - Neck tail - 1.6
  - Boar trophy - 0.5
  - Neck trophy - 0.1
- Mountains village - 8 wolves
  - Wolf pelts - 12
  - Wolf meat - 8
  - Wolf fangs - 3.2
  - Wolf trophy - 0.8
- Plains village - 3 lox
  - Lox meat - 15
  - Lox pelts - 7.5
  - Lox trophy - 0.3
- Mistlands village - 8 hares
  - Scale hide - 16
  - Hare meat - 8
  - Hare trophy - 0.4
- Ashlands village - about 4.7 asksvin and 3.3 voltures
  - Asksvin tails - 11.8
  - Asksvin hide - 11.8
  - Asksvin bladders - 4.7
  - Feathers - 4.1
  - Volture meat - 3.3
  - Volture eggs - 2.5
  - Asksvin trophy - 0.5
  - Volture trophy - 0.3
- Deep North village - about 2.7 moose and 1.3 seals
  - Moose meat - 13.3
  - Moose hide - 6.7
  - Moose sinew - 6.7
  - Seal blubber - 3.3
  - Seal pelts - 3.3
  - Moose trophy - 0.3
  - Seal trophy - 0.1

For what the village wants:

A hunt for an item the village's next gear needs, like deer hide, takes only the prey that drops it, so every one of the prey is that kind - and the hunters bring home all it drops, not only what they went for. A hunt for deer hide brings home 24 deer meat besides, where a food hunt in the Meadows brings home about 18 meat in all - 13 deer meat, 3.2 boar meat and 1.6 neck tails.

- Boar meat or leather scraps - 12 boar in the Meadows
  - Boar meat - 12
  - Leather scraps - 12
  - Boar trophy - 1.8
- Deer hide, deer meat or a deer trophy - 12 deer in the Meadows
  - Deer hide - 24
  - Deer meat - 24
  - Deer trophy - 6
- Feathers - 8 birds in the nearest biome with them: crows and seagulls in the Meadows and the Black Forest, seagulls in the Plains, and ash crows and voltures in the Ashlands, about 4 of each
  - Feathers - 24 in the Meadows, the Black Forest and the Plains, and about 17 in the Ashlands, with volture meat, eggs and trophies besides
- Wolf pelts, from the Mountains on - 8 wolves, as for food above
- Lox pelts, from the Plains on - 3 lox, as for food above

### Foraging

Foraging goes after what the village wants, in the biome it grows in, and picks only that. Foraging for food, it goes to the hardest biome no harder than the village's where food grows, and picks only what's food - leaving dandelions and thistle. Each forager picks 4-12 plants - as many as they're home before night for - 1-2 of its fruit each, each plant raising their farming skill as picking one raises yours. The numbers are for one forager picking 12 plants, a full day's foraging.

For food:

- Meadows village - in the Meadows
  - Raspberries - 9
  - Mushrooms - 9
- Black Forest, Swamp or Mountains village - in the Black Forest
  - Blueberries - 9
  - Yellow mushrooms - 9
- Plains village or harder - in the Plains
  - Cloudberries - 18

For what the village wants - raspberries, mushrooms or dandelions in the Meadows, blueberries, thistle or yellow mushrooms in the Black Forest, cloudberries in the Plains:

- What they went for - 18

### Cutting Trees

As much of the wood they went for as they can carry, or as they cut in time to be home before night, unless their axes wear out first. A Viking carries about 300 less their gear, food and potions and their axe - room for about 120 wood, fine wood, core wood or ancient bark each, all of them weighing 2.

- Wood, fine wood, core wood or ancient bark - about 120 each

### Mining

As much ore as they can carry, unless their pickaxes wear out first - about 245 weight's worth each, once their gear, food and potions and pickaxe are counted.

- Copper ore - about 24 each, weighing 10
- Tin ore - about 30 each, weighing 8
- Silver ore - about 17 each, weighing 14

### Sunken Crypts

Iron scrap from the muddy scrap piles, as much as they can carry - about 24 each, weighing 10 - and what 1 crypt chest, 2 draugr and 1 blob for each of them drop, at least 1 chest, 2 draugr and 1 blob. The iron scrap the chests hold is left out, since their pickaxes get it. The numbers are for 3 Vikings, with 3 chests, 6 draugr and 3 blobs - more go with more pickaxes, and bring home a share more each.

- Iron scrap - about 24 each
- Coins - 52
- Ancient bark - 33
- Poison arrows - 16
- Ironhead arrows - 16
- Entrails - 6
- Ooze - 4.5
- Amber - 3.9
- Chains - 2.6
- Rubies - 2.6
- Amber pearls - 2.6
- Withered bones - 0.7
- Draugr trophy - 0.6
- Blob trophy - 0.3
- Iron protection idol - 0.2
- Iron battle idol - 1 in 12
- Silver battle idol - 1 in 60
- Silver protection idol - 1 in 65

### Burial Chambers

What 2 chests and 2 skeletons for each of them drop, at least 2 of each, and a surtling core stand each. The numbers are for 2 Vikings, with 4 chests, 4 skeletons and 2 surtling core stands - a third Viking brings half as much again.

- Coins - 44
- Flinthead arrows - 16
- Feathers - 12
- Amber - 4.4
- Bone fragments - 4
- Rubies - 3.3
- Surtling cores - 2
- Silver necklaces - 0.6
- Skeleton trophy - 0.4
- Bronze protection idol - 0.3
- Bronze battle idol - 0.1
- Iron battle idol - 1 in 34
- Iron protection idol - 1 in 34

### Ghosts

What 2 ghosts and a skeleton for each of them drop, at least 2 and 1. The numbers are for 2 Vikings, with 4 ghosts and 2 skeletons - a third Viking brings half as much again.

- Ectoplasm - 12
- Bone fragments - 2
- Ghost trophy - 0.4
- Skeleton trophy - 0.2

### Trolls

What half a troll for each of them drops, at least 1 troll. The numbers are for 3 Vikings, with 2 trolls.

- Coins - 50
- Troll hide - 10
- Troll trophy - 1

### Fulings

What 2 fulings, 1 fuling archer and a third of a fuling berserker for each of them drop, at least 2, 1 and 1. The numbers are for 3 Vikings, with 6 fulings, 3 archers and 1 berserker - 5 Vikings take on 10, 5 and 2, and bring home about 1.7 times as much.

- Coins - 29
- Black metal scrap - 18
- Fuling trophy - 0.9
- Fuling totem - 0.1
- Fuling berserker trophy - 1 in 20

### Eikthyr

What Eikthyr drops, but never his trophy - no boss's trophy ever comes home from a quest, and it never counts as you having killed him. The first of them takes 2 deer trophies from the village's chests and leaves them on his altar to summon him, as you would. A village with fewer goes hunting deer for their trophies first, as the next step toward what it wanted the hard antlers for - each deer drops one half the time, so one hunt nearly always brings back enough.

- Hard antlers - 3

### Bees

A wild hive for each of them. The numbers are for the 1 Viking it takes.

- Honey - 4.5
- Queen bees - 1.5

### Traders

Selling treasure, the first of them takes along the village's amber, amber pearls, rubies and silver necklaces and comes home with what the trader pays for them - every trader pays what it's worth, as they do you. They go to whichever trader's camp they get to soonest of those the village can sell to: Hildir's camp in the Meadows for any village, Haldor in the Black Forest from the Black Forest on, and the Bog Witch's camp in the Swamp from the Swamp on - so a Meadows village only ever sells to Hildir. Those who went name the trader when they talk about it. Buying a circlet, they always go to Haldor, who sells them, taking along the coins and treasure it costs.

- Selling treasure - coins for the treasure, at least 100 worth, since that's when they go
- Buying a circlet - a Dverger circlet, worn home by the one who needed it, and the coins left over

## How Hurt They Come Home

Each table goes by the biome a Viking's gear comes from - the quest's own biome and the two after it - and for the fewest and the most of them that go, like "2:" and "3:", gives the harm a Viking takes, how much of their health is still missing when they get home ("hurt after"), and their chance of dying out there. A Viking's harm doubles for each biome the quest is harder than their gear and halves for each easier, and goes down with more of them along - rounded down to a whole percent, so far easier quests do none. On the way home their wounds close, so a Viking who ate enough comes home missing 30% of the harm they took, and one who went hungry 70% - more than twice the numbers below - and one who falls below a quarter of their health drinks a healing potion if they took one. Harm past what they have leaves them at 10% of their health. The hunts are for a full day's hunting - 8 prey's time - and go by the biome they hunt in, for the fewest and most hunters a village of that biome sends.

A Viking can die on a quest, though it's rare: 7.5% for the square of the share of their health the quest harms them by, and never when that's under 1% - shown as "-" - so only a quest that takes more than a third of their health can kill them, like a sunken crypt raid at their own biome, at 2.7%. One who dies doesn't come home: the others mourn them by name as they get back, the village remembers them, the neighbors hear of it, and in time another Viking comes in their place - what they carried, and any tool they took, is lost with them. A Viking whose gear is so far behind the quest that they'd be expected to lose all their health never goes on it - like one of the Swamp's gear or easier on a fuling raid - and the quest waits for enough Vikings who can.

### Kill Eikthyr

The Meadows, always 2 of them

| Viking's gear | 2: harm | 2: hurt after | 2: died |
| --- | --- | --- | --- |
| Meadows | 13% | 3.9% | - |
| Black Forest | 6% | 1.8% | - |
| Swamp | 3% | 0.9% | - |

### Raid Burial Chambers

The Black Forest

| Viking's gear | 2: harm | 2: hurt after | 2: died | 3: harm | 3: hurt after | 3: died |
| --- | --- | --- | --- | --- | --- | --- |
| Black Forest | 25% | 7.5% | - | 20% | 6% | - |
| Swamp | 12% | 3.6% | - | 10% | 3% | - |
| Mountains | 6% | 1.8% | - | 5% | 1.5% | - |

### Hunt Ghosts

The Black Forest

| Viking's gear | 2: harm | 2: hurt after | 2: died | 3: harm | 3: hurt after | 3: died |
| --- | --- | --- | --- | --- | --- | --- |
| Black Forest | 25% | 7.5% | - | 20% | 6% | - |
| Swamp | 12% | 3.6% | - | 10% | 3% | - |
| Mountains | 6% | 1.8% | - | 5% | 1.5% | - |

### Hunt Trolls

The Black Forest, always 3 of them

| Viking's gear | 3: harm | 3: hurt after | 3: died |
| --- | --- | --- | --- |
| Black Forest | 40% | 12% | 1.2% |
| Swamp | 20% | 6% | - |
| Mountains | 10% | 3% | - |

### Raid Sunken Crypts

The Swamp

| Viking's gear | 3: harm | 3: hurt after | 3: died | 5: harm | 5: hurt after | 5: died |
| --- | --- | --- | --- | --- | --- | --- |
| Swamp | 60% | 18% | 2.7% | 46% | 13.8% | 1.6% |
| Mountains | 30% | 9% | - | 23% | 6.9% | - |
| Plains | 15% | 4.5% | - | 11% | 3.3% | - |

### Hunt Goblins

The Plains

| Viking's gear | 3: harm | 3: hurt after | 3: died | 5: harm | 5: hurt after | 5: died |
| --- | --- | --- | --- | --- | --- | --- |
| Plains | 53% | 15.9% | 2.1% | 41% | 12.3% | 1.3% |
| Mistlands | 26% | 7.8% | - | 20% | 6% | - |
| Ashlands | 13% | 3.9% | - | 10% | 3% | - |

### Hunt in the Meadows

| Viking's gear | 1: harm | 1: hurt after | 1: died | 3: harm | 3: hurt after | 3: died |
| --- | --- | --- | --- | --- | --- | --- |
| Meadows | 8% | 2.4% | - | 4% | 1.2% | - |
| Black Forest | 4% | 1.2% | - | 2% | 0.6% | - |
| Swamp | 2% | 0.6% | - | 1% | 0.3% | - |

### Hunt in the Mountains

| Viking's gear | 1: harm | 1: hurt after | 1: died | 3: harm | 3: hurt after | 3: died |
| --- | --- | --- | --- | --- | --- | --- |
| Mountains | 8% | 2.4% | - | 4% | 1.2% | - |
| Plains | 4% | 1.2% | - | 2% | 0.6% | - |
| Mistlands | 2% | 0.6% | - | 1% | 0.3% | - |

### Hunt in the Plains

| Viking's gear | 3: harm | 3: hurt after | 3: died | 5: harm | 5: hurt after | 5: died |
| --- | --- | --- | --- | --- | --- | --- |
| Plains | 8% | 2.4% | - | 6% | 1.8% | - |
| Mistlands | 4% | 1.2% | - | 3% | 0.9% | - |
| Ashlands | 2% | 0.6% | - | 1% | 0.3% | - |

### Hunt in the Mistlands

| Viking's gear | 3: harm | 3: hurt after | 3: died | 5: harm | 5: hurt after | 5: died |
| --- | --- | --- | --- | --- | --- | --- |
| Mistlands | 8% | 2.4% | - | 6% | 1.8% | - |
| Ashlands | 4% | 1.2% | - | 3% | 0.9% | - |
| Deep North | 2% | 0.6% | - | 1% | 0.3% | - |

### Hunt in the Ashlands

| Viking's gear | 3: harm | 3: hurt after | 3: died | 5: harm | 5: hurt after | 5: died |
| --- | --- | --- | --- | --- | --- | --- |
| Ashlands | 8% | 2.4% | - | 6% | 1.8% | - |
| Deep North | 4% | 1.2% | - | 3% | 0.9% | - |

### Hunt in the Deep North

| Viking's gear | 3: harm | 3: hurt after | 3: died | 5: harm | 5: hurt after | 5: died |
| --- | --- | --- | --- | --- | --- | --- |
| Deep North | 8% | 2.4% | - | 6% | 1.8% | - |

## Where They Go and How Long They're Gone

- A quest that goes to a kind of place - crypts, troll caves, fuling villages, traders, Eikthyr's altar - looks at the nearest 6 of them in a straight line. One that goes to ground of a biome looks for the nearest of it each of 8 ways out from the village, up to 6 km, as far as twice the nearest found
- The server plans the road to whichever of those they can get to soonest - over land, keeping out of biomes harder than the village's where there's a way around, across the sea in the best boat the village's biome has, and through any pair of joined portals that gets them there sooner. A raft in the Meadows, a karve in the Black Forest and the Swamp, a longship from the Mountains to the Mistlands, a drakkar from the Ashlands on - only the drakkar crosses the Ashlands' boiling sea
- The road home leaves out the portals that won't take what they bring back - ore, iron scrap and black metal scrap - unless the portal takes everything or the world lets every portal take everything. In a world with portals turned off, they use none
- A quest's road is planned ahead, as the village decides on it, and again once a game day, so portals built or broken since count. Its time is the road there, the work and the road back, give or take 2%. With no road found, it's 5 minutes each way
- As the first of them leaves, the server plans the whole trip again - the road there, the work and the road back - and the village's guess as it sent them gives way to when they'll really be back. What the village says of when they're back follows it
- The server follows them the whole way. A portal on their road that's broken or no longer joined to its other end has them find another way from wherever they've got to, and come back later for it
- Mining and cutting trees take as long as their tools take to break off as much as they can all carry home without being slowed - each tool by its damage and their skill, more tools getting it done sooner - on top of the work time above, which is for finding the ore or trees. Each tool is worn by every hit, and comes back worn or broken

## On the Way

- Each Viking takes along food for as long as they're gone and up to 2 healing potions - no more than half of any stack in the chests
- They're hurt by the quest's danger for as long as they're at it - twice as much for each biome the quest is harder than their gear, half for each easier, however many, and more with fewer of them than the quest takes, rounded down to a whole percent of their health, so a quest far easier than their gear does them no harm - and come home as hurt as their food couldn't keep up with, drinking a potion when they're badly hurt. A quest's biome is where it works - a hunt's or foraging's the biome they go to, so a Plains village's lox hunt is a Plains quest and its boar hunt a Meadows one, cutting trees the trees' biome, and selling treasure the trader's camp's. Those who come home come home at 10% health at least, and now and then one dies out there and doesn't come home at all - see [How Hurt They Come Home](#how-hurt-they-come-home)
- They pick a few things along the road - one for every 3 minutes they walk through a biome where something grows, up to 5 each, less one they ate for every 15 minutes on the road - and hunters take a prey for every 5 minutes they walk through a biome with prey, up to 2 each - a lox three eighths as often and a moose or seal half as often - bringing home what it drops. Only biomes no harder than each Viking's own count, so a Meadows Viking crossing the Plains finds nothing there. Those out to mine, raid or hunt ghosts only pick on the way home
- What they bring home is scaled by the world's resource rate, and their skills rise for the work they did - their weapon skill for every enemy they killed

## Coming Home

- They all walk back in together from where they left the village, however long apart they left - those who left first wait for the rest, giving up on one a few minutes after they're due when it never left
- They put what they brought home in the chests before anything else, even at night, and call their own houses home again
- A village that can't be left - walled in, or ringed by water - sends them from the spot nearest its edge they can reach, and they come back there
- A quest not back 2 game days past when it was due is given up on
- They talk about the quest for a game day after they're back, and about the trip itself for 12 game hours - hunting, foraging and cutting trees are too ordinary to talk about, though the trip can be. Other villages within 3 km hear of a quest worth talking about coming home
- Disband the village while they're out and they go on to the village the rest of its Vikings went to once they're done. Start a village at its bonfire again and they still go on there first, then turn around for home

## Testing

- `kri_vv_testlog quest` shows everything behind the quests of the village you're in, and the roads to quests it knows
- `kri_vv_testlog quest <kind> [item]` and `kri_vv_travel <kind>` start a quest of a kind at once - `MineCopper`, `ChopTrees FineWood` and so on
- `kri_vv_testlog quest due` brings the village's quest home now, once its Vikings have left
