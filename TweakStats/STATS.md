# Tweak Stats - Stats

The stats Tweak Stats can change, and the values they take. See the [README](README.md#configuration) for how to write them in the config file, and [NAMES.md](NAMES.md) for the names of every item, recipe, creature, build piece and status effect.

These are the most common stats - the [`tweakstats dump`](README.md#console-commands) console command lists every one something has, with its value.

- [Item Stats](#item-stats)
  - [Weapon Damage](#weapon-damage)
  - [Attacks](#attacks)
  - [Blocking](#blocking)
  - [Armor](#armor)
  - [Equipment Modifiers](#equipment-modifiers)
  - [Durability](#durability)
  - [Food and Meads](#food-and-meads)
  - [Other](#other)
- [Recipe Stats](#recipe-stats)
- [Creature Stats](#creature-stats)
  - [Body](#body)
  - [Behavior](#behavior)
  - [Taming](#taming)
  - [Drops](#drops)
  - [Creature Attacks](#creature-attacks)
- [Build Piece Stats](#build-piece-stats)
  - [Building](#building)
  - [Chests, Fires and Stations](#chests-fires-and-stations)
- [Status Effect Stats](#status-effect-stats)
- [Damage Types](#damage-types)
- [Damage Modifiers](#damage-modifiers)
- [Skills](#skills)
- [Item Types](#item-types)

## Item Stats

### Weapon Damage

| Stat | Is |
| --- | --- |
| `damages` | The damage of each type, e.g. `damages.slash` - see [Damage Types](#damage-types). An item whose damage adds up to more than 10000 is marked as a cheated item when it loads, which is saved with it and shown in its tooltip |
| `damagesPerLevel` | Damage added by each upgrade, of each type |
| `attackForce` | Knockback |
| `backstabBonus` | Damage multiplier for sneak attacks on unaware enemies |
| `toolTier` | Which trees and ores the item can harvest |
| `attackStatusEffect` | A status effect the item gives what it hits, e.g. `Burning` |
| `attackStatusEffectChance` | The chance of giving that status effect, from 0 to 1 |
| `skillType` | The skill the item uses and raises - see [Skills](#skills) |

### Attacks

`attack` is the normal attack and `secondaryAttack` the special attack (middle mouse), e.g. `attack.attackStamina`.

| Stat | Is |
| --- | --- |
| `attackStamina` | Stamina cost |
| `attackEitr` | Eitr cost |
| `attackHealth` | Health cost |
| `damageMultiplier` | Damage multiplier for this attack |
| `forceMultiplier` | Knockback multiplier |
| `staggerMultiplier` | Stagger multiplier |
| `lastChainDamageMultiplier` | Damage multiplier for the last hit of a combo |
| `attackRange` | Reach, in meters |
| `attackAngle` | Width of the swing, in degrees |
| `speedFactor` | Movement speed while attacking, where 1 is full speed |
| `attackHealthReturnHit` | Health regained per hit |
| `drawDurationMin` | Seconds to fully draw a bow |
| `drawStaminaDrain` | Stamina used per second while drawing a bow |
| `reloadTime` | Seconds to reload a crossbow |
| `projectileVel` | Speed of a fully drawn arrow or bolt |
| `projectileAccuracy` | Spread of a fully drawn shot, where lower is more accurate |
| `projectiles` | Projectiles fired per attack |
| `raiseSkillAmount` | Skill experience gained per hit |

### Blocking

| Stat | Is |
| --- | --- |
| `blockPower` | Damage blocked |
| `blockPowerPerLevel` | Block power added by each upgrade |
| `deflectionForce` | Knockback on attackers when blocking |
| `timedBlockBonus` | Block power multiplier for a parry |

### Armor

| Stat | Is |
| --- | --- |
| `armor` | Armor |
| `armorPerLevel` | Armor added by each upgrade |
| `damageModifiers` | Resistances and weaknesses, e.g. `damageModifiers.Frost = Resistant` - see [Damage Modifiers](#damage-modifiers) |
| `equipStatusEffect` | A status effect while worn, e.g. the Wolf fur cape's frost resistance |
| `setStatusEffect` | The set bonus's status effect |
| `setSize` | How many pieces of the set give the bonus |

### Equipment Modifiers

Fractions added while the item is worn, so `-0.05` is -5% and `0.1` is +10%.

| Stat | Is |
| --- | --- |
| `movementModifier` | Movement speed |
| `eitrRegenModifier` | Eitr regeneration |
| `heatResistanceModifier` | Resistance to the Ashlands' heat |
| `attackStaminaModifier` | Stamina used attacking |
| `blockStaminaModifier` | Stamina used blocking |
| `dodgeStaminaModifier` | Stamina used dodging |
| `jumpStaminaModifier` | Stamina used jumping |
| `runStaminaModifier` | Stamina used running |
| `sneakStaminaModifier` | Stamina used sneaking |
| `swimStaminaModifier` | Stamina used swimming |

### Durability

| Stat | Is |
| --- | --- |
| `maxDurability` | Durability |
| `durabilityPerLevel` | Durability added by each upgrade |
| `useDurabilityDrain` | Durability used by each attack or use |
| `durabilityDrain` | Durability used per second while equipped, e.g. torches |
| `canBeReparied` | Whether the item can be repaired - spelled this way in the game |
| `destroyBroken` | Whether the item is destroyed when its durability runs out |

### Food and Meads

| Stat | Is |
| --- | --- |
| `food` | Health |
| `foodStamina` | Stamina |
| `foodEitr` | Eitr |
| `foodBurnTime` | Seconds the food lasts |
| `foodRegen` | Health regained every 10 seconds |
| `consumeStatusEffect` | The status effect a mead gives |

### Other

| Stat | Is |
| --- | --- |
| `weight` | Weight |
| `maxStackSize` | Most that fit in one inventory slot - lowering it deletes whatever is over the new size, see [Stack Sizes](README.md#stack-sizes) |
| `maxQuality` | Highest upgrade level - the recipe also needs costs for the new levels |
| `value` | Coins the trader pays for one |
| `teleportable` | Whether the item can go through portals |
| `equipDuration` | Seconds to equip |

## Recipe Stats

| Stat | Is |
| --- | --- |
| `resources.<Item>` | How many of an item crafting needs, e.g. `resources.Iron = 10` - `0` removes it |
| `resources.<Item>.amountPerLevel` | How many more each upgrade needs |
| `amount` | How many one craft makes |
| `craftingStation` | The station that crafts it, e.g. `forge` or `piece_workbench`, or `none` to craft without one |
| `minStationLevel` | The station level needed |
| `repairStation` | The station that repairs it, when it isn't the crafting station |
| `enabled` | Whether the recipe can be crafted |

An ingredient added to a recipe only costs something at the first crafting level - set `amountPerLevel` too for upgrades to need it.

## Creature Stats

For `[Creature:...]` sections, which only apply in single player, when hosting, or on a server that has Tweak Stats - see [Multiplayer Servers](README.md#multiplayer-servers). Changes reach the creatures already in the world as soon as the config file is saved.

A creature's stats are spread over several parts - its body, its AI, its taming and its drops - and a stat's name finds it in whichever part has it. When two parts have a stat with the same name, put the part's name in front to choose, e.g. `MonsterAI.viewRange`; `tweakstats dump` writes the names this way when it's needed.

### Body

| Stat | Is |
| --- | --- |
| `health` | Health at one star - each star adds as much again, and creatures already at full health stay at full health |
| `damageModifiers` | Resistances and weaknesses, e.g. `damageModifiers.fire = Weak` or `damageModifiers = Resistant` for every damage type - see [Damage Modifiers](#damage-modifiers) |
| `speed` | Movement speed |
| `walkSpeed` | Walking speed |
| `runSpeed` | Running speed |
| `turnSpeed` | How quickly it turns |
| `swimSpeed` | Swimming speed |
| `flySlowSpeed`, `flyFastSpeed` | Flying speeds |
| `jumpForce` | Jump height |
| `regenAllHPTime` | Seconds to heal from nothing to full health - creatures heal all the time, even while fighting |
| `staggerWhenBlocked` | Whether it staggers when its attack is blocked |
| `staggerDamageFactor` | How much damage staggers it, as a fraction of its health |
| `faction` | Who it's friendly with, e.g. `ForestMonsters` or `Boss` |
| `boss` | Whether it's a boss, with a health bar at the top of the screen |
| `tolerateWater`, `tolerateFire`, `tolerateSmoke`, `tolerateTar` | Whether water, fire, smoke or tar don't hurt it |

### Behavior

| Stat | Is |
| --- | --- |
| `viewRange` | How far it sees, in meters |
| `viewAngle` | How wide it sees, in degrees |
| `hearRange` | How far it hears, in meters |
| `alertRange` | How far away it alerts others of its kind |
| `maxChaseDistance` | How far it chases before giving up |
| `minAttackInterval` | Fewest seconds between attacks |
| `fleeIfLowHealth` | Flees below this fraction of its health, where 0 never flees |
| `afraidOfFire`, `avoidFire` | Whether it runs from fire, or walks around it |
| `avoidWater` | Whether it stays out of water |
| `attackPlayerObjects` | Whether it attacks buildings |
| `enableHuntPlayer` | Whether it heads for players as soon as it appears, like raid creatures - takes effect on creatures that appear after the change |

### Taming

| Stat | Is |
| --- | --- |
| `tamingTime` | Seconds of feeding to tame it |
| `fedDuration` | Seconds it stays fed after eating |
| `commandable` | Whether a tamed one follows you when told to |

### Drops

| Stat | Is |
| --- | --- |
| `drops.<Item>` | The chance it drops an item, from 0 to 1, e.g. `drops.TrollHide = 0.5` - `0` removes the drop, and naming an item it doesn't drop adds it |
| `drops.<Item>.amountMin`, `drops.<Item>.amountMax` | How many it drops |
| `drops.<Item>.onePerPlayer` | Whether each nearby player gets one |
| `drops.<Item>.levelMultiplier` | Whether each star multiplies the amount |

### Creature Attacks

A creature's attacks are hidden weapons, with the same stats as [items](#item-stats).

| Stat | Is |
| --- | --- |
| `attacks.<stat>` | A stat of every weapon it attacks with, e.g. `attacks.damages = 1.5x` |
| `attacks.<attack>.<stat>` | A stat of one of its weapons, e.g. `attacks.troll_groundslam.damages = 2x` - `tweakstats dump` lists a creature's weapons |

Some weapons are shared, like a bite used by several kinds of wolf, so changing one changes it for every creature that uses it.

## Build Piece Stats

For `[Piece:...]` sections, which only apply in single player, when hosting, or on a server that has Tweak Stats - see [Multiplayer Servers](README.md#multiplayer-servers). Changes reach the pieces already built as soon as the config file is saved.

### Building

| Stat | Is |
| --- | --- |
| `health` | Health |
| `damages` | Resistances and weaknesses, e.g. `damages.fire = Immune` or `damages = Resistant` for every damage type - see [Damage Modifiers](#damage-modifiers) |
| `minToolTier` | The lowest tool tier that can damage it |
| `noRoofWear` | Whether it wears down in the rain without a roof - despite the name, `false` stops it |
| `noSupportWear` | Whether it breaks without support - despite the name, `false` lets it float |
| `burnable` | Whether fire can set it alight |
| `resources.<Item>` | How many of an item building it costs, e.g. `resources.Wood = 2` - `0` removes it |
| `resources.<Item>.recover` | Whether you get the item back when you remove it |
| `craftingStation` | The station it must be built near, or `none` to build it anywhere |
| `comfort` | Comfort it gives when resting |
| `canBeRemoved` | Whether it can be removed with the hammer |
| `allowedInDungeons` | Whether it can be built in dungeons |

### Chests, Fires and Stations

These belong to the pieces that have them.

| Stat | Is |
| --- | --- |
| `width`, `height` | A chest's size, in slots - making a chest smaller deletes the items in the slots it loses, and a chest only changes size when it's loaded again |
| `maxFuel` | Most fuel a fire, smelter or kiln holds |
| `secPerFuel` | Seconds a fire burns one piece of fuel |
| `infiniteFuel` | Whether a fire never runs out |
| `maxOre` | Most ore a smelter holds |
| `secPerProduct` | Seconds a smelter, kiln or refinery takes for each item |
| `fuelPerProduct` | Fuel a smelter uses for each item |
| `maxHoney` | Most honey a beehive holds |
| `secPerUnit` | Seconds a beehive takes for each honey |
| `fermentationDuration` | Seconds a fermenter takes |
| `rangeBuild` | How far from a crafting station you can build |
| `extraRangePerLevel` | Build range added by each station upgrade |
| `craftRequireRoof` | Whether a crafting station needs a roof |

## Status Effect Stats

For `[Effect:...]` sections. Effects that land on creatures, like `Burning`, `Poison` and `Frost`, only apply in single player, when hosting, or on a server that has Tweak Stats - see [Multiplayer Servers](README.md#multiplayer-servers). Effects from equipment being worn, like Megingjord's or a set bonus, change as soon as the config file is saved. Any other effect that's already running, like Rested, keeps its old values until it ends.

| Stat | Is |
| --- | --- |
| `ttl` | Seconds it lasts |
| `healthRegenMultiplier` | Health regeneration multiplier, e.g. Rested's |
| `staminaRegenMultiplier` | Stamina regeneration multiplier |
| `eitrRegenMultiplier` | Eitr regeneration multiplier |
| `healthOverTime` | Health given or taken over its duration |
| `healthPerTick` | Health given each tick, or taken when negative |
| `mods.<DamageType>` | Resistances and weaknesses it gives, e.g. `mods.Frost = Resistant` - see [Damage Modifiers](#damage-modifiers) |
| `percentigeDamageModifiers` | Extra damage of each type dealt by your attacks, as a fraction - spelled this way in the game |
| `modifyAttackSkill`, `damageModifier` | A skill whose attacks deal more damage, and the damage multiplier for them |
| `speedModifier` | Movement speed, as a fraction, e.g. `0.1` is +10% |
| `addMaxCarryWeight` | Carry weight added, e.g. Megingjord's |
| `addArmor` | Armor added |
| `runStaminaDrainModifier` | Stamina used running, as a fraction |
| `jumpStaminaUseModifier` | Stamina used jumping, as a fraction |
| `fallDamageModifier` | Fall damage, as a fraction |
| `raiseSkill`, `raiseSkillModifier` | A skill that levels faster, and how much faster |
| `skillLevel`, `skillLevelModifier` | A skill raised while the effect lasts, and by how much |
| `noiseModifier`, `stealthModifier` | How much noise you make and how hard you are to see |

## Damage Types

The parts of `damages` and `damagesPerLevel`, e.g. `damages.fire`.

| Damage Type | Is |
| --- | --- |
| `damage` | Damage that ignores resistances and weaknesses |
| `blunt` | Physical damage from clubs, maces and fists |
| `slash` | Physical damage from swords and axes |
| `pierce` | Physical damage from spears, knives and arrows |
| `chop` | Damage to trees and logs |
| `pickaxe` | Damage to rock and ore |
| `fire` | Fire damage, which sets things burning |
| `frost` | Frost damage, which slows what it hits |
| `lightning` | Lightning damage |
| `poison` | Poison damage, dealt over time |
| `spirit` | Spirit damage, which hurts undead |

## Damage Modifiers

The values of `damageModifiers` entries, e.g. `damageModifiers.Fire = Resistant`.

| Damage Modifier | Damage Taken |
| --- | --- |
| `Normal` | 100% |
| `SlightlyResistant` | 75% |
| `Resistant` | 50% |
| `VeryResistant` | 25% |
| `Immune` | None, shown as immune |
| `SlightlyWeak` | 125% |
| `Weak` | 150% |
| `VeryWeak` | 200% |
| `Ignore` | None, without showing immune |

## Skills

For `[Skill:...]` sections and the `skillType` stat.

| Skill | Used By |
| --- | --- |
| `Swords` | Swords |
| `Knives` | Knives and daggers |
| `Clubs` | Clubs, maces and sledges |
| `Polearms` | Atgeirs |
| `Spears` | Spears |
| `Blocking` | Shields |
| `Axes` | Axes and battleaxes |
| `Bows` | Bows |
| `Crossbows` | Crossbows |
| `ElementalMagic` | Staffs that cast fire, frost and lightning |
| `BloodMagic` | Staffs that summon and shield |
| `Unarmed` | Fists, claws and knuckles |
| `Pickaxes` | Pickaxes |
| `WoodCutting` | Chopping trees with an axe |
| `Fishing` | Fishing rods |

## Item Types

For `[Type:...]` sections.

| Item Type | Is |
| --- | --- |
| `OneHandedWeapon` | One-handed weapons |
| `TwoHandedWeapon` | Two-handed weapons, including pickaxes |
| `TwoHandedWeaponLeft` | Two-handed weapons held in the left hand, like some staffs |
| `Bow` | Bows, crossbows and other ranged weapons |
| `Ammo` | Arrows, bolts and other ammo that can be equipped |
| `AmmoNonEquipable` | Ballista missiles and other ammo that can't be equipped |
| `Shield` | Shields |
| `Helmet` | Helmets and hoods |
| `Chest` | Chest armor |
| `Legs` | Leg armor |
| `Shoulder` | Capes |
| `Utility` | Megingjord, Wishbone and other utility slot items |
| `Trinket` | Trinkets |
| `Tool` | Build tools like the hammer, hoe and cultivator |
| `Torch` | Torches |
| `Consumable` | Food and meads |
| `Material` | Crafting materials |
| `Fish` | Fish |
| `Trophy` | Trophies |
| `Misc` | Keys, saddles, eggs and everything else |
