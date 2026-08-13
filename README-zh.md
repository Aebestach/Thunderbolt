# Thunderbolt / 雷劫
[English](https://github.com/Aebestach/Thunderbolt/blob/master/README.md) | [中文](https://github.com/Aebestach/Thunderbolt/blob/master/README-zh.md)

![Banner](https://i.imgur.com/1WtXutR.png)

## 模组简介 (Introduction)
在 blackrack 的 **[True Volumetric Clouds (体积云)](https://www.patreon.com/c/blackrack/posts)** 中，雷暴闪电原本主要用于视觉表现。
**雷劫**在此基础上增加了互动：飞船处于可放电云层下方时，闪电有概率击中船体，并可选地损毁部件。未安装 EVE / 体积云时也可使用大气模式——以 Kerbin 海平面大气密度为基准，密度更高的星球雷击概率增大，更低则减小；无大气天体不会雷击。

若只想保留特效，可在难度设置中关闭伤害。此外提供通用避雷模块，供部件或 Kerbal Konstructs 静态避雷塔使用——附近的雷击会以较高概率改劈至避雷针，但并非绝对免疫。

## 画廊 (Gallery)

![雷击效果](https://i.imgur.com/SsqlnJp.png)
![雷击效果](https://i.imgur.com/53HHh6w.png)
![雷击效果](https://i.imgur.com/i5jW5xs.png)
![雷击效果](https://i.imgur.com/cSssYX7.png)
![雷击效果](https://i.imgur.com/HoP45vf.png)
![雷击效果](https://i.imgur.com/XOwbfYR.png)

## 建议模组 (Recommended)
雷劫可独立运行。以下模组可增强体验：

* **[True Volumetric Clouds](https://www.patreon.com/c/blackrack/posts)** —— 通过 `ThunderboltEVE.dll` 与云层同步雷击；目前在 **v5** 测试。
* **[BlastFX](https://github.com/Aebestach/BlastFX)** —— 雷击摧毁部件时的不透明火球特效。未安装时雷劫仍可运行，但没有火球罩住再删除的效果。

## 安装指南 (Installation)
将压缩包内 `GameData` 下的文件夹放入游戏根目录的 `GameData` 中即可。**BlastFX** 已捆绑在压缩包中，提供雷击摧毁部件时的火球特效；若不需要可单独删除 `GameData/BlastFX`。请特别注意：该摧毁特效目前可能并不完美。

## 哪些载具会被雷劈 (Eligible Vessels)
需同时满足：

* 处于**飞行中 / 发射前 / 着陆 / 溅落 / EVA**等已加载状态（未打包卸载）
* 位于有大气的天体上，且高度不超过大气层顶
* **EVE 模式：** 上方存在可放电体积云层（覆盖与闪电频率达到难度阈值）
* **大气模式：** 处于较低大气带；风暴格点带时空随机，概率按相对 Kerbin 的海平面密度缩放

舱外宇航员也可被击中；致死概率单独较低（可在难度设置中调整）。

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

## 致谢 (Credits)

感谢 @CharonSSS 提供的封面与效果图。
