# Quick Fill

Hold Shift and press the use key (E) to fill a station in one go instead of adding items one at a time. Mouse targets on stations are also larger.

- Smelters, kilns, blast furnaces, refineries, etc. - fills ore and fuel
- Ovens - fills fuel
- Campfires, bonfires, hearths, torches, etc. - fills fuel without turning the fire off
- Shield generators - fills bones
- The hover text shows the shortcut on everything it works with

You don't have to aim at the small ore and fuel openings either. Each half of a smelter, kiln, blast furnace, refinery, etc. targets the opening on that side, an oven's top half adds food while its bottom half adds wood, and anywhere on a shield generator.

<img src="images/QuickFill.jpg" alt="Offering to quick fill a charcoal kiln" width="750">

## Configuration

`BepInEx\config\kriona.QuickFill.cfg` is created on first run, and changes take effect as soon as the file is saved. You do not need to restart Valheim or rejoin the world.

- `ModifierKey` - the key to hold while pressing the use key, e.g. `LeftControl` or `LeftAlt` - either side of Ctrl, Shift and Alt counts, and `None` turns the fill off (default `LeftShift`)

## Installation

1. Install [BepInExPack for Valheim](https://www.nexusmods.com/valheim/mods/3605)
2. Extract the zip into your Valheim folder, or copy `QuickFill.dll` into `BepInEx\plugins`

Client-side only - other players and the server don't need it.
