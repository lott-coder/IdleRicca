# Gameplay 模块知识

> 模块：gameplay
> 关联层：Domain / Application / Presentation
> 关键类型：PlayerActions、EnemyActions、EnemyPopulation、RiccaPlayer、ThirdPersonCamera、SwordHitbox、WaveRun、PlayerSkillSet、SkillHitbox
> 最后更新：2026-11-22

## 架构概述

Ricca 的设备输入、Unity 相机、Animator 和物理查询可独立验证。游戏状态由 Application 接收命令后写入，Collider 只负责提供命中的候选信息。

## 关键类型

| 类型 | 层 | 职责 | 依赖 |
|---|---|---|---|
| `PlayerActions` | Application | 计算有效攻击批次，按敌人实例结算，消费敌人对 Ricca 的伤害 | Domain |
| `EnemyPopulation`、`EnemyActions` | Application | 用场景名额 ID 管理生成生命周期，用按挥击标识的批次裁定敌人命中 | Domain、Application |
| `RiccaPlayer`、`ThirdPersonCamera` | Presentation | 设备输入、角色物理和镜头 | Unity、Application |
| `SwordHitbox`、`EnemyTarget` | Presentation | 复用场景物理缓存，映射受击体 | Unity、Application |
| `EnemySpawner`、`EnemyActor` | Presentation | 城市导航、占位与 Animator 接线，提供 Ricca 定向盒形接触信号 | Unity、Application |

## 接口与事件

`BeginAttackFrame` 和 `EndAttackFrame` 必须配对。已开启的批次即使没有有效碰撞区间，也要结束以消费动画进度。按敌人实例去重，使多个 Collider 不会多次扣同一份 HP。

生成使用“预留 ID → 实例交接 → 实际销毁释放”的顺序。可见尸体与已失去碰撞体的实例从活跃记录取占位，避免补位时重叠。一次释放只消耗对应 ID。预留后未创建实例由生成器回滚。实例已接管时，由该实例的 OnDestroy 归还名额。

## 数据

- `Assets/Config/Gameplay` 保存普攻、生成和三个敌人的配置资产。启动时将玩法数值复制进场景运行状态。
- 改普攻片段、有效窗口或剑盒几何后，在普攻配置 Inspector 选择 Ricca prefab 重新烘焙轨迹。Editor 检查源片段与 `Attack` 状态的绑定。
- City 静态碰撞、可进入区域或障碍改动后，在 Demo 场景使用 `IdleRicca > Gameplay > Rebake City enemy navigation` 更新同一个导航资产。测试用已知栏杆和花坛作为真实 City 负候选。
- 敌人命中盒由 EnemyActor 的 BoxCollider 和 Combat 配置共同描述，调整动作时同时检查近身距离、盒体前缘和有效窗口。

## 使用约束

- 剑尖在前后两帧间可能走过弧线。跨帧时查询实际动画中间姿态，使受击检测覆盖该弧线。物理回归用例从独立 Animator 样本定位弧线上的目标。
- `EnemyTarget` 注册体积和其生命周期绑定。HP 归零时撤销受击体，模型可继续完成死亡动作。Collider 已被销毁时，解绑仍需识别原 CLR 引用。
- 地面射线、投影后距离、身体净空和完整路径各自负责不同的拒绝条件。生成器每帧限制同步路径检查次数，复用查询缓冲。生成服务本身只处理名额和合法性报告。

## 踩过的坑

| 现象 | 根因 | 正确做法 |
|---|---|---|
| 紧接上一击点击时，第二次挥击直接结算并结束 | Animate Physics 尚未求值，Animator 仍报告上一击的末帧 | 玩家起手从零 Play 后立即 `Update(0)`，再按求值后的进度开始本次批次 |
| Animator 处于 Attack 第 0 帧时片段信息为空 | 状态与本帧权重的采样点不同 | 在 Editor 烘焙时核对源片段和控制器，运行时校验已序列化绑定 |
| 测试在暂停 Animator 上快进后仍读到 0 | 同帧切换 Animator.speed 后立即调用 `Update` 不保证已推进 | 测试显式按归一化进度 `Play` 再求值，并断言实际跨过有效窗口 |
| City EditMode 用 Null 图形设备时地形贴图分配报错 | 地形渲染依赖有效图形设备 | 本项目场景相关的 EditMode 与 PlayMode 均走 D3D11 批处理 |
| 敌人攻势跨过末帧时漏结算或误伤 | Animator 先退出 Attack，或把实际大于 1 的进度截断后再用于角色位移插值 | AI 驱动使用 HoldAttack 消费末批次，再显式回 Idle。以真实进度采样位置，仅裁剪有效窗口 |
| 敌人攻击盒与 Ricca 胶囊重合但接触查询返回 false | 禁用的 BoxCollider 在本工程 Unity 版本没有参与 ComputePenetration 的有效形状 | 使用 Ignore Raycast 层的启用 Trigger 与明确绑定的 CharacterController 查询。由动画窗口和 Application 约束结算 |
| NavMesh 在墙内或断开区域仍能找到候选 | 投影只负责找邻近多边形，CalculatePath 返回 true 也可能是部分路径 | 核对投影前后的地面、距离和身体净空，验证返回状态为 PathComplete 再生成 |

