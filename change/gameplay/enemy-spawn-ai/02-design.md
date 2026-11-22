# 技术设计文档：敌人生成与 AI

> 模块：gameplay ｜ 子分类：enemy-spawn-ai ｜ 版本：v0.0.1  
> 日期：2026-11-22 ｜ 状态：已独立复审并获用户确认  
> 权威模式：本地 Application  
> 在途契约终检：0 冲突（独立在途模块 0 个，本轮配套 core delta 1 份）2026-11-22

## ★ 核心重点

### ★1. 架构总览

用 City 现有物理几何离线烘焙 NavMesh。每个生成位置都重新验证地面、占位、距离及完整路径。敌人使用 NavMeshAgent 绕障，战斗状态、名额和 HP 由 Application 维护。攻击盒只查询本场景绑定的 Ricca，使用动画进度裁定生效区间。

### ★2. 关键类型清单

| 类型 | 层 | 职责 | 依赖 |
|---|---|---|---|
| `HitPoints` | Domain | 复用 HP 下界与伤害计算 | 现有类型 |
| `EnemyPopulation` | Application | 管理同一场景的名额、首轮种类和合法候选的占用 | 纯 C#，可注入随机种子 |
| `EnemyActions` | Application | 管理敌人休整、追踪、攻击、受伤、死亡和攻击结算 | `EnemyHealth`、`PlayerActions` |
| `PlayerActions` | Application | 增加 Ricca 的生命查询与限定伤害入口，复用现有普攻取消规则 | `HitPoints` |
| `EnemySpawner` | Presentation | 持有场景生成器与 NavMeshData 注册，查找候选并生成实例 | Unity Physics、NavMesh、`EnemyPopulation` |
| `EnemyActor` | Presentation | 调用敌人指令，驱动 NavMeshAgent 和 Animator，发出盒形接触报告 | Unity、`EnemyTarget`、`RiccaPlayer` |
| `EnemyTarget` | Presentation | 保留 Ricca 剑身的受击体映射，将已结算的命中反馈交给敌人表现 | Unity、`EnemyHealth` |
| `EnemyCombatSettings`、`EnemySpawnSettings` | Unity 配置 | Inspector 提供战斗与生成数值；启动时校验快照 | ScriptableObject |

### ★3. 接入点

- 程序集保持 Domain → 无依赖、Application → Domain、Presentation → Application/Domain。使用现有 `com.unity.modules.ai`。Editor 烘焙工具留在 `Assets/Editor/Gameplay/`。
- 在 Demo 放置一个序列化绑定 Ricca、三种 prefab、生成配置和烘焙资源的 `EnemySpawner`。取消原固定位置的三个敌人场景实例。
- 敌人生命配置及受击层保留。每种 prefab 增加 AI、攻击盒和对应战斗配置。Ricca 现有动作与胶囊继续使用，新增默认 100 HP 配置值。
- 不新增存档分区，重载场景重建会话。未改动 Ricca 的普攻伤害和已有攻击资产。

### ★4. 核心数据流

1. 编辑器收集 Demo 静态可碰撞几何并烘焙 `CityEnemyNavigation.asset`。运行时由场景组件注册一次，在场景退出时注销。
2. 生成器将候选的落点距离、身体净空及完整路径结果提交 Application。成功预留后才实例化，实例移除后释放同一名额。
3. 敌人观察目标距离、可见性和路径，向 Application 提交观测。读回状态，驱动运动与动画。
4. 敌人攻击动画进度进入生效区间时检查盒体与本次绑定的 Ricca 胶囊。Application 消费本挥唯一命中并扣血。Ricca 剑身继续走现有攻击批次，再用结算结果触发 Hit 或 Death。
5. 死亡时立即撤回行为及受击体，保留死亡动画，按照实例实际移除释放名额。

### ★5. 实现步骤概要

