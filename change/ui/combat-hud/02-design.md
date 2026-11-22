# 技术设计文档 — Demo 主界面战斗 HUD

> 模块 ui / combat-hud · v0.0.1  
> 日期 2026-11-22  
> 状态 已确认设计  
> 权威模式 本地单机  
> 在途契约终检对照 gameplay / core 的 2 份在途增量。接口、配置和结构重叠以顺序交接化解，存档契约无冲突。先由 gameplay / core 落地 Ricca 生命、受伤与 Demo 生成器，ui 再基于落地版本接线。进 Phase 4 前复核实际接口、场景和配置。

## ★ 核心重点

### ★1. 架构总览

由 `PlayerActions` 在成功结算敌人伤害时记一次命中，并在 gameplay 的 Ricca 伤害结算入口内清空连击。UGUI 通过 `RiccaPlayer` 读取状态快照。使用场景中的独立 HUD 预制体，让界面和玩法共享同一次伤害结算。

### ★2. 关键类型清单

| 类型 | 层 | 职责 | 依赖 |
|---|---|---|---|
| `HitPoints` | Domain | gameplay 的正伤害处理及 0 HP 下界 | 无 |
| `PlayerActions` / `IPlayerCommands` | Application | 在 gameplay 已落地的生命和伤害机制中加入连击与游戏时间命令 | Domain |
| `PlayerCombatStatus` | Application | 只读值类型快照，携带 HP、连击数与剩余时间 | 无 |
| `RiccaPlayer` | Presentation | 采集游戏时间并为 HUD 提供只读快照 | Application、Unity |
| `CombatHud` | Presentation | 渲染 HP、MP 占位、连击数字和剩余时间，负责数字强调 | Application、UGUI、TMP |

### ★3. 接入点

- 由 gameplay 在 Ricca prefab 配置初始生命 100，HUD 使用 `PlayerActions` 上的 HP 查询。
- gameplay / core 完成 Demo 生成器接线后，在最新的 `Assets/Scenes/Demo.unity` 实例化 `Assets/Prefabs/UI/CombatHud.prefab`，序列化绑定同场景 Ricca。
- `Game.Presentation` 显式引用已安装的 `UnityEngine.UI` 和 `Unity.TextMeshPro`。同步登记《工程与资源》§2 的表现层引擎包依赖，保持项目层间依赖方向。
- HP 和连击随 Demo 场景创建和销毁。存档分区与迁移均不适用。

### ★4. 核心数据流

```text
gameplay 的生命配置 → RiccaPlayer.Awake → PlayerActions
Unity 游戏时间 → AdvanceCombatTime → 处理续连窗口
剑身碰撞 → TryHit → 伤害成功 → 更新连击及末次命中时间
敌方有效攻击 → gameplay 现有扣血入口 → 扣血及断连
PlayerCombatStatus → CombatHud → UGUI 填充、数字与动效
```

### ★5. 实现步骤概要

| 步骤 | 内容 | 依赖 |
|---|---|---|
| 1 | 核对 gameplay 实际落地的生命接口、伤害写入口与 Demo 场景。先写 EditMode 行为用例 | gameplay / core 接线、AC-1／3／4／5 |
| 2 | 实现时间输入、连击和只读快照，接入成功扣血路径 | 步骤 1 |
| 3 | 连接 Ricca、场景和 HUD 预制体，完成 UGUI 布局与状态反馈 | 步骤 2 |
| 4 | 通过真实普攻路径执行 PlayMode 验证，留存截图与动效证据 | 步骤 3 |

## 详细设计

### 1. 架构设计

| 方案 | 分层与数据流 | 性能与验证 | 代价 |
|---|---|---|---|
| A 扩展 `PlayerActions`，推荐 | 在现有有效命中及受伤命令后更新同一场景实例。HUD 每帧读一次 5 字段快照 | 单实例、无事件订阅，能由现有 EditMode 和 PlayMode 路径驱动 | 增加续连字段，并按需暴露已有生命配置的只读最大值 |
| B 独立战斗状态服务 | 新增 Application 服务处理结果事件，玩家驱动和 HUD 分别绑定服务 | 支持多种攻击与状态订阅者，需验证派发顺序及退订 | 为当前单角色增加事件桥接和生命周期 |

采用 A。现有 `TryHit` 已持有有效目标、去重和伤害结果，在结果产生时同步更新可避免漏计或重复派发。

### 2. 数据结构

| 对外契约 | 方向 | 返回或字段 | 语义 |
|---|---|---|---|
| gameplay 的 `PlayerActions.CurrentHp`、`IsAlive`、初始生命 | gameplay → ui | 生命只读值 | 按落地属性读取当前和最大 HP。若最大值未暴露，基于同一构造值补只读 `MaxHp`。 |
| `IPlayerCommands.AdvanceCombatTime(double now)` | Presentation → Application | `void` | 输入非负、有限、非倒退的游戏秒数。该命令裁定窗口到期。 |
| gameplay 的 `IEnemyCommands.TryHitRicca` 及内部扣血方法 | 敌人命令 → Application | 既有命中结果 | 复用该路径；在已经扣除正数 HP 的分支清空连击，拒绝结算的请求不会清空。 |
| `PlayerActions.CombatStatus` | Application → Presentation | `PlayerCombatStatus` | `CurrentHp`、`MaxHp`、`ComboCount`、`ComboSecondsRemaining`、`HitSequence`。最近一次计入的命中会推进序号。 |
| `RiccaPlayer.TryGetCombatStatus(out PlayerCombatStatus status)` | HUD → 玩家桥接 | `bool` | 已完成初始化且角色启用时返回有效快照。 |