## 卡肉

- 从剑身结算收到有效伤害结果时，分别设置 Ricca 与被命中敌人的 `Animator.speed`。动画播放倍率与两名角色各自的原速相乘。以 `Time.unscaledTimeAsDouble` 计算恢复时间，镜头和其他角色继续按各自时钟更新。
- 活跃时保存减速前的速率。新命中用当前时刻刷新截止点，停用角色时恢复。受击和死亡动画的超时累计使用局部动画速率，避免延长播放时提前进入兜底。
- 参数在 `Assets/Config/Gameplay/RiccaNormalAttack.asset` 的 Inspector 调整。设计参考为 [樱井政博的卡肉案例](https://www.youtube.com/watch?v=5MBxBsF6-vg&t=698s)、[Hitstop Animation System](https://www.maoboulve.com/hitstop-system.html)、[Unity Animator.speed](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Animator-speed.html) 和 [Unity 非缩放计时](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Time-unscaledTimeAsDouble.html)。

## 波次与奖励接线

- 击败数和物理占位来自两个时点。用 `EnemyHealth` 的归零回调推进 `WaveRun`，用真实销毁回调释放 `EnemyPopulation` 的名额。提前移除的活敌只归还配额，已归零的旧实例在换波后仍占名额。用 `WaveSettings.asset` 调参时，可用降低相邻波上限的用例验证生成不会与前一波尸体重叠。
- 奖励面板关闭时，`RiccaPlayer` 会拦截该帧的设备输入，下一帧才恢复捕获光标。焦点回归也经过同一个局内状态检查。换 UI 时用 `WaveHud` 当前的组标识和选项索引提交，保留同帧点击隔离。技能奖励的资格筛选和槽位写入在 WaveRun 的抽取及领取流程内完成。预览和写入共用槽位规则。SkillRewardSelected 在领取提交后通知观察者，避免把它当作授予事务。

## 技能界面和时间

- 技能选择和波次奖励共用 `WaveHud` 的交互 Canvas 与射线检测。被动 `CombatHud` 的血条和连击画面只负责展示。调整层级后要同时复验 EventSystem 真实点击与被动 HUD 的输入测试。面板截图无法证明点击路由已接通。
- HUD 注册成功后角色才接收选技输入。解绑使用角色当前持有的选择编号，保障首帧绘制前停用也能退出。相机解绑、焦点丢失和波次切换同样走退出清理。
- 全局选技减速分别记录 `timeScale`、`fixedDeltaTime` 的进入值和写入值。退出时只还原仍属于本次选择的设置，以免覆盖之后的暂停。普攻的 Animator 卡肉使用独立的非缩放截止时间。


## 源技能动画与移动命中

- 导入源技能时先追踪 Prefab、粒子、材质、Shader、贴图、Mesh 和子控制器的 GUID。把导出工程的运行脚本和伤害组件从表现闭包中排除。合并特效时重设子 Prefab 的出生位置，避免保留导出场景里的世界坐标。剔除无接收端的动画事件时只处理事件段，保留源关键帧字节并核对目标骨架。
- 移动技能的起手盒已在角色身前，单从盒起点检测障碍会漏掉贴身薄墙。先从角色扫过起手盒，再扫描活动窗内的实际轨迹。突击保留完整水平阻挡尺寸，垂直方向只留贴地间隙。命中环节用重叠补起止两端，归一化进度跨窗时按交集结算。
- 配置校验应能隔离单条失效定义。开面板、恢复时间、处理奖励资格和接受施放沿用同一可用性快照。Unity 在扣蓝前核对播放入口，Application 在同一提交中接受动作和消耗；已接受动作被中断时仍作废后续命中批次。

## 相关

- 规格见 `spec/gameplay.md`。
- 迭代历史见 `change/gameplay/`。
