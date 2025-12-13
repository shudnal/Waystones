# Waystones
Build a waystone network, sit in front of one, locate with your gaze a distant place or another waystone and fast travel there

![](https://staticdelivery.nexusmods.com/mods/3667/images/headers/2832_1722236465.jpg)

This mod allows you to build a waystone, new piece which you can use to fast travel to some fixed and dynamic locations and also other activate and tagged waystones.

This mod is meant mainly to use on nomap mode and/or noportals mode. In nomap mode it could be also used for orienting and in noportals mode as somewhat alternative way of fast travel. A waystone could be used as a landmark visible from afar and as an alternative to a signs.

On your first approach a waystone the Hugin will hint you how to use it. If you disabled raven hints there will be no Hugin for you.

![](https://staticdelivery.nexusmods.com/mods/3667/images/2832/2832-1722236953-1648019909.png)

## Direction search

After building a waystone sit in front of it and activate search mode. 

Your screen will be darken, camera movement slowed and you will be able to hear and locate available distant places. 

Sound pitch and screen brightness will reflect a distance between current look direction and distant place direction. 

"Touch" the direction with your gaze and you can start fast travelling there.

You can use Zoom buttons (mouse scroll) to change camera FoV and actually zoom in and zoom out the view to increase/decrese look sensitivity.

![](https://staticdelivery.nexusmods.com/mods/3667/images/2832/2832-1722237104-706801051.png)

In singleplayer time flow will be slowed as well.

## Cooldown

Fast travelling ability takes some time to recover. The length of that recovery depends on a travelling distance. Max and min distance and cooldown are configurable. In location hover in search mode you will see what cooldown will be set after fast travelling.

There is console command `setwaystonecooldown [seconds]` to manually set cooldown for test purposes.

Cooldown is based on a world time by default. It could be changed to use global real time instead.

### Cooldown reduction

You can sacrifice items to waystone to reduce cooldown.

Create file shudnal.Waystones.reduce_cooldowns.json or shudnal.Waystones.reduce_cooldowns.yaml anywhere inside of the Bepinex config folder. 

This file should consist of pairs `itemname: cooldown`. Item name could be set as prefab name (i.e. TrophyNeck) or item name (i.e. $item_trophy_neck)

JSON example:
```json
{
	"TrophyNeck": 10
}
```

If you want several items to be sacrificed at once you can do it like this where 50 is amount of coins and 10 is amount of seconds.

JSON
```json
{
"Coins:50": 10
}
```

YAML entry can be set like this
﻿`'Coins:50': 10`

## Available directions

### Fixed locations
* start temple (Sacrificial stones)
* Haldor (if discovered)
* Hildir (if discovered)

### Dynamic points
* current spawn point (bed or other spawn point)
* last death position (even after tombstone pickup)
* last ship position (saved when character stands on the ground for the first time after leaving a ship)
* last location from where you used fast travel last time

### Waystone network
After activating search mode all waystones available for fast travelling will be temporary added to your map. You can open Map in search mode without breaking it. 

A waystone should be tagged and activated by you for it to be added to map.

Removed, deactivated or waysone without tag will not be available for fast travelling.

### Random point
Absolutely random (but not ocean) point in already discovered biome.

To fast travel to random point look at your feet. Camera should be looking straight down. 

Fast travelling to random point will set a shortest cooldown.

## Fast travelling
After choosing a direction and pressing Use hotkey fast travelling will be started. 

The moment you initiate fast travelling nearby monsters will be agitated by the sound and light emitted by you. Any damage taken while channeling will add 1 second to teleporting process.

To break channeling manually you can press Block button.

## Restrictions
There are several restrictions on fast travel and search mode usage.
* you should not be encumbered
* you should not have nonteleportable items
* you should not be wet
* you should not be sensed by enemies
* you should be sitting

All this restrictions are disableable via config.

You can also enable entering search mode just by pressing hotkey only without need of building a waystone.

Tag character limit is 15 by default. If you plan to use waystones as signs you can increase that limit.

Default waystone recipe is "SurtlingCore:1,GreydwarfEye:5,Stone:5" and is also configurable on the fly using configuration manager.

## Visual effects

Almost everything related to direction search mode and particles visual effect of fast travelling are configurable in case you struggle with performance (which should not be a case but whatever).

Notable configurable values:
* mouse sensitivity factor in search mode
* screen brightness and sound volume
* fov delta (how much you can zoom in search mode)
* screen and sound sensitivity threshold (an angle between location direction and look direction for effect to start showing)

## Localization
To add your own localization create a file with the name Waystones.LanguageName.yml or Waystones.LanguageName.json anywhere inside of the Bepinex folder. For example, to add a French translation you could create a Waystones.French.yml file inside of the config folder and add French translations there.

Localization file will be loaded on the next game launch or on the next language change.

You can send me a file with your localization at [GitHub](https://github.com/shudnal/Waystones/issues) or [Nexus](https://www.nexusmods.com/valheim/mods/2832?tab=posts) so I can add it to mod's bundle.

[Language list](https://valheim-modding.github.io/Jotunn/data/localization/language-list.html).

English localization example is located in `Waystones.English.json` file next to plugin dll.

## Installation (manual)
copy Waystones.dll to your BepInEx\Plugins\ folder.

## Incompatibility
Mod should be compatible with anything.

## Configurating
The best way to handle configs is [Configuration Manager](https://thunderstore.io/c/valheim/p/shudnal/ConfigurationManager/).

Or [Official BepInEx Configuration Manager](https://thunderstore.io/c/valheim/p/Azumatt/Official_BepInEx_ConfigurationManager/).

## Mirrors
[Nexus](https://www.nexusmods.com/valheim/mods/2832)
[Thunderstore](https://valheim.thunderstore.io/package/shudnal/Waystones/)

## Donation
[Buy Me a Coffee](https://buymeacoffee.com/shudnal)

## Discord
[Join server](https://discord.gg/e3UtQB8GFK)