| 步骤 | 内容 | 依赖 |
|---|---|---|
| 1 | 先写名额、行为转换、命中去重、Ricca 生命与取消的 EditMode 测试，再实现 Application 与必要的 Domain 复用。 | 玩法需求 |
| 2 | 接入场景生成、导航、敌人动画与 Ricca 定向攻击盒。 | 1 |
| 3 | 在独立 Unity 副本完成动作导入、NavMesh 烘焙和配置接线。 | 2 |
| 4 | 全量 EditMode、PlayMode、资源门禁及截图。 | 3 |

## 详细设计

### 1. 架构设计

#### 1.1 模块定位

gameplay 管理战斗会话，core 负责 Demo 构建入口规格和角色预制体可用性。City 环境资产仍提供碰撞输入。单实例 `EnemySpawner` 是 Demo 的唯一自动创建敌人入口。场景接线测试断言只有一个；重复启用则该场景的重复生成器报错停用，已有生成器和名额继续有效。

#### 1.2 备选方案

| 方案 | 取舍 |
|---|---|
| A 编辑器烘焙 NavMesh（推荐） | 复用 Unity AI、当前城市物理结构及导航资源。离线成本可测，启动费用限于加载数据和有上限的候选检查。城市碰撞修改后通过 Editor 菜单重烘焙。 |
| B 运行时收集几何并构建 NavMesh | Application 与碰撞契约相同，运行时可以适配变化的建筑。当前 City 有 1,911 个 MeshCollider 与 BoxCollider，运行时构建增加首帧等待、失败和资源生命周期处理。 |

#### 1.3 依赖方向

纯 C# 层只接受数值、布尔观测及战斗句柄。Unity 引用、世界坐标、Animator state 和物理查询留在 Presentation。Editor 使用公开配置和 UnityEditor API 生产数据资产，无运行时 Editor 依赖。

### 2. 数据结构

#### 2.1 初始配置

生成距离在 XZ 平面按面积均匀取样，默认 7–14 米。默认上限 3。开场按三种 prefab 各申请一只，受配置上限约束，缺位在后续轮次优先尝试，补位间隔默认 1.5 秒。每个生成轮次最多 12 个候选，每帧最多发起 2 个同步完整路径检查。查询无合法结果时，等待下一轮。种类与位置使用本场景随机序列，测试可指定种子。

| 类型 | 移动速度（米/秒） | 起手距离（米） | 伤害（HP） | 攻击有效区间（归一化） | 收招后冷却（秒） |
|---|---:|---:|---:|---|---:|
| 哥布林 | 1.90 | 1.25 | 8 | 0.36–0.48 | 0.85 |
| 骷髅 | 1.65 | 1.40 | 10 | 0.47–0.62 | 1.00 |
| 地狱犬 | 3.20 | 1.25 | 9 | 0.32–0.48 | 0.90 |

数值为初始配置，Phase 4 以原片段的实际骨骼姿态校准攻击盒中心、大小及窗口。起手距离须小于等于盒体对 Ricca 胶囊的有效前缘。攻击盒底面、顶部及宽度匹配每种敌人动作。最大寻路频率每敌 4 Hz，目标移动或路径失效时更新，初次追踪即发一次请求。

#### 2.2 接口与状态

接口签名为计划采用的公开契约，Unity 命令入口与生命写方法依旧分开。参数以配置快照和当前会话为准。

