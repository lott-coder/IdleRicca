# 实施计划 — Ricca 技能系统

> 模块 gameplay ｜ 子分类 ricca-skills ｜ 版本 v0.0.1  
> 日期 2026-11-22 ｜ 状态 已验证、安装并归档  
> 关联 [需求](01-requirements.md)、[设计](02-design.md)、[规格增量](07-spec-delta.md)。

## 1. 任务清单

| # | 任务与输出路径 | 层 | 证据 | 资产 | 依赖 | 状态 |
|---|---|---|---|---|---|---|
| 1 | 在 `Logs/RiccaSkillsVerification/project/` 复制当前 Unity 输入，建立 `baseline/sources.json` 与基线输出。复用同版本 Unity 和 D3D11，按验证规范确认批处理独占。 | 验证环境 | L0、L1、L2 基线 | 只读复制 | 开工确认 | ✓ |
| 2 | 先在 `Assets/Tests/EditMode/PlayerSkillTests.cs`、`Assets/Tests/EditMode/SkillWaveRewardTests.cs` 写授予、升级、过滤、伤害、选择与批次用例。新增 Domain `Assets/Scripts/Domain/Gameplay/SkillId.cs` 与 `Assets/Scripts/Domain/Gameplay/SkillDefinition.cs`。在任务 3 列出的最终接口和状态文件中先写可编译的最小桩，运行新用例并核对失败原因。 | 测试、Domain | L0、L1 红 | 新脚本和 meta | 1 | ✓ |
| 3 | 在 `Assets/Scripts/Application/Gameplay/PlayerSkillSet.cs` 管四槽与等级，在同目录以 `SkillStatus.cs`、`SkillCast.cs`、`SkillRewardPreview.cs` 分别定义公开值类型，在 `IPlayerSkillCommands.cs` 定义命令。改同目录 `PlayerActions.cs`、`WaveRun.cs`、`WaveRewardPool.cs`，完成互斥、计时、批次、候选资格与提交。新用例先转绿，再跑既有规则用例。 | Application | L1 | 无运行资产改动 | 2 | ✓ |
| 4 | 改 `Assets/Scripts/Presentation/Gameplay/RiccaPlayer.cs`、`EnemySpawner.cs`、`EnemyTarget.cs`、`EnemyActor.cs`，并新增 `RiccaSkillSettings.cs`、`SkillHitbox.cs`。接入 F、Esc、游戏速率、动作退出、盒体、特效和受击反馈。保留旧构造、输入路径和波次事件。 | Presentation | L0、L2 | A1、A2、A3 | 3 | ✓ |
| 5 | 新增 `Assets/Scripts/Presentation/UI/SkillSelectionHud.cs` 并改 `WaveHud.cs`。在隔离工程用 Unity Editor API 制作动画、配置、特效、状态和 UI。维护原有 Canvas 与 EventSystem、序列化字段和 GUID。 | Presentation、资源 | L0、L2、L4 | A1–A5 | 4 | ✓ |
| 6 | 新增 `Assets/Tests/PlayMode/RiccaSkillGameplayTests.cs` 和 `Assets/Tests/EditMode/RiccaSkillAssetTests.cs`。PlayMode 测试程序集新增 Domain 直接引用，将现有 WaveRunGameplayTests 的技能占位断言更新为实际等级，并将 DemoAssetTests 控制器的精确集合扩展到原六段和四段技能。在 Demo 验证 AC-1–AC-5，完整跑全量 EditMode、PlayMode、资产和编译门禁。截图与性能样本进 `Logs/RiccaSkillsVerification/`，登记 `05-issues.md`。 | 测试、验证 | L0–L4 | A1–A5 | 5 | ✓ |
| 7 | 发起独立代码和 UI 审核，记录到 `04-review-report.md`。按确认结果修订并重新运行相关门禁。确认主编辑器已释放受影响脏场景与 Prefab 编辑资源后，在主工程仅安装清单文件及 meta。比较当前源哈希并备份，安装后比对隔离验证哈希。 | 审核、安装 | G1–G8，哈希 | A1–A5 | 6、代码审核确认 | ✓ |
| 8 | 用 `knowledge-set` 预览并记录 `06-knowledge-update.md`。完成真人试玩或登记缺项，按流程确认知识写入和归档。校验增量后用 `spec_tool.py archive` 应用。 | 交付 | C1–C8 与验证证据 | 归档工具 | 7、知识与归档确认 | ✓ |

