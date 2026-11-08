# Changelog

## 1.8.2
- Mark the Valkyrie who carries players into a new world as a passive creature with a gray (#888) marker like Oden's - she's marked when All Passive Creatures is on or her own line is true, including while she's carrying you
- Place the markers after the camera moves each frame, so a marker on a creature that moves in steps, like the Valkyrie, holds still instead of flickering between two spots
- Rename Odin to Oden, per the game's lore

## 1.8.1
- Mark Odin, the hooded figure who watches you from a distance, and his ravens Hugin and Munin while they're landed, as passive creatures with a gray (#888) marker - they're marked when All Passive Creatures is on or their own line is true
- Only pin an off-screen arrow to the top half of the screen when the creature is higher up than you, so a creature behind you on level ground gets its arrow in the bottom half

## 1.8.0
- Support leviathans and lava leviathans, with the All Leviathans switch, on by default, and the Leviathan Color setting, default #B366FF (purple)
- Support other players who are sharing their position on the map and aren't sneaking, with the All Players switch, on by default, and the Player Color setting, default #FFF (white)
- Move a trader's arrow above their speech bubble while they are talking, so it doesn't cover what they say

## 1.7.0
- Added the Angry Color setting, default #FF801A (orange), for hostile creatures that are targeting you
- In multiplayer, each creature's target is shared with the other players who have the mod, so Angry Color only goes to creatures that are after you - see Multiplayer in the README for creatures run by players without the mod
- Bob Height defaults to 0, so arrows don't bob unless you turn it on

## 1.6.0
- Replaced the All Creatures switch with All Passive Creatures, which marks every creature that won't attack you, including traders and their pets - a config file with the old All Creatures = true turns on All Passive Creatures, All Hostile Creatures and All Birds, so the same creatures stay marked
- Added the All Birds switch, off by default, to mark every bird
- Added the Min Creature Level setting, the number of stars a creature needs to get a marker, default 0
- Added the Min Fish Level setting, default 1 (suggested by DarkFreezer)
- Hide the on-screen markers while the loading screen is shown, like when going through a portal

## 1.5.1
- Draw markers above the minimap, so an arrow on the top right of the screen isn't hidden behind it
- Hide the on-screen markers while the large map is open

## 1.5.0
- Support birds

## 1.4.0
- Hide the mod's name and level when the game is already showing this info
- Make sure the marker arrow doesn't overlap the game's info
- Traders like Haldor, Hildir and the Bog Witch, and their pets like Halstein, can be marked too, each with its own line in the config file, and are marked by All Creatures
- Added the Trader Color setting for traders, blank by default to use Passive Color - their pets use Passive Color
- Added the Pet Color setting for creatures you have tamed, default #FFF

## 1.3.1
- Support BepInEx Configuration Manager (reported by DB451)

## 1.3.0
- Fish can be marked too, each with its own line in the config file and a new Fish Color setting (suggested by DarkFreezer)
- Added the Show Level setting, off by default, which shows a ⭐ per star after a creature's name, and a fish's level after its name like Perch[3] (suggested by DarkFreezer)
- Added the Far Distance and Far Scale settings - markers of creatures further away than 50 meters are drawn at two thirds size
- Added the Bob Height setting for how far on-screen arrows bob up and down, 0 to disable bobbing

## 1.2.1
- Shows a message when the config file is reloaded

## 1.2.0
- Config changes take effect as soon as the file is saved; you don't need to restart Valheim or rejoin the world

## 1.1.0
- Marked creatures within Max Distance get a dot on the minimap and the large map in their marker color
- Added the Show On Screen and Show On Map settings, both on by default, to turn the arrows and the map dots on or off

## 1.0.0
- Initial release
