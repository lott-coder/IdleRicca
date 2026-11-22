# IdleRicca

IdleRicca 是一个 Unity 3D 动作游戏原型，用于验证角色操控、战斗和敌人波次等玩法。

## Unity 版本

使用 Unity `2022.3.52f1c1`。版本记录见 [ProjectVersion.txt](ProjectSettings/ProjectVersion.txt)。

## 代码目录

| 目录 | 用途 |
| --- | --- |
| [Assets/Scripts/Domain](Assets/Scripts/Domain) | 领域模型与玩法规则 |
| [Assets/Scripts/Application](Assets/Scripts/Application) | 游戏用例、流程编排与状态更新 |
| [Assets/Scripts/Infrastructure](Assets/Scripts/Infrastructure) | 基础设施层程序集定义 |
| [Assets/Scripts/Presentation](Assets/Scripts/Presentation) | Unity 组件、输入与界面表现 |
| [Assets/Editor](Assets/Editor) | Unity 编辑器工具 |
| [Assets/Tests](Assets/Tests) | EditMode 和 PlayMode 测试 |

## 运行 Demo

仓库只跟踪代码、工程配置及配套文档。运行 Demo 需要本地的场景、预制体、游戏配置和美术资产。

用上述版本的 Unity 打开包含这些资产的本地工程，加载 `Assets/Scenes/Demo.unity`，然后进入 Play Mode。
