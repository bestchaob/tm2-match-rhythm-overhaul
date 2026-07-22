# 熵引擎配置编辑器 / Entropy Engine Config Editor

---

## 快速开始

1. 在 Mod 目录中双击 `熵引擎配置编辑器.exe`
2. 首次运行如提示缺少 .NET 10 Runtime，点击弹窗中的下载链接安装即可
3. 拖动滑块调整参数 → 保存
4. 重启游戏生效

## 技术栈

- WinForms (.NET)
- 目标运行时：.NET 10 Runtime
- 配置格式：TOML

## 功能

- **滑块调参**：所有 AI 行为参数通过滑块直观调整，无需手写 TOML
- **中英文切换**：界面支持 ZH/EN 双语切换（大写简写按钮，32×20 固定尺寸，活跃语言粗体标识）
- **自动检测路径**：默认自动加载 Workshop 目录中的配置文件
- **手动浏览**：通过 📁 按钮手动选择配置目录
- **预设方案**：
  - 激进入侵：回城阈值 8~18%、赖线概率 20~38%、劣势回城降低 15%、劣势赖线加成 30%
  - 保守运营：回城阈值 32~46%、赖线概率 1~5%、劣势回城/赖线加成归零、优势回城提高 10%
- **配置优先级**：config.user.toml > config.toml > 内置默认值（用户自建配置不会被 Mod 更新覆盖）

## 参数说明

### 混沌回城 (chaos_recall)

| 参数 | 默认值 | 说明 |
|------|--------|------|
| `recall_threshold_min` | 22 | 回城血量下限 (%) |
| `recall_threshold_max` | 33 | 回城血量上限 (%) |
| `overstay_min` | 10 | 赖线概率下限 (%) |
| `overstay_max` | 18 | 赖线概率上限 (%) |

### 败方反扑 (comeback)

| 参数 | 默认值 | 说明 |
|------|--------|------|
| `danger_hp` | 15 | 残血判定线 (%) |
| `threshold_reduce` | 8 | 劣势方回城阈值降低 (%) |
| `overstay_increase` | 15 | 劣势方赖线额外加成 (%) |
| `safe_threshold_increase` | 5 | 优势方回城阈值提高 (%) |

## 常见配置

- 关闭反扑：`threshold_reduce=0, overstay_increase=0, safe_threshold_increase=0`
- 还原原版：`recall_threshold_min=50, recall_threshold_max=50, overstay_min=0, overstay_max=0`

## 关联项目

本编辑器是「熵引擎 — TM2 团战节奏改造」Mod 的配套工具。

- Mod：Entropy Engine (MOD_ID: tm2_bp_ai)
- Workshop ID：3763852408
- 创意工坊：https://steamcommunity.com/sharedfiles/filedetails/?id=3763852408
- 版本：v2.4

---

# Entropy Engine Config Editor

A graphical TOML config editor for the "Entropy Engine" mod in Teamfight Manager 2. Adjust AI behavior parameters with sliders instead of editing TOML by hand.

---

## Quick Start

1. Double-click `熵引擎配置编辑器.exe` in the mod directory
2. On first run, if prompted for a missing .NET 10 Runtime, click the download link in the dialog to install
3. Drag sliders to adjust parameters → Save
4. Restart the game

## Tech Stack

- WinForms (.NET)
- Target Runtime: .NET 10 Runtime
- Config Format: TOML

## Features

- **Slider-based Tuning**: All AI behavior parameters adjustable via sliders — no manual TOML editing required
- **Bilingual UI**: ZH/EN language toggle (compact uppercase buttons, 32×20 fixed size, active language in bold)
- **Auto Path Detection**: Auto-loads config from the Workshop directory by default
- **Manual Browse**: Use the 📁 button to manually select a config directory
- **Presets**:
  - Aggressive: recall threshold 8~18%, overstay chance 20~38%, losing-side recall reduction 15%, losing-side overstay bonus 30%
  - Conservative: recall threshold 32~46%, overstay chance 1~5%, losing-side bonuses zeroed, winning-side recall threshold +10%
- **Config Priority**: config.user.toml > config.toml > built-in defaults (user configs survive mod updates)

## Parameters

### Chaos Recall (chaos_recall)

| Param | Default | Description |
|-------|---------|-------------|
| `recall_threshold_min` | 22 | Recall HP lower bound (%) |
| `recall_threshold_max` | 33 | Recall HP upper bound (%) |
| `overstay_min` | 10 | Overstay chance lower bound (%) |
| `overstay_max` | 18 | Overstay chance upper bound (%) |

### Comeback (comeback)

| Param | Default | Description |
|-------|---------|-------------|
| `danger_hp` | 15 | Danger HP threshold (%) |
| `threshold_reduce` | 8 | Losing-side recall threshold reduction (%) |
| `overstay_increase` | 15 | Losing-side overstay bonus (%) |
| `safe_threshold_increase` | 5 | Winning-side recall threshold increase (%) |

## Common Configs

- Disable Comeback: `threshold_reduce=0, overstay_increase=0, safe_threshold_increase=0`
- Restore Vanilla: `recall_threshold_min=50, recall_threshold_max=50, overstay_min=0, overstay_max=0`

## Related

This editor is a companion tool for the "Entropy Engine — TM2 Match Rhythm Overhaul" mod.

- Mod: Entropy Engine (MOD_ID: tm2_bp_ai)
- Workshop ID: 3763852408
- Workshop: https://steamcommunity.com/sharedfiles/filedetails/?id=3763852408
- Version: v2.4
