# Weight Display

Shows your current and maximum carry weight, followed by how many inventory slots are in use, on the HUD just below the minimap.

- Weight turns yellow at 80% of your maximum and red when you're overburdened
- Slot count turns yellow when every slot is in use. Optional: it can warn you when you're close to full
- All of the colors and warning thresholds can be changed, and either the weight or the slot count can be turned off
- Moves to the top-right corner when the minimap is hidden
- Hides along with the rest of the HUD

<img src="images/WeightDisplay.jpg" alt="Your carrying capacity being shown under the minimap">

## Configuration

`BepInEx\config\kriona.WeightDisplay.cfg` is created on first run, and changes take effect as soon as the file is saved. You do not need to restart Valheim or rejoin the world.

| Setting | Default | Description |
| --- | --- | --- |
| ShowWeight | true | Show your current and maximum carry weight |
| ShowSlots | true | Show how many inventory slots are in use |
| FontSize | 18 | Size of the weight text |
| Margin | 6 | Gap between the minimap (or the screen corner) and the text |
| WeightWarningPercent | 80 | Percentage of your maximum carry weight at which the weight takes WeightWarningColor (1-100) |
| WeightWarningColor | yellow | Color of the weight at or above WeightWarningPercent |
| OverburdenedColor | red | Color of the weight when you're overburdened |
| SlotWarningOpenSlots | 3 | Number of open inventory slots at or below which the slot count takes SlotWarningColor (1-100) |
| SlotWarningColor | (empty) | Color of the slot count when SlotWarningOpenSlots or fewer slots are open |
| FullSlotsColor | yellow | Color of the slot count when every slot is in use |

Colors can be a name (black, blue, green, orange, purple, red, white, yellow) or a `#RRGGBB` value. Leave one empty to keep the number uncolored at that point.

## Installation

1. Install [BepInExPack for Valheim](https://www.nexusmods.com/valheim/mods/3605)
2. Extract the zip into your Valheim folder, or copy `WeightDisplay.dll` into `BepInEx\plugins`

Client-side only - other players and the server don't need it.
