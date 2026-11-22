# 执行计划 - v0.0.1

> 模块：gameplay ｜ 子分类：enemy-spawn-ai  
> 日期：2026-11-22 ｜ 状态：实现、安装与归档已验证  
> 对应技术设计：02-design.md ｜ 配套规格：change/core/_tmp/07-spec-delta.md

## 1. 任务清单

| # | 任务 | 输出文件 | 层 | 测试 | 资产 | 依赖 | 状态 |
|---|---|---|---|---|---|---|---|
| 1 | 备份并记录原文件哈希。复制当前工程的 Assets、Packages、ProjectSettings 到独立 Unity 工程，先采集编译与测试基线。 | `Logs/EnemyAI/baseline/`、`Logs/EnemyAI/project/` 中的工程和日志 | 验证环境 | L0、L1、L2 | — | 用户明确开始 | ✓ |
| 2 | 加规则测试后实现会话名额、首轮种类选择、距离与合法性守卫，覆盖 1、3、满额、无点、随机可复现、重复释放。 | `Assets/Tests/EditMode/EnemyPopulationTests.cs`、`Assets/Scripts/Application/Gameplay/EnemyPopulation.cs` | Application | L1 | A1 | 1 | ✓ |
| 3 | 加战斗规则测试后实现敌人 AI 状态、动画有效窗口批次及命中去重，扩展 Ricca 生命和归零取消，保留 Ricca 既有普攻及 EnemyHealth 构造。 | `Assets/Tests/EditMode/EnemyCombatRulesTests.cs`、`Assets/Scripts/Application/Gameplay/{EnemyActions,EnemyHealth,PlayerActions}.cs` | Domain 规则复用、Application | L1 | A1 | 1 | ✓ |
| 4 | 加生成配置、候选地面/占位/完整路径检查、场景生成器和名额交接。预检失败无实例，生成失败按所有者回滚，OnDestroy 才释放已接管实例的 ID。 | `Assets/Scripts/Presentation/Gameplay/{EnemySpawner,EnemySpawnSettings}.cs` | Presentation | L1、L2 | A1、A3、A5 | 2、3 | ✓ |
| 5 | 加战斗配置和敌人驱动，接 NavMeshAgent、目标身份、攻击盒、HoldAttack 和生命状态。修 EnemyTarget 归零顺序及 RiccaPlayer 的生命接入。 | `Assets/Scripts/Presentation/Gameplay/{EnemyActor,EnemyCombatSettings,EnemyTarget,RiccaPlayer}.cs` | Presentation | L2、L4 | A1–A4 | 3 | ✓ |
| 6 | 在独立副本 Unity 中导入六段动画，按类型校准命中窗口和盒几何，通过 Editor 工具保存 prefab、controller、配置、Demo 与 NavMeshData。 | A1–A5 资产和 `.meta`，`Assets/Editor/Gameplay/CityEnemyNavigationBaker.cs`；一次性接线工具留在 `Logs/EnemyAI/project/` | Unity Editor、Presentation | L1、L2、L4 | A1–A5 | 4、5 | ✓ |
| 7 | 加自动生成、绕障、攻击窗口、死亡、Ricca 归零及生成失败名额的端到端回归。更新变更了的旧场景前提，保留渲染、镜头、动画绑定及剑形状断言。 | `Assets/Tests/EditMode/DemoAssetTests.cs`、`Assets/Tests/PlayMode/{EnemyGameplayTests,DemoAnimationTests,DemoRenderingTests,RiccaGameplayTests}.cs`、`Assets/Tests/PlayMode/Game.Tests.PlayMode.asmdef` | 测试 | L1、L2、L4 | 读取 A1–A5 | 6 | ✓ |
| 8 | 跑最终门禁、独立代码审查和可视证据。用户确认后将已验证文件从副本安装到主工程，校验哈希。 | `Logs/EnemyAI/`、`change/gameplay/_tmp/{04-review-report,05-issues}.md` | 验证、工作流 | L0–L4 | A1–A5 | 7 | ✓ |
| 9 | 沉淀 gameplay 知识，复核两份 delta，通过工具顺序归档并同步版本索引。 | `knowledge/gameplay.md`、`change/gameplay/_tmp/06-knowledge-update.md`、两模块归档工具产物 | 知识、规格 | C1–C9 | — | 8、Phase 4 审核确认 | ✓ |

**状态**：☐ 未开始 / ◐ 进行中 / ✓ 完成 / ✗ 受阻

## 2. 资产修改清单

Unity 批处理 Editor API 在独立副本内改场景和资产。跨工程导入保留各自骨骼绑定，初次生成的 `.meta` 与资源一并安装，现有资源的 GUID 保持原值。

