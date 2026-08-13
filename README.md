# Thunderbolt
[English](https://github.com/Aebestach/Thunderbolt/blob/master/README.md) | [中文](https://github.com/Aebestach/Thunderbolt/blob/master/README-zh.md)

![Banner](https://i.imgur.com/1WtXutR.png)

## Introduction
In blackrack's **[True Volumetric Clouds](https://www.patreon.com/c/blackrack/posts)**, storm lightning is primarily a visual effect.
**Thunderbolt** adds interaction on top of that: when a vessel is under a lightning-capable cloud layer, bolts may strike the craft, with optional part destruction. Without EVE / volumetric clouds, an atmospheric mode is still available — it uses each body's sea-level density relative to Kerbin (denser → higher chance, thinner → lower); airless bodies never strike.

Damage can be disabled in the difficulty settings if you only want the visuals. A shared lightning-rod module is also provided for parts and Kerbal Konstructs static towers — nearby strikes are diverted to the rod with high probability, but not with absolute immunity.

## Gallery

![Strike](https://i.imgur.com/SsqlnJp.png)
![Strike](https://i.imgur.com/53HHh6w.png)
![Strike](https://i.imgur.com/i5jW5xs.png)
![Strike](https://i.imgur.com/cSssYX7.png)
![Strike](https://i.imgur.com/HoP45vf.png)
![Strike](https://i.imgur.com/XOwbfYR.png)

## Recommended
Thunderbolt runs on its own. These mods improve the experience:

* **[True Volumetric Clouds](https://www.patreon.com/c/blackrack/posts)** —— Cloud-synced strikes via `ThunderboltEVE.dll`; tested on **v5**.
* **[BlastFX](https://github.com/Aebestach/BlastFX)** —— Opaque fireball when a part is destroyed by lightning. Thunderbolt still loads without it, but cover-destroy FX is skipped.

## Installation
Drop the folders from the archive's `GameData` into your game's root `GameData` directory. **BlastFX** is bundled in the archive for the fireball when lightning destroys a part; you can delete `GameData/BlastFX` if you don't need it. Please note that this destroy effect is not perfect yet.

## Eligible Vessels
A vessel may be struck when all of the following are true:

* It is **loaded** (not packed/unloaded) in **Flying / Pre-Launch / Landed / Splashed / EVA**
* It is on a body with an atmosphere, and below the atmosphere top
* **EVE mode:** a lightning-capable volume-cloud layer is above it (coverage / lightning frequency thresholds)
* **Atmospheric mode:** vessel is in a lower-atmosphere band; storm cells are random in space/time, scaled by ASL density vs Kerbin

EVA kerbals can also be struck; their kill chance is kept separately low (adjustable in difficulty settings).

## For Modders

### Part

```cfg
MODULE
{
    name = ModuleThunderboltRod
    attractRadius = 400
    divertChance = 0.92
    attractPriority = 2
    // tipTransform =
    // tipOffset = 0, 0, 0
    // canBeDestroyed = false
    // destroyChance = 0.05
}
```

### Kerbal Konstructs Static

```cfg
MODULE
{
    namespace = Thunderbolt
    name = ModuleThunderboltRodStatic
    attractRadius = 800
    divertChance = 0.95
    attractPriority = 3
    // tipTransform =
    // tipOffset = 0, 20, 0
    // displayName = Lightning Tower
}
```

| Field | Meaning |
| --- | --- |
| `attractRadius` | Spherical divert range (m). Keep it modest: unloaded vessels are never struck anyway, and a few hundred meters usually covers a pad |
| `divertChance` | Chance to divert a nearby strike (0–1) |
| `attractPriority` | Priority when multiple rods compete |
| `tipTransform` / `tipOffset` | Optional bolt tip |
| `canBeDestroyed` / `destroyChance` | Part rods only |

## Credits

Thanks to @CharonSSS for the banner and screenshot images.
