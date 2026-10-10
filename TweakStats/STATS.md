# Tweak Stats - Stats

The stats Tweak Stats can change, and the values they take. See the [README](README.md#configuration) for how to write them in the config file, and [NAMES.md](NAMES.md) for the names of every item and recipe.

These are the most common stats - the [`tweakstats dump`](README.md#console-commands) console command lists every one an item or recipe has, with its value.

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
- [Damage Types](#damage-types)
- [Damage Modifiers](#damage-modifiers)
- [Skills](#skills)
- [Item Types](#item-types)

## Item Stats

### Weapon Damage

| Stat | Is |
| --- | --- |
| `damages` | The damage of each type, e.g. `damages.slash` - see [Damage Types](#damage-types) |
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
| `maxStackSize` | Most that fit in one inventory slot |
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