| 名称 | 参数与返回 | 方向及责任 |
|---|---|---|
| `IEnemySpawnCommands.TryReserve` | 种类、实际生成距离平方、是否有地面、是否无占位、是否有完整路径 → 名额 ID | Presentation → Application。校验候选、距离、容量，原子占一个名额。 |
| `IEnemySpawnCommands.Release` | 名额 ID → 是否释放 | Presentation → Application。消耗对应名额，未知或已消费 ID 无效。 |
| `IEnemyCommands.Step` | 当前目标有效性、水平距离平方、高差、是否无遮挡、是否可达、有限的 deltaTime → `EnemyPhase` | Presentation → Application。根据当前冷却、生命和状态裁定待机、追踪或起手。 |
| `IEnemyCommands.ObserveHit` | 无参数 → `EnemyPhase` | Presentation → Application。仅消费 `EnemyHealth` 最近的尚未观察到的实际命中版本，作废攻击并决定 Hit 或 Death。 |
| `IEnemyCommands.BeginAttackFrame`、`EndAttackFrame` | 当前 Animator 是否处于 Attack、进度、deltaTime → 敌人攻击批次；结束当前批次 | Presentation → Application。有效窗口取相邻进度区间的交集，同一批次只处理一次。 |
| `IEnemyCommands.TryHitRicca` | 当前攻击批次、创建时绑定的 `PlayerActions` 实例 → `AttackHit` | Presentation → Application。校验实例身份、攻击批次、HP、同挥唯一命中。内部调用 Ricca 的扣血入口。 |
| `IEnemyCommands.ObserveAnimation` | 预期的 Hit 或 Death state、归一化进度、deltaTime → 当前阶段或退役 | Presentation → Application。死亡仅在播完或 Animator 故障超时后退役。 |
| `PlayerActions.CurrentHp`、`IsAlive` | int、bool | Application → Presentation。生命状态只读。 |

`EnemyHealth` 现有公开构造、HP 查询和 `ReceiveDamage` 的内部联系保留，增加单调的命中版本。新应用规则以版本去重，致死转换为终态，所有攻击令牌作废。已有 PlayerActions 增加默认 100 HP 的尾部可选参数以保持旧调用；RiccaPlayer 从序列化初始 HP 构造它。修改接口前在计划中核对现有测试替身和入参。

#### 2.3 存档影响

HP、生成统计和敌人 AI 全部限于 Demo 场景。无 SaveData 字段、迁移和加载次序变化。保留已引用 prefab、controller 和现有片段 GUID。

### 3. 交互流程

#### 3.1 启动、生成和寻路

- 由 Editor 从 Demo 的静态物理几何收集 bake sources，排除 trigger、角色和带 Rigidbody 的动态物体。选择覆盖 City 可玩区域的烘焙范围，以现有 agentType 0 生成数据。斜率、半径、台阶和网格垂直范围通过 City 起点及道路通行测试验证。资产在启动时通过 `NavMesh.AddNavMeshData` 注册，场景卸载时仅移除自己的实例。 [Unity NavMesh 烘焙 API](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AI.NavMeshBuilder.BuildNavMeshData.html)
- 我方角色在 Awake 从序列化配置创建状态。Spawner 在所有 Awake 后校验 Ricca、三个 prefab 与导航资源，成立即 Ready。初始化没有无界等待，配置错误一次显性报错并停用对应生成入口。
- 对 Ricca 附近和位置候选先做地面投影，再做半径有限的 NavMesh 投影。限制对地面射线落点的高差并复核投影后的平面距离，拒绝跨楼层吸附。使用各 prefab 的实际受击体包络检查其预估朝向下的占位，地面接触容差与占位上边界分开。地形及建筑阻挡用实心 Collider，已存在的敌人占位包含 EnemyHurtbox trigger，死亡中实例的包络由活动名额的几何记录补充。检查目标和候选 (NavMeshQueryFilter) 一致且路径状态为 `PathComplete`；API 返回 true 仍可能给出部分路径。 [Unity NavMesh 投影 API](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AI.NavMesh.SamplePosition.html) [Unity 完整路径检查](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AI.NavMesh.CalculatePath.html)
- 生成事务顺序为预检 prefab → 预留名额 → 实例化 → 交接名额 → 绑定 Agent、Ricca。预检拒绝 inactive、组件禁用或缺引用的 prefab。Instantiate 抛错且没有实例时，Spawner 立即按 ID 回滚。已有实例但交接失败时，Spawner 记录并销毁该实例，等 Unity 确认实例不存在后释放，期间继续占名额。交接后只有该 actor 的 OnDestroy 按 ID 释放。未能绑定 Agent 或场景中 actor 意外禁用时请求销毁，不提前归还。场景卸载先停用生成，再清理实例和导航句柄。无有效目标时停止生成及寻路。
- 追踪使用 Unity 寻路和代理局部避让。近距离仍检查高差和两者胸口之间的场景遮挡。能出招时锁定水平朝向、停导航。攻击期间目标离开并不能修改敌人的挥击朝向。攻击完全结束先回 Idle，再根据距离、视线和冷却决定追踪或起手。

