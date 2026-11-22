# 问题与验证 — enemy-spawn-navigation-distance v0.0.1

## 现象与根因

City 第 1 波的三只敌人能正常生成，最后一只可能长时间未接战。原刷怪器按 7–14 米水平距离采样并判定范围。其完整性检查接受了能绕街区返回的路径。固定种子 3 和 9 的哥布林导航路线分别为 153.18 米和 153.78 米，首次攻击分别发生在 80.50 秒和 81.02 秒。

## 改动与文件

候选搜索以 Ricca 的导航投影为圆心。平面搜索覆盖最大距离以内，最终导航路径长度按拐点逐段累计，落在原配置区间才发放名额。

| 文件 | 改动 |
|---|---|
| `Assets/Scripts/Application/Gameplay/EnemyPopulation.cs` | 覆盖候选搜索圆，预约按导航距离平方校验。 |
| `Assets/Scripts/Presentation/Gameplay/EnemySpawner.cs` | 只接受完整且符合配置长度的路径，复用拐点数组。 |
| `Assets/Scripts/Presentation/Gameplay/EnemySpawnSettings.cs` | Inspector 提示明确距离口径。 |
| `Assets/Tests/EditMode/EnemyPopulationTests.cs` | 验证候选覆盖范围与固定随机种子的复现。 |
| `Assets/Tests/PlayMode/EnemyGameplayTests.cs` | 回归 City 绕行、正常和过短候选，按路线长度校验实际刷怪。保留命中特效新增用例。 |

## 验证

| 证据 | 结果 |
|---|---|
| [红测](../../../Logs/EnemySpawnNavigationFix/NavSpawn-Red.xml) | 原代码错误接受直线范围内且路径超过 140 米的候选。 |
| [绿测](../../../Logs/EnemySpawnNavigationFix/NavSpawn-Green.xml) | 同一候选拒绝。正常路线接受，过短路线拒绝。 |
| [固定种子](../../../Logs/EnemySpawnNavigationFix/NavSpawn-ReplayedSeeds.log) | 种子 3 路线 10.35 米、5.02 秒首次攻击。种子 9 路线 11.81 米、6.02 秒首次攻击。 |
| [编译](../../../Logs/EnemySpawnNavigationFix/Verified-Compile.log) | Unity 2022.3.52f1c1 退出 0。 |
| [EditMode](../../../Logs/EnemySpawnNavigationFix/Verified-EditMode.xml) | 69 通过、0 失败。 |
| [PlayMode](../../../Logs/EnemySpawnNavigationFix/Verified-PlayMode.xml) | 19 通过、0 失败。包含实际刷怪、名额回收和十波完成。 |
| [安装前哈希](../../../Logs/EnemySpawnNavigationFix/before-install/manifest.json) | 五个目标文件按原文与隔离验证副本核对。 |

首轮全量 PlayMode 收尾停滞，另一次复验的隔离工程缺少并行迭代的命中特效资源。重新同步最新的完整 Assets 后跑完编译和全量测试。
