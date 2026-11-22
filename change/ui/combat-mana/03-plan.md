# 实施计划 — 战斗 HUD 蓝量

> ui ｜ combat-mana ｜ v0.0.1 ｜ 2026-11-22 ｜ ✓ 已归档至 change/ui/combat-mana v0.0.1

| 任务 | 路径 | 证据 | 依赖 |
|---|---|---|---|
| 蓝条与缺蓝状态 | `Assets/Scripts/Presentation/UI/CombatHud.cs`、`Assets/Scripts/Presentation/UI/SkillSelectionHud.cs` | L2、L4，真实状态与输入 | 已获确认的 `PlayerCombatStatus` 和 `SkillStatus` 契约 |
| HUD Prefab | `Assets/Prefabs/UI/CombatHud.prefab`、`Assets/Prefabs/UI/SkillSelectionHud.prefab` | 满蓝、耗蓝、缺蓝与重开截图，原被动射线回归 | 隔离 Editor 接线 |
| 联合验证与归档 | gameplay [实施计划](../../gameplay/skill-pool-mana/03-plan.md) 任务 6–9 | L0、L2、L4 和 UI C1–C8 | 同一次验证版本安装后归档，回退清单统一维护 |