#### 3.2 攻击、受击和死亡

- 三种 Animator 保留原 `Moving`、`Attack` 输入和已有 Idle/Move/Attack，新增 Hit、Death 与默认 false 的 `HoldAttack`。原 Attack → Idle 自动过渡增加 `HoldAttack=false` 条件，独立 Animator 的 `Attack` 触发仍按原行为回待机。AI 起手设置 HoldAttack，从归一化 0 `Play` 并 `Update(0)`，锁定导航和朝向。中间帧即使一次推进到 >=1 也先按当前 Attack 消费最终批次。处理完最后批次后 Application 启动冷却，表现清除 HoldAttack 和 Moving，再显式回 Idle。任何取消、禁用、受击和死亡路径同步清除 HoldAttack 并作废批次。禁用 root motion，Death 优先于 Hit 和 Attack。
- 攻击使用角色前方带 Inspector Gizmo 的 BoxCollider 作为几何。它与唯一绑定的 Ricca 胶囊做接触查询，不遍历敌人，也不走刚体推挤。先在隔离 Unity 校验 `Physics.ComputePenetration` 对该 prefab 的 BoxCollider 与 CharacterController 支持，若 CharacterController 查询不稳定，采用同尺寸的专用 CapsuleCollider 并增加等价尺寸检测。用前后帧 Ricca 位姿在当前生效区间做有上限的采样，跨过整个窗口时也处理其中的姿态。每次盒形接触同样检查该姿态下到 Ricca 的静态遮挡。 [Unity 指定位姿的 Collider 接触查询](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Physics.ComputePenetration.html)
- 时间与状态由 Application 守卫。错误状态、倒退进度、非有限数、无效或重复令牌不产生结算。攻击等待动画进入的上限为 0.5 秒。Attack 进入后一旦停更或卡在动画状态，按配置片长及速度计算的时长加 0.5 秒作为绝对故障上限，超时取消并开始冷却。若本帧已读到有效的正常完成，先消费末批次再评估超时。Hit/Death 使用同类有界故障处理。配置或 Animator 丢失时显性报错。正常死亡一定完整播放且整个生命周期计入上限。
- `EnemyTarget.ShowHitResult` 在现有剑身结算后让 `EnemyActor` 同步已发生的命中；致死顺序是停止攻击及代理 → 撤销受击体注册并关 Collider → 播 Death → 销毁。Ricca 归零时 `PlayerActions` 原子取消已开始普攻、不接收移动或攻击，保留第三人称镜头。后续敌人结算看到 `IsAlive=false` 就失效。
- 复用现有移动与攻击片段。在 `D:/Learning/Unity/_ricca/work/project/Assets/AnimationClip` 导入哥布林 `damage_7`/`death_7`、骷髅 `skeleton_damage`/`skeleton_death`、地狱犬 `hellhound_damage`/`hellhound_death`。这六段来自对应的原 Animator Controller。保留骨骼绑定，移除依赖源工程脚本的动画事件和不必要的对象曲线，检查非循环。验证每个 state 实际驱动蒙皮骨骼。

### 4. 性能与防御性设计

- 常驻对象按配置上限预分配引用和 ID 存储。复用 NavMeshPath、查询缓冲和场景引用。每帧更新没有查找组件、LINQ 或动态字符串。生成和销毁是低频生命周期分配，单独计入采样，首版省略对象池。
- 用关联系统内部状态验证同一 slot 和 attack batch 只能消费一次。ApplyDamage 只接受正整数和存活目标，位置、距离、配置、进度及时间均先校验有限与范围。组件禁用、销毁、重载、路径无效及 Animator 停更各有明确退出路径。
- 供 Physics 判断的胶囊和盒几何与 HUD 无关。运行帧报无效配置后停用组件，以免每帧异常和日志分配。

