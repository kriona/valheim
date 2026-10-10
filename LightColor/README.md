# Light Color

Look at an object that gives off light (sconce, campfire, portal) and press L to type a color for its light, the same way you type on a sign.

- Type a color name like `red`, `orange`, `yellow`, `lime`, `cyan`, `blue`, `purple` or `magenta`, or a hex color like `#0F0` or `#002B49`
- Works on item stands too - color the stand to recolor a mounted Dverger circlet, torch or anything else that glows, and the color stays with the stand when you swap the item
- Type `default` or blank to go revert to the original color
- The hover text shows the key on every object it works with
- Respects wards - you can't change lights inside a ward you don't have access to

<img src="images/white-portal.jpg" alt="A white portal" width="750">

## Multiplayer Servers

An object's color is stored on the object and saved with the world. The server does not need this mod installed to save the color.

Light colors are shared with everyone who uses this mod, and changes are seen by all players.

## Screenshots

### A purple Dverger circlet

<img src="images/purple-dverger-circlet.jpg" alt="A purple Dverger circlet" width="750">

### Blue campfires and sconce

<img src="images/blue-campfire-sconce.jpg" alt="Blue campfires and sconce" width="750">

## Color Brightness

The game treats black as "no light". If you set an object's color to black, it does not emit light.

The game respects brightness for most light sources. `#111` (dark gray) emits very little light, while `#FFF` (white) emits a lot of light.

### Portals

Portals are an exception. The markings on the portal are the same brightness, no matter what brightness level you use. The brightness level only impacts the effects of a portal you are standing near.

<img src="images/green-portals.jpg" alt="Green portals with different brightness" width="750">

## Default Colors

`default` or blank reverts an object to its original color. Fires that burn low when they run out of fuel switch to a smaller, dimmer light. Objects with more than one light have one of each color listed - the color you type is used for all of them.

| Object | Light color | Low on fuel |
| --- | --- | --- |
| Artisan Table | `#87DCFF` | |
| Battering Ram | `#FF7A00` | |
| Black Forge | `#FF7B99`, `#FF9E7B` | |
| Blast Furnace | `#FFA400` | |
| Blue Standing Brazier | `#7BC5FF` | `#BFFEFF` |
| Bonfire | `#FF8153` | `#FF8153` |
| Campfire | `#FF8153` | `#D68669` |
| Cauldron | `#FFAC53` | |
| Charcoal Kiln | `#FF7A00` | |
| Drakkar | `#F4C7AE` | |
| Dverger Circlet on an item stand | `#A0F8EE` | |
| Dverger Pole Lantern | `#FFC849` | |
| Dverger Wall Lantern | `#FFC849` | |
| Eitr Refinery | `#53E1FF`, `#FF53C6`, `#FF7B99` | |
| Eternal Pyre | `#FF7400` | |
| Fey Lights | `#8DB8FF` | |
| Forge | `#FF9E7B` | |
| Frigid Kiln | `#77D9FF` | |
| Frost Foundry | `#52E4FF` | |
| Galdr Table | `#FF31DC` | |
| Hanging Brazier | `#FF9E7B` | `#D68669` |
| Hearth | `#FF9E7B` | `#D68669` |
| Hooded Lantern | `#FFECC2` | |
| Hot Tub | `#FF8153` | |
| Iron Fire Pit | `#FF8153` | `#D68669` |
| Jack-o-turnip | `#FF9E7B` | |
| Lava Lantern | `#FF9E7B` | |
| Longship | `#F4C7AE` | |
| Mead Ketill | `#FFAC53` | |
| Portal | `#FF6400` | |
| Resin Candle | `#FFA539` | |
| Sap Extractor | `#ADFF5D` | |
| Sconce | `#FF9E7B` | |
| Shield Generator | `#FFAF8B` | |
| Smelter | `#FFA400`, `#FF8400` | |
| Snow Lantern | `#FFA539` | |
| Standing Blue-burning Iron Torch | `#7BC5FF` | |
| Standing Brazier | `#FF9E7B` | `#D68669` |
| Standing Green-burning Iron Torch | `#A1FF7B` | |
| Standing Iron Torch | `#FF9E7B` | |
| Standing Wood Torch | `#FF9E7B` | |
| Stone Oven | `#FF8153` | |
| Stone Portal | `#FF0E00` | |
| Unfading Candles | `#FF31DC` | |
| Ward | `#FCDFC1` | |
| Wisp Fountain | `#CB00FF` | |
| Wisp Torch | `#7BC8FF` | |
| Yule Tree | `#FFAD31` | |

Typing one of colors instead of `default` or blank these gets you the same light, but the flame comes out paler than the original.

## Configuration

`BepInEx\config\kriona.LightColor.cfg` is created on first run:

- `ColorKey` - the key to press while looking at a light, optionally with modifiers, e.g. `L` or `LeftControl + L` (default `L`)

## Installation

1. Install [BepInExPack for Valheim](https://www.nexusmods.com/valheim/mods/3605)
2. Extract the zip into your Valheim folder, or copy `LightColor.dll` into `BepInEx\plugins`

Client-side only - the server doesn't need it. Other players only need it to see the colors.
