# 执行计划 - v0.0.1

> 模块：gameplay ｜ 子分类：basic-character-controls
> 日期：2026-11-22 ｜ 状态：已实施并验收

## 1. 任务清单

| # | 任务 | 输出文件 | 层 | 测试 | 资产 | 依赖 | 状态 |
|---|---|---|---|---|---|---|---|
| 1 | 加规则测试：轴向净化、镜头相对及斜走、Shift、HP 边界、每目标去重和有效窗口批次。再加控制与伤害实现。 | `Assets/Tests/EditMode/GameplayRulesTests.cs`、`Assets/Tests/EditMode/Game.Tests.EditMode.asmdef`、`Assets/Scripts/Domain/Gameplay/{MovementDirection,HitPoints}.cs`、`Assets/Scripts/Application/Gameplay/{PlayerActions,AttackSampleBatch,EnemyHealth}.cs` | Domain、Application | L1 | — | 已批准设计 | ✓ |
| 2 | 加 Ricca 快照输入、角色胶囊和 Animator 接入。加主相机和光标的生命周期控制。 | `Assets/Scripts/Presentation/Gameplay/{RiccaPlayer,ThirdPersonCamera}.cs` | Presentation | L1、L2、L3、L4 | A2、A3、A5 | 1 | ✓ |
| 3 | 加剑身盒、目标注册与 0 HP 消失。加配置及 Editor 剑身轨迹烘焙。 | `Assets/Scripts/Presentation/Gameplay/{NormalAttackSettings,EnemyHealthSettings,SwordHitbox,EnemyTarget}.cs`、`Assets/Editor/Gameplay/NormalAttackSettingsEditor.cs` | Presentation、Editor | L1、L2、L4 | A1、A2、A4 | 1、2 | ✓ |
| 4 | 经 Unity Editor 导入原跑步片段、保存配置、预制体、Animator 与当前 City Demo 接线。 | 下节 A1～A6 所列资产及对应 `.meta` | Unity Editor、Presentation | L1、L2 | A1～A6 | 2、3、City 已归档 | ✓ |
| 5 | 加真实 Animator、角色输入和物理命中的 PlayMode 测试。拓展存量资产测试检查 Ricca 跑步以及新配置，保留原角色验证。 | `Assets/Tests/PlayMode/RiccaGameplayTests.cs`、`Assets/Tests/PlayMode/Game.Tests.PlayMode.asmdef`、`Assets/Tests/PlayMode/DemoRenderingTests.cs`、`Assets/Tests/EditMode/DemoAssetTests.cs` | 测试 | L1、L2、L4 | 测试加载 A1～A6 | 1～4 | ✓ |
| 6 | 运行全量门禁，记录截图、手测与审核，准备工具归档材料。 | `Logs/GameplayVerification/`、`change/gameplay/_tmp/{04-review-report,05-issues,06-knowledge-update}.md`、`knowledge/gameplay.md` | 工作流 | L0～L4 | 工具生成规格 | 5 | ✓ |

**状态**：☐ 未开始 / ◐ 进行中 / ✓ 完成 / ✗ 受阻

## 2. 资产修改清单

使用 Unity Editor 编辑、导入和保存资产，优先在独立工程副本中执行批处理。用户确认执行计划后按已授权范围安装，保留最新 City 版本的备份和原有 GUID。

| # | 类型 | 资产路径 | 操作 | 验收方式 |
|---|---|---|---|---|
| A1 | ScriptableObject | `Assets/Config/Gameplay/{RiccaNormalAttack,GoblinHealth,SkeletonHealth,HellhoundHealth}.asset` | 攻击默认 25，敌人独立初始 100。引用攻击片段及有效窗口，生成剑刃中间姿态。 | 检查合法值、独立可编辑、无断引用、轨迹源一致。 |
| A2 | Prefab | `Assets/Prefabs/Demo/Ricca.prefab` | 接入 CharacterController、动画、输入与随武器盒。 | Inspector 组件齐全，测试接地、墙面与攻击。 |
| A3 | Animator、Clip | `Assets/Art/Demo/Controllers/Ricca.controller`、`Assets/Art/Demo/Ricca/AnimationClip/rc_run_loop.anim` | 导入原跑步片段并清除无绑定脚步事件，为 Move 接入走跑和 HoldAttack 参数。沿用普通攻击与 Idle 片段。 | 对应骨骼实际播放。保留原参数、三状态和旧攻击退出行为。 |
| A4 | Prefab | `Assets/Prefabs/Demo/{Goblin,Skeleton,Hellhound}.prefab` | 增加受击 Collider 与 HP 组件，独立配置，保留现有渲染、动画。 | 各目标从默认值或调参后的初始值开始，归零显示和碰撞同时关闭。 |
| A5 | Scene | `Assets/Scenes/Demo.unity` | 把 Main Camera 序列化绑定到 Ricca，保留原场景 GUID、City 环境和四个实例。 | 从构建首序号运行，只有一个主相机，真实鼠标转动能驱动镜头。 |
| A6 | ProjectSettings | `ProjectSettings/TagManager.asset` | 通过 Unity 配置专用 `EnemyHurtbox` 层，现有层槽位与名称保留。 | 剑身盒查询只接收专用层上的已绑定 Collider。 |

## 3. 依赖关系

任务 1 先为后续实现定义接口。任务 2 和 3 完成后才保存新资产。资产接线完成后从构建入口验证。原 City 的 5 条 EditMode 和 2 条 PlayMode 为对照基线，人物操作修改后重新全量运行。