## 2. 资产修改清单

| # | 类型 | 资源路径 | 变更与证据 |
|---|---|---|---|
| A1 | 配置 | `Assets/Config/Gameplay/RiccaSkills.asset` | 四个唯一 ID、三级伤害、生效窗、盒尺寸、20% 默认减速、动画和特效引用。EditMode 读取并校验。 |
| A2 | 动画 | `Assets/Art/Demo/Ricca/Skills/{QuickCut,Thrust,Sweep,GroundBurst}.anim`、`Assets/Art/Demo/Controllers/Ricca.controller` | 四段新创作的非循环占位动作，为当前骨架录制各自动作关键帧。现有 Idle、移动、Attack、Hit、Death 的引用保留。以真实 Animator 求值验证。 |
| A3 | 预制体和特效 | `Assets/Prefabs/Demo/Ricca.prefab`、`Assets/Prefabs/Demo/Skills/{SweepFx,GroundBurstFx}.prefab` | 引用技能配置与盒查询，在相应技能上使用原创粒子。验证非空和取消清理。所需材质只在该路径下增补。 |
| A4 | 技能 UI | `Assets/Prefabs/UI/SkillSelectionHud.prefab` | 四个现成按钮、空禁用态、等级和范围提示。绑定现有 UGUI，按场景事件或快照刷新。留两档分辨率截图。 |
| A5 | 场景 | `Assets/Scenes/Demo.unity` | 接入技能面板与 Ricca，继续共用当前 Canvas 层级与一个 EventSystem。资产测试查引用、场景加载、重复 EventSystem 和 Missing Script。 |

新建资源的 Unity `.meta` 随资源安装。制作资源使用隔离项目中的一次性 Editor 脚本 `Logs/RiccaSkillsVerification/project/Assets/Editor/Gameplay/BuildRiccaSkillAssets.cs`。主工程的资源和控制器用 Unity 生成格式保存。

## 3. 接口、依赖与回退

- `TryOpenSkillSelection()`、`CancelSkillSelection(ulong selectionId)`、`TryCastSkill(ulong selectionId, int slot, double now, out SkillCast cast)`、`CancelSkillCast(ulong castId)` 返回 bool。最后一项供停用、Animator 失效或无效速率使用，只接受当前施放 ID，接受后立即作废批次。
- `BeginSkillFrame(ulong castId, bool inSkill, float normalizedTime, double now)` 返回 `AttackSampleBatch`，`TryHitSkill(AttackSampleBatch batch, EnemyHealth target)` 返回 `AttackHit`，`EndSkillFrame(AttackSampleBatch batch)` 返回是否完成。复用现有普攻批次计算，但区分动作类型和有效批次所有者。
- `WaveRun.TryGetSkillRewardPreview(ulong offerId, int index, out SkillRewardPreview preview)` 返回 bool。波次抽取使用 `WaveRewardPool.Draw(WaveReward[] destination, bool includeSkill)`。现有 Draw、TryChooseReward 和 SkillRewardSelected 的签名继续可用。
- 开工确认 → 隔离基线 → L1 用例 → 本地规则 → 场景桥接和资源 → L2、L4 → 独立审核 → 哈希对照安装 → 知识和归档。
- 当前主工程没有 Git 工作树，设计与增量已落盘。安装前按工作区原路径备份既有目标文件及 meta。失败时先比对当前哈希，回退本轮已装文件。已被其他会话继续编辑的文件先人工复核差异，只撤回本轮改动。

## 4. 验证计划