### 5. 架构约束遵守声明

- [x] 类型落在正确程序集，层依赖保持单向。
- [x] Application 是 HP、生成容量、状态机和命中写入口。
- [x] 权威位置与项目本地单机声明一致。
- [x] 配置及观测值校验有限与范围。
- [x] 生命周期回调与 NavMesh 注册按持有者配对。
- [x] 存档 schema 和旧会话序列化保持兼容。
- [x] 就绪无无界等待，动画等待有时限。
- [x] 稳态热路径设计为 0 B GC，使用 Profiler 验证。
- [x] Phase 4 有 L0–L4 证据路径。

### 6. 验证策略

| 用例 | 级别 | 覆盖的 AC | 构造方式 |
|---|---|---|---|
| 生死、打断及窗口 | L1、L2 | AC-1、3、4 | L1 覆盖空窗、跨窗、重复批次、冷却、受击抢占、死亡终态、坏时钟。L2 对真实敌人 Animator 在有效窗前采一帧、下一次直接推进到归一化进度 >1，验证窗口仍扣伤一次且先返回 Idle。暂停 Animator 推进以验证超时及清 HoldAttack。 |
| 上限与安全报告 | L1 | AC-2 | 配置 1、3 和两种随机种子。距离端点、路径不通、候选阻挡、名额不释放、重复释放。 |
| 场景和骨骼 | L1、L2、L4 | AC-1、2 | 从真实三 prefab 检查资源和无断引用，采样骨骼姿态。载入默认 Demo，按帧记录初次生成、死亡终态与随机补位。 |
| 真实导航与地形 | L2、L4 | AC-2、3 | 使用可控 U 形阻挡场景验证不可达/重叠/模型中的候选失败、追踪绕障、回 Idle 及冷却。真实 City 还用已知建筑墙体、门缝或栏杆的固定负候选复核，占位和完整路径均走生产方法。发现视觉模型与物理缺口时补 bake blocker 或相应碰撞，再复跑点位与绕行断言。 |
| 敌人攻击盒和 Ricca 归零 | L1、L2 | AC-4、5 | 构造 Ricca 与其它敌人同时覆盖盒体，再测试躲闪、偏移、墙隔断、受击打断、Ricca 归零后在途攻击取消、重载重置。 |
| 回归和性能 | L0–L4 | AC-1 至 AC-5 | 保留原渲染、镜头、资产及 Ricca 剑身断言。把测试里依赖旧场景静态敌人和立即隐藏的部分改成新需求对应的场景驱动与断言。运行适用 G1–G8，记录待机、追踪及攻击的 GC 和帧时间、关键动作截图。 |

### 7. 风险评估

| 风险 | 影响 | 应对 |
|---|---|---|
| City 模型与物理结构不一致 | NavMesh 允许穿入实际模型区域 | 用场景碰撞和实际模型截图复核代表性建筑，对发现的缺口补 bake blocker，再测路径与占位。 |
| 原导入动画未驱动当前骨骼 | 进入状态但看不见动作 | 绑定检查及采样实际蒙皮骨骼，动作验收同时查位姿和截图。 |
| 原测试依赖固定三只位置或立即隐藏 | 旧前提与新玩法冲突 | 以独立 fixture 固定需要的敌人姿态，保留几何及 HP 断言。另测启动随机位置、死亡收尾及补位。 |
| 编辑器已打开主工程，当前工作区没有 Git 元数据 | 验证争用或无法以提交冻结契约 | 在独立副本验证，通过 SHA-256 对照及安装前备份保护主工程。本阶段用已校验的增量文件哈希留痕。 |

归档前先校验和预览 gameplay、core 的两份 delta。core 只更新场景默认阵容契约，常规 Attack 结束回 Idle 的 R2 行为继续由 Animator 保证。两份增量都通过后，先通过工具归档 gameplay R7，再归档引用它的 core R1。若第二次失败，保留第一项已完成的归档，诊断、校验后幂等重跑 core。最后复核两份规格、全局索引和模块索引。
