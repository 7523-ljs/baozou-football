# 暴走足球 ⚽

一款基于 **Unity** 开发的 2D 足球游戏。

## 项目介绍

暴走足球是一款 2D 街机风格足球游戏，包含完整的球员控制系统、技能系统、动画状态机和 UI 界面。

## 技术栈

- **引擎**：Unity (C#)
- **核心功能**：玩家控制、物理碰撞、技能系统、动画管理
- **场景**：主菜单、游戏场景

## 核心脚本

| 脚本 | 功能 |
|------|------|
| `PlayerController.cs` | 玩家移动、踢球、抢断控制 |
| `Ball.cs` | 足球物理与运动逻辑 |
| `GameManager.cs` | 游戏状态管理、计时计分 |
| `SkillSystem.cs` | 技能系统（射门、传球、抢断等） |
| `UIManager.cs` | 游戏 UI 管理 |
| `CameraController.cs` | 摄像机跟随与震动效果 |
| `PlayerAnimator.cs` | 玩家动画状态控制 |
| `MainMenuController.cs` | 主菜单逻辑 |

## 如何运行

1. 使用 Unity 2022.3 或更高版本打开项目
2. 打开 `Assets/Scenes/MainMenu.unity` 场景
3. 点击 Play 运行
