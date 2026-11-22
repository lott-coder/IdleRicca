# 波次奖励权重与 Blaze Charge 火焰充能

## 改动

- `WaveSettings.asset` 和 `WaveSettings.cs` 将 City Demo 的奖励权重设为技能 50、生命 25、普攻伤害 25。`WaveRewardPool.cs` 在每张卡上先按类型抽取，再按该类剩余等级抽取。技能候选失去资格时，两种属性各占 50%。场景使用的本地 Unity 配置资产由仓库现有资源忽略规则排除。
- `PlayerActions.cs` 与 `SkillDefinition.cs` 在 Blaze Charge 动作完成后开始 20 个游戏秒的充能。若该技能已命中、清波提前结束动作，同样开启充能。充能使普攻基础值与本局攻击奖励之和提高 20%，重复施放刷新时间。到期或死亡后恢复原伤害。
- `RiccaPlayer.cs` 将源火焰挂在剑刃碰撞盒中心，按施放与充能状态播放。场景和预制体沿用既有剑刃及粒子引用。

## 验证

在独立 Unity 2022.3.52f1c1 工程副本执行。

| 验证 | 结果 |
|---|---|
| 旧版对照 | 5 项中 4 项在预期断言失败，分别覆盖场景权重、第二张卡的概率偏差、充能伤害、刷新时间。[Red.xml](../../../Logs/BlazeChargeRewards/Red.xml) |
| 编译 | 退出码 0，C# 错误和警告均为 0。[Compile.log](../../../Logs/BlazeChargeRewards/Compile.log) |
| EditMode | 75/75 通过。技能可用和不可用各抽取 12,000 组，分别统计三个卡位的比例。覆盖充能普攻实际命中、精确到期、重复施放、取消和清波提前结算。[EditMode.xml](../../../Logs/BlazeChargeRewards/EditMode.xml) |
| PlayMode | 19/19 通过。由实际技能按钮施放 Blaze Charge，确认火焰挂在剑上、动画结束后继续燃烧、普攻伤害由 25 变为 30，约 20 个游戏秒后火焰消失且普攻伤害恢复到 25。[PlayMode.xml](../../../Logs/BlazeChargeRewards/PlayMode.xml) |
| 场景截图 | 施放结束数秒后，剑刃周围仍可见橙红火焰。到期后剑身恢复原样。[火焰充能](../../../Logs/BlazeChargeRewards/Screens/Blaze-Charge-Sword-Buff-720p.png) · [充能结束](../../../Logs/BlazeChargeRewards/Screens/Blaze-Charge-Expired-720p.png) |
| 程序集 | 生产程序集引用符合项目白名单。[Assembly-References.txt](../../../Logs/BlazeChargeRewards/Assembly-References.txt) |