套件中原动画测试继续经过 `Moving` 和 `Attack` 入口。相机测试自行取景时暂停跟随组件，保留原光影取样和渲染断言。新增 PlayMode 验证正常相机接线及跟随。资产测试把 Ricca 由三段扩大到四段动作，三种敌人继续核验原动作。

## 4. 验证计划

| 阶段 | 动作 | 通过标准 |
|---|---|---|
| 规则 | 先写 EditMode 用例再实现；检查跨越全窗、旧批次、正常结束、掉帧和取消。 | 用例执行，规则与数值断言全绿。 |
| 组件和资源 | 使用 Unity 编译、检查配置、asmdef 方向和 Missing Script，执行全量 EditMode 与有 D3D11 图形设备的 PlayMode。 | 无编译错误，无新增编译警告，项目测试程序集均绿。原动画和 City 渲染两项均实际运行且无跳过。 |
| 端到端 | 使用真正的 Animator、Collider 和 City 场景，向输入入口注入键鼠快照。低帧率测试独立用实际 Animator 姿态设置命中和未命中位置。 | 移动方向、动画、相机接线、跨帧命中、HP 归零与重载均通过。 |
| 表现 | 生成 L4 截图，记录鼠标锁定、步跑切换、攻击的手测步骤与实际结果。待机、移动和有效挥击分别用 Profiler 采样 GC Alloc。 | 角色保持可见、鼠标锁定不误触发攻击、相机可旋转、跑步和攻击易辨。真实键鼠手感使用人工验证，分配另以 Profiler 记录。 |
| 归档 | 复跑 G1–G8 的适用项，记录不适用项，校验规格并预览归档。 | 代码审查与用户确认齐备，证据对应最终资产。 |

## 5. 验收标准对照

| AC | 覆盖任务 | 证据等级 | 具体判据 | 状态 |
|---|---|---|---|---|
| AC-1 | 2、4、5、6 | L2、L3、L4 | 跟随、偏航和俯仰由 PlayMode 断言。光标捕获、失焦及回焦由用户试玩。镜头截图见验证记录。 | ✓ |
| AC-2 | 1、2、4、5、6 | L1、L2、L3 | 单测镜头相对方向、行走/跑步状态和斜向上限。PlayMode 复核真实位移与两个片段。 | ✓ |
| AC-3 | 1、3、4、5、6 | L1、L2、L3 | 按住 WASD 和 Shift 起手，整个普攻期间水平位置和朝向固定，镜头仍响应，末次批次后继续当前移动输入。实际剑刃弧线跨帧命中，端点假接触、空挥和收招不扣血。每敌只结算一次。 | ✓ |
| AC-4 | 1、3、4、5 | L1、L2 | 三敌独立 100 HP，默认命中扣 25。第四击同帧目标不可见且物理查询无目标 Collider，重进恢复，配置变体有效。 | ✓ |

## 6. 回滚方案

当前工作区没有 Git 元数据，按用户确认通过文件备份回滚。安装前备份已存在的每个改动路径及其 `.meta`，记录原始 SHA-256，安装前再比较一次以发现并行修改。若验证或安装中途失败，按清单还原受影响的原文件，并仅撤回本轮清单中的新文件。回滚后重新对比 SHA-256，重新导入资源。存档结构未改。

安装后逐文件复核与已验证副本的 SHA-256。临时接线脚本和测试辅助资产保留在隔离副本，正式安装遵循计划路径清单。若主工程后来重新保存变更资产，重跑该资产对应的验收。

## 7. 风险

| 风险 | 影响 | 应对 |
|---|---|---|
| 编辑器当前打开 IdleRicca，同目录批处理会冲突 | 测试不稳或热重载中断人工验证 | 在独立副本运行完整门禁。安装只在无活动试玩或编辑共享资产时执行。 |
| 当前默认触发窗口短 | 掉帧时真实碰撞可能被跳过 | 烘焙中间姿态、测试同帧跨全有效区间，捕获轨迹与实际 Animator 的差异。 |
| 第三人称覆盖原固定机位 | 渲染验收会被镜头覆盖 | 测试专用机位暂时挂起跟随。正常场景启动另行覆盖。 |
| 攻击和运动动画共用 Animator | 混合状态可能影响轨迹和退场 | 玩家起手直接从 0 播放并在最后批次后主动返回 Idle，原触发器保持原过渡。 |
| 鼠标锁定受真实 Game 窗口焦点影响 | 仅在 batchmode 不能证明真实锁定与手感 | 光标逻辑做端到端和失焦测试，补真实键鼠人工验证。 |

## 8. 任务动作与前置检查

- [x] City 已归档，City 版本的编译、EditMode 5/5、PlayMode 2/2 有可追溯记录。
- [x] 已核对原 Animator、Prefab 与 Demo 构建入口的引用点。
- [x] 已确认无存档迁移，新增序列化只涉及玩法组件、配置和资源引用。
- [x] 用户确认在独立副本中由 Unity Editor 批处理完成资产接线并授权开工。
- [x] 已备份并校验 21 个基线文件，清单见 `Logs/GameplayVerification/baseline/sha256.json`。
- [x] 已在独立项目完成实现与机器验证，主工程 66 个产物通过安装一致性校验。

## 9. 每批收尾三件事

更新任务状态，保存本批的执行与验证证据，把真正影响后续开发的问题记录到 `05-issues.md`。无 Git 工作树时按已确认方案对本批产物留文件和哈希证据。
