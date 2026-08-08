# Thunderbolt
[English](https://github.com/Aebestach/Thunderbolt/blob/master/README.md) | [中文](https://github.com/Aebestach/Thunderbolt/blob/master/README-zh.md)

## Introduction
In blackrack's **[True Volumetric Clouds](https://www.patreon.com/c/blackrack/posts)**, storm lightning is primarily a visual effect.
**Thunderbolt** adds interaction on top of that: when a vessel is under a lightning-capable cloud layer, bolts may strike the craft, with optional part destruction.

Damage can be disabled in the difficulty settings if you only want the visuals. A shared lightning-rod module is also provided for parts and Kerbal Konstructs static towers — nearby strikes are diverted to the rod with high probability, but not with absolute immunity.

---

## Dependencies
* **[True Volumetric Clouds](https://www.patreon.com/c/blackrack/posts)** —— Builds with lightning should work; tested on **v5** only.

---

## Installation
Drop the `Thunderbolt` folder from the archive's `GameData` into your game's root `GameData` directory.

---

## Eligible Vessels
A vessel may be struck when all of the following are true:

* It is **loaded** (not packed/unloaded) in **Flying / Pre-Launch / Landed / Splashed**
* It is on a body with an atmosphere, and below the atmosphere top
* A lightning-capable volume-cloud layer is above it (coverage and lightning frequency meet the difficulty thresholds)
* It is **not** an EVA kerbal

---

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
