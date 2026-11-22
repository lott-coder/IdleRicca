# Ricca 技能施放期间的霸体与减伤

## 改动原因与范围

Ricca 成功施放任意技能时立即获得霸体和 80% 减伤，直到技能动作完成或取消。有效受击按原伤害的五分之一向上取整扣血。若 Ricca 仍存活，当前施放和命中批次继续有效。若 HP 归零，则取消施放并进入死亡流程。

- `Assets/Scripts/Domain/Gameplay/HitPoints.cs` 计算施放期间的整数伤害。
- `Assets/Scripts/Application/Gameplay/PlayerActions.cs` 在敌方伤害入口按当前施放状态结算，并记录需要播放的伤害反应。
- `Assets/Scripts/Presentation/Gameplay/RiccaPlayer.cs` 按伤害反应计数播放普通受击或死亡动作。
- `PlayerSkillTests.cs`、`SkillManaTests.cs` 与 `RiccaSkillGameplayTests.cs` 覆盖扣血、动作延续、退出保护、致命伤害和扣蓝。

## 验证记录

Unity 2022.3.52f1c1 在独立工程副本中运行。首次完整 EditMode 在无图形设备模式下因 Demo 场景创建 RenderTexture 失败，启用 D3D11 后复跑通过。

| 验证 | 结果 |
|---|---|
| 旧版施放保护对照 | 10 条中 3 条在预期断言失败。25 点与 12 点伤害都全额扣血，1 点伤害中断施放。[Red-EditMode.xml](../../../Logs/SkillCastProtection/Red-EditMode.xml) |
| 编译 | 退出码 0，0 条 C# 错误或警告。[Compile.log](../../../Logs/SkillCastProtection/Compile.log) |
| EditMode | 69/69 通过。覆盖选择阶段按全额扣血、施放中减伤且保留技能命中、结束和取消后恢复全额伤害、极大致命伤害、已扣蓝的致死取消，以及 Demo 场景和技能资产绑定。[EditMode.xml](../../../Logs/SkillCastProtection/EditMode.xml) |
| PlayMode | 18/18 通过。通过技能菜单实际点击施放，5 点伤害使 Ricca 只扣 1 HP，Animator 维持技能状态，施放结束后的同等攻击扣 5 HP 并播放普通受击。[PlayMode.xml](../../../Logs/SkillCastProtection/PlayMode.xml) |
| 场景截图 | 施放中受击后 HUD 显示 99 HP，施放结束受击后显示 94 HP 并处于普通受击状态。[施放中](../../../Logs/SkillCastProtection/Screens/05-Protected-Cast-After-Hit-720p.png) · [施放后](../../../Logs/SkillCastProtection/Screens/06-Normal-Reaction-After-Cast-720p.png) |
| 程序集 | 生产程序集引用符合项目白名单。[Assembly-References.txt](../../../Logs/SkillCastProtection/Assembly-References.txt) |