| # | 类型 | 资产路径 | 操作 | 验收方式 |
|---|---|---|---|---|
| A1 | ScriptableObject | `Assets/Config/Gameplay/EnemySpawn.asset`、`Assets/Config/Gameplay/{GoblinCombat,SkeletonCombat,HellhoundCombat}.asset` | 保存默认 7–14 米、上限 3、各类型战斗数值与动画引用。保留已有敌人 Health 资产。 | 每个 prefab 使用独立配置，窗口、伤害、距离和引用通过检查。 |
| A2 | Animator 和片段 | `Assets/Art/Demo/Controllers/{Goblin,Skeleton,Hellhound}.controller`、`Assets/Art/Demo/Ricca/AnimationClip/{damage_7,death_7,skeleton_damage,skeleton_death,hellhound_damage,hellhound_death}.anim` | 从 `D:/Learning/Unity/_ricca/work/project/Assets/AnimationClip` 导入对应 Hit/Death，保留 Idle/Move/Attack 和旧手动触发。增加 HoldAttack 出口条件，校验非循环与事件清理。 | 五状态驱动真实骨骼，AI 跨 1 时保留攻击末批次，独立播放触发后回 Idle。 |
| A3 | 敌人 prefab | `Assets/Prefabs/Demo/{Goblin,Skeleton,Hellhound}.prefab` | 绑定 AI、Agent、Animator、敌人受击体、战斗配置及不参与刚体碰撞的攻击盒。 | 攻击盒覆盖对应动作，旧受击层仍映射到正确 HP。无 Missing Script。 |
| A4 | Ricca prefab | `Assets/Prefabs/Demo/Ricca.prefab` | 写入初始 HP 100，保留角色胶囊与原脚本。先在隔离副本以实际 BoxCollider 与 CharacterController 测重叠及分离。若目标形状查询不支持，再加尺寸与角色胶囊一致的专用受击胶囊。 | 盒内/外、关闭形状、偏移采样和其他敌人共处均有端到端断言。保持剑轨迹、相机与旧动作绑定。 |
| A5 | Scene、导航 | `Assets/Scenes/Demo.unity`、`Assets/Scenes/Demo/CityEnemyNavigation.asset` | 移除三个固定敌人实例，序列化绑定唯一生成器和 Ricca。用 Collider 体积烘焙 City 可玩路线，必要时增加经验证的实体或导航遮挡。 | 启动、合法点、安全失败、固定 City 负候选、可重烘焙、资源路径和原构建首场景正常。 |

## 3. 依赖关系

1 → 2、3 → 4、5 → 6 → 7 → 8 → 9。只在生产代码、资产和测试都接入后执行最终测试。烘焙重跑应更新同一个导航数据资产，而非生成新的无引用文件。

Application 保留 `PlayerActions` 构造的前三项、第四项 `expectedTargets` 及 `IPlayerCommands` 接口，新增初始 HP 作为尾部可选参数。敌人状态、资格命令与 slot ID 使用 02-design 中的契约。测试需要状态和几何时优先用公开只读查询或受限测试接入。装配阶段不新增反向 asmdef。

## 4. 验证计划

| 阶段 | 动作 | 通过标准 |
|---|---|---|
| 基线 | 以独立副本跑实际 Unity 2022.3.52f1c1，D3D11，顺序执行编译、EditMode、PlayMode，保存 XML、stdout 和警告。 | 从当前输入文件得到可比对基线。出现既有错误则定位并登记。 |
| 规则 | 在实现前写对应 EditMode 测试。实现后检查负数、NaN、边界和状态时序，运行全量。 | 规则全通过。围绕坏数据、空窗、跨全窗、重复批次、冷却、取消、HP 下界和名额事务均有断言。 |
| 几何和引用 | 保留 City Material 和地面检测。将原现场固定三敌断言换成新生成入口与 prefab 配置断言。增加实际骨骼采样和无缺失引用扫描。 | 三种敌人动作与资源有效，Animator 手工触发回 Idle，实际 City 起点和固定负候选走生产路径。 |
| 端到端 | 默认 Demo 测合法初始生成和死亡后补位。独立受控地形测障碍、合法/不合法路径与容量边界。绑定失败或运行中意外禁用的实例在同帧保留名额，等 Unity 实际销毁再断言恰好释放一次及后续可补位。独立战斗夹具测试 Ricca 的真实胶囊。 | 上限 1 和 3 含死亡状态均无超量。低帧率和正常动画进度都只扣一次，Ricca 归零与重载有断言。 |
| 视觉和性能 | 从真实相机留追踪、攻击、受伤、死亡证据，记录主动走出盒的步骤与结果。分别采集稳态追踪/攻击和生成帧的 GC 与帧时间，固定种子和场景。 | 关键动作可见，受伤打断和消失顺序正确。提供 L4。独立副本性能数据与测试设备范围明确。 |
| 最终门禁 | G1 编译、G2 警告对比、G3 全量 EditMode、G4 asmdef 白名单、G5 scene/prefab Missing Script、G6 全量 PlayMode、G8 配置。G7 本轮不适用，生命和生成状态仅限场景内存，存档结构无变更。 | 日志和 NUnit XML 对应最终文件，退出码和用例数齐全。未满足的门禁记为阻断。 |

