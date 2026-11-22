# 敌人移动漂移

## 复现

在 Demo 加载 prefab 使用的 Animator，关闭 NavMeshAgent。记录固定角色根节点，并采样 Move 两个完整循环。允许水平偏移上限为 0.025 米。

- Goblin 原始最大水平偏移 1.3560 米。
- Skeleton 原始最大水平偏移 0.7436 米。
- Hellhound 原始偏移 0.0000 米。
- 实际角色根在三个采样中都没有平移。失败证据为 `Logs/EnemyDrift/Drift-Red.xml`。

## 根因和修复

walk_5 与 skeleton_walk 的 rig 子节点 root 有一段 local Y 前进位移。rig 坐标系绕 X 旋转，因此曲线推着整个模型做水平运动。角色根及受击盒仍按导航位置移动，循环回卷时出现滑移。用 Unity Editor 将两段片段的该轴固定为 prefab 的初始值，实际关节旋转数值及其他节点位置曲线保持原值。

## 回归证据

- `Logs/EnemyDrift/Drift-Green.xml` 用同一测试再次测量，三类敌人的模型根水平偏移均为 0.0000 米。
- `Logs/EnemyDrift/EditMode.xml` 中 54 通过，`Logs/EnemyDrift/PlayMode.xml` 中 15 通过，无失败或跳过。原骨骼动作和实际导航绕障仍通过。
- `Logs/EnemyDrift/Enemy-Chase.png` 记录测试场景的追踪姿态。
- 两段动画与新增回归测试已安装到主工程，三个文件哈希与独立验证副本一致。

## 涉及文件

- `Assets/Art/Demo/Ricca/AnimationClip/walk_5.anim`
- `Assets/Art/Demo/Ricca/AnimationClip/skeleton_walk.anim`
- `Assets/Tests/PlayMode/DemoAnimationTests.cs`，回归用例 `EnemyMoveLoopsKeepTheVisualRootAlignedWithTheNavigationRoot`

本轮恢复 R8 的追踪表现，现有追踪、出招和动画契约继续适用。按已有基线使用无增量归档。

## 项目约束

用户要求将直接完成的项目改动纳入归档门禁。已同步 `AGENTS.md`、`docs/rules/工作流执行.md` 和 `change/README.md`，明确最小归档记录及同模块在途迭代冲突时的处理。
