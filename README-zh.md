# Thunderbolt / 雷劫
[English](https://github.com/Aebestach/Thunderbolt/blob/master/README.md) | [中文](https://github.com/Aebestach/Thunderbolt/blob/master/README-zh.md)

## 模组简介 (Introduction)
在 blackrack 的 **[True Volumetric Clouds (体积云)](https://www.patreon.com/c/blackrack/posts)** 中，雷暴闪电原本主要用于视觉表现。
**雷劫**在此基础上增加了互动：飞船处于可放电云层下方时，闪电有概率击中船体，并可选地损毁部件。

若只想保留特效，可在难度设置中关闭伤害。此外提供通用避雷模块，供部件或 Kerbal Konstructs 静态避雷塔使用——附近的雷击会以较高概率改劈至避雷针，但并非绝对免疫。

## 必要前置 (Dependencies)
* **[True Volumetric Clouds](https://www.patreon.com/c/blackrack/posts)** —— 带雷电的版本理论上均可；目前仅在 **v5** 中测试。

## 安装指南 (Installation)
将压缩包内 `GameData` 下的 `Thunderbolt` 文件夹放入游戏根目录的 `GameData` 中即可。

## 哪些载具会被雷劈 (Eligible Vessels)
需同时满足：

* 处于**飞行中 / 发射前 / 着陆 / 溅落**等已加载状态（未打包卸载）
* 位于有大气的天体上，且高度不超过大气层顶
* 上方存在可放电的体积云层（覆盖与闪电频率达到难度设定阈值）
* **不是**舱外活动（EVA）的宇航员

## 给开发者 (For Modders)

### 部件 (Part)

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

### Kerbal Konstructs 静态物 (Static)

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

| 字段 | 含义 |
| --- | --- |
| `attractRadius` | 球形引雷半径（米）。建议别过大：未加载的飞船本来也不会被劈，发射台防护通常几百米足够 |
| `divertChance` | 将附近雷击引至该针的概率（0–1） |
| `attractPriority` | 多根针竞争时的优先级 |
| `tipTransform` / `tipOffset` | 可选雷击落点 |
| `canBeDestroyed` / `destroyChance` | 仅部件避雷针 |