| 用例 | 构造与证据 |
|---|---|
| L1 奖励与等级 | 锁定随机种子和配置顺序，验证四种卡授予、同级左槽、不同卡片等级、满级后移除技能候选、无配置旧路径、失败领取保持槽位与波次、第 10 波技能奖励仅结算和通知一次并进入通关。 |
| L1 动作状态 | 提交空槽、失效 ID、重复选择、输入互斥、无效或倒退时钟、非法批次、受击与波次退出后的旧报告。按窗口内外对照，跨进度末帧仍只结算一次。 |
| L2 实际场景 | 使用 Ricca 的输入替身和真实 UGUI 按钮，使用相同设备命令入口。建立等级前后对照、多个敌人及重复 Collider，用真实 WaveRun 验证归零与推进。对比技能和普通攻击，确认技能触发受伤死亡处理，普通攻击的连击数与局部卡肉依据原命中规则。 |
| L2 时间和装配 | 在默认和非默认起始速率覆盖生效、取消、伤害、死亡、失焦、暂停、领奖、禁用、卸载。核对 Time.timeScale、fixedDeltaTime、动作状态、全局无残留、F 可再用。 |
| L4 图像 | 1280×720 与 1920×1080 捕获空槽、可用四槽、两个带 VFX 的技能。记录暂停前后姿态，确认提示、点击区域、遮挡、按钮状态以及实际表现。按 U1–U10 逐项核对。 |
| L3、L4 节奏和分配 | 正常入口记录首次获得和施放的波数。隔离场景稳定后采集非刷怪、非打开面板的持续战斗帧，按验证门禁登记观感和 GC 数据。至少采样连续 120 个稳定帧，目标为新技能更新路径 0 B/帧。单列 Editor 与测试外壳分配，记录任何排除项和无法验证的播放器环境差异。 |

全量编辑和播放测试使用项目指定 Unity 版本与 D3D11。编译日志、XML、截图和资产扫描证据写入 `Logs/RiccaSkillsVerification/`。用 实际 UI复现验收场景。回归出现时保留已有测试的断言。

## 5. 验收标准对照

| AC | 任务 | 证据 | 状态 |
|---|---|---|---|
| AC-1 授予、升级与重开 | 2、3、6 | 等同命中条件比较真实 HP 差，旧场景抽取与新场景满级筛选均正常 | ✓ |
| AC-2 开启、取消与输入隔离 | 2、4、5、6 | 四槽两态、F 和 Esc、同帧输入、原速恢复、鼠标 | ✓ |
| AC-3 动画与特效 | 4–6 | 四个动作有效，两个特效按配置出现，正常完成及取消 | ✓ |
| AC-4 碰撞与波次击败 | 2–4、6 | 盒内、盒外、窗口跨越、去重、双目标、归零与计数 | ✓ |
| AC-5 生命周期 | 2–6 | 端到端时间还原、阶段、受击、失败和焦点 | ✓ |

## 6. 回滚方案

隔离验证不写主工程运行资产。发布前先复制所有已存在的待安装文件，保持相对目录并记录 SHA-256。安装前核对清单目标的所有权和输入哈希。若主编辑器的 Demo 或待改 Prefab 仍有未保存更改，或无法确认其已释放编辑权，等待用户保存并离开对应编辑状态。已有磁盘变化则重读差异，必要时重接后重跑用例。部分安装失败时在校对哈希后恢复原件，将本轮新增资源移出。保留验证日志和恢复副本。

## 7. 风险

| 风险 | 处置 |
|---|---|
| 现有 Unity 编辑器打开 | 批处理只打开隔离目录。主工程安装要确认相应场景和 Prefab 已释放编辑权，再比较源哈希。无法从可见状态确认时，停在安装前，不接管当前编辑器。 |
| 最后一帧既施放伤害又换波 | 先提交 Ricca 批次，再推进敌人及波次，场景测试覆盖两者优先级。 |
| 无法用普通随机序列一次拿满技能 | 固定随机源回归四槽与升满。普通试玩另记首次获得和施放时点。 |
| 动作与盒体范围不一致 | 用真实模型做命中位置和生效窗的独立样本，手动观察效果与攻击范围。 |

## 8. 任务动作与前置检查

- [x] 需求与设计完成独立复审并获用户确认。
- [x] 规格增量四条已通过 C1–C8。
- [x] 独立复审计划并获用户开工授权。
- [x] 建立隔离基线与安装前哈希清单。
- [x] 确认可用 Unity 版本、批处理独占资源以及可回溯日志。

## 9. 每批收尾三件事

更新时间、任务状态和证据。核对本批改动路径与下一批依赖。整理安装范围和回退所需的文件哈希。
