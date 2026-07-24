# 熵引擎 / Entropy Engine — TM2 团战节奏改造 v2.5.0

Teamfight Manager 2 原生 Rust Mod。选手属性驱动回城 AI —— 每个人都是独特的。

[创意工坊](https://steamcommunity.com/sharedfiles/filedetails/?id=3763852408) · [更新日志](CHANGELOG.txt)

---

## 功能

### 属性驱动 AI
读取选手 `aggressive` / `judgement` / `mental` 三项属性，直接影响局内回城决策：
- **aggressive 高** → 更贪，回城更晚、残血赖线
- **judgement 高** → 时机更精准、danger 感知降低
- **mental 高** → 残血不慌、生死局镇定

训练选手属性直接改变 AI 表现，每个选手独一无二。

### 位置策略
5 个位置各 3 个偏移参数，config.toml 中可调：
- **Top** — 最贪（回城 = 亏兵 + 镀层）
- **Jungle** — 保血量控资源
- **Mid** — 短线快节奏
- **Bottom** — 最保守（活着才有输出）
- **Support** — 守护者（可残血保 ADC）

### 反扑机制
滚动窗口追踪双方危险度。劣势方回城阈值降低 + 赖线概率提高，翻盘后自动恢复。

### 绝对血量安全网
HP < 150 无条件逃命，可配置。

---

## 配置编辑器

`editor-src/` 目录下是图形化配置编辑器的 C# 源码。

**构建：**
```
cd editor-src
dotnet publish -c Release -r win-x64 -o publish
```
需要 .NET 10 SDK 和 Windows Forms。

**编辑器功能：**
- 26 个滑块（混沌回城 4 + 反扑 6 + 安全网 1 + 位置偏移 15）
- 中英文切换
- 浏览加载任意 .toml 文件
- 5 个预设方案（默认 / 激进 / 保守 / 关闭反扑 / 还原原版）
- 保存到 config.user.toml（mod 更新不覆盖）

---

## 参数参考

### 混沌回城
| 参数 | 默认 | 说明 |
|---|---|---|
| `recall_threshold_min` | 22 | 回城血量下限 (%) |
| `recall_threshold_max` | 33 | 回城血量上限 (%) |
| `overstay_min` | 10 | 赖线概率下限 (%) |
| `overstay_max` | 18 | 赖线概率上限 (%) |

### 败方反扑
| 参数 | 默认 | 说明 |
|---|---|---|
| `danger_hp` | 15 | 残血判定线 (%) |
| `danger_floor` | 3 | 最低危险阈值 |
| `danger_ring_window` | 64 | 滚动窗口大小 (tick) |
| `threshold_reduce` | 8 | 劣势方回城阈值降低 (%) |
| `overstay_increase` | 15 | 劣势方赖线加成 (%) |
| `safe_threshold_increase` | 5 | 优势方回城阈值提高 (%) |

### 安全网
| 参数 | 默认 | 说明 |
|---|---|---|
| `absolute_hp_panic` | 150 | 无条件逃命血量 |

### 位置偏移
| 位置 | recall | overstay | danger |
|---|---|---|---|
| Top | -8 | 15 | -6 |
| Jungle | 8 | -8 | 5 |
| Mid | -2 | 3 | 0 |
| Bottom | 15 | -10 | 10 |
| Support | -3 | 10 | -3 |

配置优先级：`config.user.toml` > `config.toml` > 内置默认值

---

## 相关链接

- 创意工坊：https://steamcommunity.com/sharedfiles/filedetails/?id=3763852408
- 构建依赖：SDK `nightly-2026-05-24` + `mod-sdk` 0.5.2