### 3. 交互流程

**战斗。** 在 `RiccaPlayer.Update` 读取 `Time.timeAsDouble` 并提交给 Application，先推进时钟再处理本帧输入，缩放到零时计时冻结。现有 `LateUpdate` 按剑身碰撞结算。结果为 `Damaged` 或 `Defeated` 时计数并刷新最近命中时刻。多个已结算目标同帧逐个计数。

**受击。** gameplay 的扣血入口在同一结算中扣除 HP，并清空连击计数与剩余时间。受伤与有效命中同帧时按实际提交顺序结算。归零行为沿用 gameplay 已确认的契约，包括停止移动与普攻、取消在途攻击以及保留镜头。HUD 显示 0／最大生命并隐藏连击。

**绑定。** Unity 在场景内完成 Ricca 初始化，HUD 用显式序列化引用查询就绪状态。根对象或 Ricca 被禁用时隐藏 HUD 的数据区域，再启用就根据最新快照恢复。Ricca 初始化失败时由 gameplay 输出诊断，HUD 隐藏数据区域。缺少 HUD 必需的序列化引用时由 HUD 输出一次诊断并停用。L2 覆盖启动、临时禁用、恢复和无效绑定，无协程或无限等待。

**显示。** Canvas 为 Screen Space Overlay，`CanvasScaler` 以 1920×1080 为参考。左上状态区保留可读边距，HP 使用醒目的暖红配深灰底，MP 使用亮度较低的蓝色窄条。右侧靠上显示大号连击数、短标签和细金色倒计时条。第 2 击激活该组。每次命中使用约 0.18 秒的缩放强调，半径和位置变化限制在组件边界内。计时到零或受伤时立即隐藏。采用原创的框线与字阶，场景中使用装饰透明度控制与信息对比。

### 4. 性能与防御性设计

- HUD 的 `LateUpdate` 排在 `RiccaPlayer` 及 gameplay 实际伤害生产端之后，接线时复核脚本执行顺序并用 PlayMode 验证同帧读取。数字使用 `TextMeshProUGUI.SetCharArray` 和预分配缓冲区，只在值变化时更新。倒计时条只在显示时更新。
- HUD 图形的 `raycastTarget` 全部关闭。Canvas 不挂输入射线器。预制体在场景加载时实例化一次。
- 生命周期采用同步就绪查询，角色引用不可用或状态未就绪时隐藏。生命配置由 gameplay 初始化和校验。
- 游戏时间取高精度绝对秒数，比较最近命中与当前时间的差。拒绝 NaN、无穷和倒退时间。连击计数触顶时饱和，仍重开续连窗口。

### 5. 架构约束遵守声明

- [x] Domain 复用纯计算。HP、连击和到期只由 Application 裁定。
- [x] Presentation 中的玩家桥接只采样和转发，HUD 只读快照。
- [x] 本地权威， 无存档或跨场景引用。
- [x] 初始值、伤害和时钟均已定义输入防护。
- [x] 使用同步查询，无订阅、异步等待或每帧字符串拼接。
- [x] 已定义 L1、L2、L4 回归证据。

### 6. 验证策略

| 用例 | 级别 | 覆盖 | 构造方式 |
|---|---|---|---|
| HP 初始值、真实伤害、无效伤害和过量扣除 | L1、L2 | AC-1、AC-4 | L1 构造敌方有效攻击批次，沿 gameplay 的 `IEnemyCommands.TryHitRicca` 结算。L2 在可控近战位置验证真实攻击与 HUD。 |
| 精确 5 秒、受伤后重启、空挥和重复接触 | L1 | AC-3、AC-4、AC-5 | 手动推进绝对时间，调用现有有效批次和敌人伤害路径 |
| 场景绑定及真实普攻 → HUD | L2 | AC-1、AC-2、AC-4、AC-5、AC-6、AC-7 | 加载 Demo 验证绑定。组合输入用固定目标姿态驱动实际 Animator 和剑身物理，排除随机寻路扰动。断言资源条、数字、剩余条及重载。 |
| 场景截图和倒计时动态 | L4 | AC-2、AC-6、AC-7 | 两种分辨率截取默认和连击画面，记录计时缩短、再命中时恢复并强调、到期后隐藏的过程 |
| HUD 生命周期及预算 | L2、L4 | AC-6、AC-7 | 禁用并恢复 Ricca 或 HUD，检查显示重新同步。无效绑定只记一次诊断。Profiler 检查待机、倒计时可见和连击强调期间。 |

### 7. 风险评估

| 风险 | 影响 | 应对 |
|---|---|---|
| 依赖 gameplay 和 core 的在途迭代 | 共同修改生命入口、Ricca 和 Demo 的顺序会影响最终资产 | 对照 gameplay 设计的命中、受伤与归零契约，以及 core R1 场景增量。待共同文件在主工程落地后核验接口再接入 HUD。 |
| HUD 在本帧伤害处理前读取 | 受击或连击显示落后一帧 | 显式晚于玩家执行，L2 测试验证快照与画面同步 |
| 原工作区已有打开的 Unity Editor | 同目录启动批处理会争用 | 使用隔离验证副本完成 Editor 操作和门禁，核对源资产并安装已验证文件 |
| HP 与 MP 整体压过城市场景 | 影响余光读取和战斗焦点 | 暖红 HP 主层级，MP 降低亮度，右侧提示按需出现并检查 L4 截图 |
