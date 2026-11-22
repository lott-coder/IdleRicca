# 变更与验证 — 战斗命中特效

## 改动

- Ricca 的普攻和五个主动技能在有效命中时，于每个受伤敌人的接触位置播放 `SwordHitBlue`。敌方命中使 Ricca 损失 HP 时，在其胶囊受击侧播放 `SwordHitRed`，覆盖致死伤害。
- `SwordHitbox`、`SkillHitbox` 和 `EnemyActor` 从 Application 的 `AttackHit` 结果触发粒子。`RiccaPlayer` 在初始化时绑定并预建粒子实例，`ImpactEffectPool` 复用并清理实例。保留技能盒的既有调用签名，兼容现有性能采样。
- 两个源 Prefab 来自 `D:/Learning/Unity/_ricca/work/project/Assets/GameObject/`，安装于 `Assets/Art/Demo/Ricca/CombatImpacts/`。迁移时移除旧脚本，将根位置归零，关闭自动播放，保留源粒子梯度和材质引用。搬运 9 项资源，复用 4 项已导入依赖。`Assets/Prefabs/Demo/Ricca.prefab` 直接引用两个根粒子组件。

## 证据

| 项目 | 结果 | 文件 |
|---|---|---|
| 编译及 EditMode | Unity 2022.3.52f1c1 编译成功，69 通过、0 失败 | `Logs/CombatImpactVerification/Impact-Final-EditMode.log`、`Impact-Final-EditMode.xml` |
| 五技能 PlayMode | 1 项用例覆盖全部五项技能，逐一确认有效伤害与单个命中粒子，覆盖双受击体去重和移动路径回归 | `Logs/CombatImpactVerification/Impact-Skills.xml` |
| 普攻与受击 PlayMode | 4 通过、0 失败，覆盖普攻多目标、空挥、卡肉、敌方命中、闪避、致死与重载 | `Logs/CombatImpactVerification/Impact-Final-Contact.xml` |
| 图像 | 普攻命中呈蓝青色，Ricca 掉血呈暖红橙色，五技能均生成命中帧截图 | `Logs/CombatImpactVerification/Screens/` 中的 `Ricca-Deals-Damage.png`、`Ricca-Receives-Damage.png` 和 `Impact-*-720p.png` |
| 安装 | 主工程 33 个文件与验证副本的 SHA-256 一致 | `Logs/CombatImpactVerification/installed-sha256.json`、`source-assets.json` |
