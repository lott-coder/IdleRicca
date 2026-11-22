# 问题与验证记录

> gameplay/enemy-spawn-ai v0.0.1 ｜ 2026-11-22

## 已解决

- 探针发现 disabled BoxCollider 无法执行本版 Unity 的 ComputePenetration。生产几何为 Ignore Raycast 层的 Trigger。攻击窗口由命令守卫，死亡及退役立即关盒。已用真实 Ricca CharacterController 验证命中和空挥。
- 大额度配置导致启动时过量分配。容量仍按配置执行，集合初始容量最高 16，低频生成时扩展。新增 int.MaxValue 回归。
- 低帧率使归一化进度超过 1 时，保留实际采样终点。生效窗口仍只取相交区间。L2 重现 Ricca 在有效窗口之后才进入盒体，HP 保持 100。
- City 在 D3D11 的隔离项目中烘焙与测试。负候选深入既有混凝土栏杆和花坛，另构造完全断开的导航区。均由生成器拒绝。

## 最终门禁

| 门禁 | 证据 | 结果 |
|---|---|---|
| G1、G2 | Logs/EnemyAI/Compile-Final.log | 退出码 0，无 C# 编译错误或警告 |
| G3 | Logs/EnemyAI/EditMode-Final.xml | 22 通过、0 失败、0 跳过 |
| G4 | 读取独立副本的四层 asmdef 并比对白名单 | 4 项通过 |
| G5 | DemoAssetTests 检查 Demo 场景和四个 prefab 树 | 无 Missing Script |
| G6 | Logs/EnemyAI/PlayMode-Final.xml | 8 通过、0 失败、0 跳过 |
| G7 | 没有存档分区与 schema 变更 | 不适用 |
| G8 | DemoAssetTests 检查敌人配置，另查新增资产 GUID | 通过 |
| 性能 | Logs/EnemyAI/allocation-probe.csv | 隔离 Unity Editor 中抽样四种脚本回调，稳态 GC 0 B。回调计时覆盖测试机器脚本。 |

## 视觉验证

| 用例 | 前置及操作 | 观察 | L4 |
|---|---|---|---|
| 追踪与出招 | 放置场景墙和 Ricca，观察敌人绕到另一侧并进入有效窗口 | 哥布林移动时有明确步态，靠近 Ricca 后挥击 | Enemy-Chase.png、Enemy-Attack.png |
| 躲开攻击盒 | 攻击起手后经测试入口移动 Ricca 出盒，再跨过有效窗口 | 哥布林挥过原位置，Ricca HP 保持 100 | Enemy-Dodge.png、PlayMode-Final.xml |
| 受伤和死亡 | 验证 City 真实模型，驱动对应 Hit 和 Death，另通过实际 Ricca 剑击验证退出碰撞 | 骨骼姿态改变，致死同帧退出受击查询，完整播放后销毁 | Goblin-Hit.png、Goblin-Death.png、Skeleton-Hit.png、Skeleton-Death.png、Hellhound-Hit.png、Hellhound-Death.png |

L4 来自独立 Unity 自动化相机。真实键鼠手感待在主工程试玩。

## 安装

经用户确认，60 个产物已安装到主工程，安装后复核 SHA-256 与验证副本一致。见 Logs/EnemyAI/installed-sha256.json。
