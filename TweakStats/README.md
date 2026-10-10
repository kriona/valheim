# Tweak Stats

Change the stats of any item or recipe - weapon damage, attack stamina, armor, block power, food, durability, weight, stack sizes, crafting costs and more.

- Set a stat to a number, add to it or multiply it - `weight = 5`, `weight = +5` or `weight = 1.5x`
- Change one item, several items, every item matching a name like `Sword*` or `*Bow*`, or every item of a skill (e.g., `[Skill:Axes]`) or type (e.g., `[Type:Shield]`)
- Limit tweaks to certain worlds, e.g. single player (`[Worlds: SinglePlayer][/Worlds]`) or a specific multiplayer server (e.g., `[Worlds: Midgard][/Worlds]`)
- Changes take effect as soon as the config file is saved - there's no need to restart Valheim or rejoin the world
- Works with items and recipes added by other mods

## Items and Stats

Three places help you work out what to write in the config file:

- [NAMES.md](NAMES.md) - the name of every item, recipe, creature, build piece and status effect, next to its in-game name
- [STATS.md](STATS.md) - every stat you can change, from weapon damage to armor to food to crafting costs, plus the damage types, damage modifiers, skills and item types they use
- The [`tweakstats` console command](#console-commands) - looks up names in game or lists the items you're carrying and writes out every stat an item has

## Examples

```ini
# Stone axes get knockback
[AxeStone]
attackForce = 10
```

```ini
# Every item that uses the Axes skill becomes an insta-kill omni-tool
[Skill:Axes]
damages.slash = 10000
damages.chop = 10000
damages.pickaxe = 10000
```

```ini
# Stone axes are free to craft on my single-player world
[Worlds: SinglePlayer]
[Recipe:AxeStone]
resources.Wood = 0
resources.Stone = 0
[/Worlds]
```

```ini
# ZOOOOM (fall damage will probably kill you, but enemies can't)
[ArmorRagsChest]
armor = 1000
movementModifier = 3  # +300% speed
```

## Configuration

`BepInEx\config\kriona.TweakStats.cfg` is created on first run with an explanation and examples, all commented out. Remove the `#` from the start of a line to use it.

Each `[section]` names what to change, and each line under it is `stat = value`. Lines starting with `#` are comments and are ignored.

### Sections

| Section | Changes |
| --- | --- |
| `[SwordIron]` | An item, by its prefab name |
| `[SwordIron, AxeIron]` | Several items |
| `[Sword*]` | Items whose prefab name matches - `*` matches any text and `?` any one letter |
| `[Skill:Swords]` | Every item that uses a skill - see [Skills](STATS.md#skills) |
| `[Type:Shield]` | Every item of a type - see [Item Types](STATS.md#item-types) |
| `[Recipe:SwordIron]` | The recipe that makes an item - wildcards work here too |

Names ignore case. Items are named by their prefab name, like `SwordIron`, rather than the name shown in game, like "Iron sword". [NAMES.md](NAMES.md) lists every item, recipe, creature, build piece and status effect with its name in game, and the [`tweakstats find`](#console-commands) command looks them up in game.

### Values

| Value | Does |
| --- | --- |
| `5` | Sets the stat to 5 |
| `+5` | Adds 5 |
| `-5` | Subtracts 5 |
| `1.5x` | Multiplies by 1.5 |
| `=-5` | Sets the stat to -5 - a negative number needs `=` in front, or it subtracts |

Stats that are whole numbers, like `maxStackSize`, are rounded after adding or multiplying. Stats that aren't numbers are set:

- `true` or `false`, e.g. `teleportable = true`
- One of a list of choices, e.g. `skillType = Axes` or `damageModifiers.Fire = Resistant`
- The name of an item, status effect or crafting station, e.g. `attackStatusEffect = Burning` or `craftingStation = forge`, or `none` to remove it

### Stat Names

A stat's name is the game's own name for it without the `m_`, so `m_attackStamina` is `attackStamina`. Names ignore case.

Dots go into a group of stats:

- `attack.attackStamina` - the attack's stamina cost
- `damages.slash` - the slash part of the damage

A group of damage types can be changed all at once - `damages = 1.5x` multiplies slash, fire, chop and every other damage type.

Lists are named by their entries:

- `resources.Iron = 10` - the amount of iron a recipe needs, adding iron when the recipe doesn't use it
- `resources.Iron = 0` - removes iron from the recipe
- `resources.Iron.amountPerLevel = 5` - another stat of the entry
- `damageModifiers.Frost = Resistant` - an armor's frost resistance, adding it when the armor doesn't have one

A misspelled name is reported in the log with suggestions, e.g. `there's no stat "damage" - did you mean damages?`

### Order

Every section that names an item applies to it, from the top of the file down, so a broad section followed by a specific one combines them:

```ini
[Skill:Swords]
damages = 1.2x

[SwordIron]
damages.slash = +10   # The Iron sword gets 1.2x, then +10 slash
```

### Worlds

Sections between `[Worlds: ...]` and `[/Worlds]` only apply on those worlds:

```ini
[Worlds: SinglePlayer, Midgard]
[Skill:Pickaxes]
damages.pickaxe = 2x
[/Worlds]
```

- A world is named by its name, or by its ID when several share a name - a dedicated server's world is often just `Dedicated`
- A server can be named by the address you join it with, with or without the port, e.g. `203.0.113.5`, `203.0.113.5:2456` or `valheim.example.com`. A server joined from the server list is named by its Steam ID, or its PlayFab ID for crossplay servers
- `SinglePlayer` is any world started without letting other players join
- The [`tweakstats world`](#console-commands) command shows the world's name and ID and the server's address or ID for wherever you're playing
- Sections outside a group apply everywhere
- Groups can't be inside each other

### Issues

Lines that can't be used are skipped and logged to `BepInEx\LogOutput.log` with their line number and what's wrong, and the rest of the file still applies. When the file is saved while playing, a message in the top left says how many problems it has.

## Console Commands

Press F5 to open the console.

| Command | Does |
| --- | --- |
| `tweakstats find iron sword` | Lists the items whose name or prefab name contains the text, e.g. `SwordIron - Iron sword` |
| `tweakstats inventory` | Lists the items you're carrying with their section names and main stats - damage, armor, block power, food, durability and weight - including your tweaks and each item's upgrade level, e.g. `[SwordIron] Iron sword - level 2, equipped, damages slash 55, durability 180/250, weight 1.5` |
| `tweakstats world` | Shows the world's name and ID, whether you're playing alone, hosting or on a server - with the server's address, Steam ID or PlayFab ID - and the `[Worlds: ...]` groups that apply to it |
| `tweakstats dump SwordIron` | Writes every stat of a section with its value to `BepInEx\config\kriona.TweakStats.dump.txt`, ready to copy into the config file. Any section works, e.g. `tweakstats dump Skill:Swords` or `tweakstats dump Recipe:SwordIron` |
| `tweakstats list` | Writes every item and recipe name to `BepInEx\config\kriona.TweakStats.names.md`. **Only needed** if you are using a mod that adds new items / recipes |
| `tweakstats reload` | Reads the config file again (this **should** be automatic) |

`dump` writes the game's values before any tweaks, so multiplying is easy to work out.

## Multiplayer Servers

Tweaks apply to you only. Other players without the mod see the game's own values, and the server doesn't need the mod.

Your weapon's damage is worked out by your game, so a stronger weapon also hits harder in PvP.

## Installation

1. Install [BepInExPack for Valheim](https://www.nexusmods.com/valheim/mods/3605)
2. Extract the zip into your Valheim folder, or copy `TweakStats.dll` into `BepInEx\plugins`