新 PlayMode 用例等实际条件并设超时。旧 Ricca 剑弧线测试在能固定位置的夹具保留命中、多敌去重、空挥、余势和碰撞断言。归零时断言画面仍在、受击盒已关闭和 Death 最终退出。镜头动作测试应隔离敌人袭击，真实默认 Demo 另有敌人端到端覆盖。渲染用例对实际四种角色采样的断言保留。

## 5. 验收标准对照

| AC | 覆盖任务 | 证据和判据 | 状态 |
|---|---|---|---|
| AC-1 | 3、5–8 | L1 片段与绑定，L2 实际骨骼及状态，L4 追踪、出招、受伤与完整死亡画面。 | ✓ |
| AC-2 | 2、4、6–8 | L1 距离、seed、容量、失败释放；L2 City 启动、紧邻墙体及栏杆负候选、无空间、不可达、重叠、延时和补位。 | ✓ |
| AC-3 | 3、5–8 | L1 决策和冷却，L2 U 形绕障、距离与视线、Move 和 Idle 切换，L4 真实相机中的起手和成功躲闪。 | ✓ |
| AC-4 | 3、5–8 | L1 批次边界、命中身份与唯一性，L2 Ricca 胶囊重叠、墙后、空挥、受伤中断、其他敌人和跨 1 末批次。 | ✓ |
| AC-5 | 3、5、7、8 | L1 HP 上限情形及归零取消；L2 在途普攻取消、敌人停战、场景重置。 | ✓ |

## 6. 回滚方案

工作区无 Git 元数据。任务 1 在复制当前资产时记录每个计划修改文件及现有 `.meta` 的原始 SHA-256，并为所有新增路径及对应 `.meta` 记录「复制时不存在」。原文件备份到 `Logs/EnemyAI/baseline/`。如果执行时新增计划外路径，先对照任务 1 的副本基线补齐预期状态。主工程只安装最终清单的文件。安装前与任务 1 原始基线逐项比对，任何已有文件哈希变化、新路径出现或编辑器状态存在冲突都先解决本轮安装冲突，再继续。安装后核对目标、备份与测试副本。安装中断则只恢复清单中的已有文件，并按同一清单移除新建文件。存档不参与回滚。

## 7. 风险

| 风险 | 影响 | 应对 |
|---|---|---|
| 主工程编辑器在运行 | Unity 同工程批处理冲突 | 主工程保留用户控制，隔离副本批处理。安装前再核对没有正在修改目标资产或试玩。 |
| 烘焙对建筑密区的结果不稳定 | 找不到合法点或模型内产生伪可达点 | 固定种子、引擎条件以及 City 固定负候选复现。必要时补实际障碍或标记，再重烘焙验证。 |
| 播放速度或低帧率造成跨窗 | 一次攻击被漏判或多算 | HoldAttack 和进度区间采样，增加独立真实 Animator 跨 1 测试、连续接触去重和停更超时。 |
| Core 规格引用 gameplay | 归档顺序倒置时规格引用悬空 | 两份 validate 和预览均通过后，先归档 gameplay，再归档 core。第二步有问题则诊断并安全重跑，最后复核双模块索引。 |

## 8. 任务动作与前置检查

- [x] 对应源动画由三份原 Animator Controller 核对。六个 Hit/Death 与同类 Move 的源骨骼绑定路径一致，源均非循环。
- [x] Gameplay 和 Core 的规格增量各通过 C1–C8 及归档预览。
- [x] 已复核现有接口、Animator 以及保存的 City 场景接入点。
- [x] 用户确认实施计划并开始。
- [x] 在独立副本采集当前文件的 Unity 基线并备份。进入安装前复核主工程的编辑状态与所有文件哈希。

## 9. 每批收尾三件事

更新任务状态，记录可回溯的真实测试或截图，把影响后续开发的问题写到 `05-issues.md`。契约文件与各实施批次记录 SHA-256。待 Phase 4 确认后，按知识与规格流程归档。
