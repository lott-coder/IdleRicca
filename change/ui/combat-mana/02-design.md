# 技术设计 — 战斗 HUD 的局内蓝量

> ui ｜ combat-mana ｜ v0.0.1 ｜ 2026-11-22 ｜ 已联合复审，已获用户确认  
> 本模块只读使用 `PlayerCombatStatus.CurrentMp` / `MaxMp`，数据由 gameplay 的本地 Application 保存与结算。
> 在途契约终检为 0 冲突，已同步 gameplay/skill-pool-mana 的 C1–C5 契约。

- `CombatHud` 绑定已有 `Mana fill` 及显示当前/上限的文本，复用 `SetFill` 和稳定数值缓存。HP 和 MP 依各自快照变化刷新。Scene 保留既有的被动 HUD EventSystem 约束。
- `SkillSelectionHud` 读取只读 `SkillStatus`，在标题或状态文字提示 10 MP 及缺蓝。当前没有能施放的槽时，禁用已装备按钮并保持 F / Esc 关闭入口。接线沿用 WaveHud 的交互 Canvas。
- UI 测试在真实 Demo 以授权的源动作进入施放，通过真实按钮验证扣蓝和组件更新，同时记录空槽、就绪及缺蓝两档分辨率。回归保护血量、连击和原有按钮。
- C1 共用字段新增而兼容原签名，C2 无存档变更，C3 MP 上限源于现有玩家配置，C4 仅 HUD Prefab 接线，C5 保留原读取入口。
