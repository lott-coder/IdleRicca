# 实施计划 — Ricca 源技能、技能池与蓝耗

> gameplay ｜ skill-pool-mana ｜ v0.0.1  
> 2026-11-22 ｜ ✓ 已归档至 change/gameplay/skill-pool-mana v0.0.1  
> 关联 [需求](01-requirements.md)、[设计](02-design.md)、[规格增量](07-spec-delta.md) 和 [蓝条计划](../../ui/combat-mana/03-plan.md)。

## 1. 任务清单

| # | 输出与动作 | 层 | 证据 | 资产 | 依赖 | 状态 |
|---|---|---|---|---|---|---|
| 1 | 创建 `Logs/SkillPoolManaVerification/project/`、`run-unity.ps1`、`baseline/sources.json`。在隔离项目以 2022.3.52f1c1、D3D11 跑主工程编译和全量基线。只读记录授权素材 SHA-256 和主工程可能覆盖的路径。 | 验证环境 | L0、L1、L2 | 只读 | 用户开工确认 | ☑ |
| 2 | 先在 `Assets/Tests/EditMode/SkillManaTests.cs`、`SkillWaveRewardTests.cs`、`WaveRunTests.cs` 建立新行为失败断言。以槽位多于四项、有效/无效施放和等奖励边界构造用例。需要新签名时，只在任务 3 的最终文件加入可编译接口桩。 | 测试 | L0、L1 红 | 新脚本及 meta | 1 | ☑ |
| 3 | 修改 `Assets/Scripts/Domain/Gameplay/{SkillId,SkillDefinition}.cs`、`Assets/Scripts/Application/Gameplay/{PlayerActions,PlayerSkillSet,PlayerCombatStatus,SkillStatus,WaveRun,WaveRewardPool}.cs`。实现目录与四槽独立、锁定预告、按资格同权、可配置满蓝及每次原子扣 10 MP。让新增用例转绿，并保护旧场景奖励与通知。 | Domain、Application | L1 红绿 | 无运行资产改动 | 2 | ☑ |
| 4 | 在 `Logs/SkillPoolManaVerification/import_source_skills.py` 维护源路径、目标路径、数据类型、SHA、GUID 和目标适配说明。将十段动作和使用中的原始效果闭包复制到隔离的 `Assets/Art/Demo/Ricca/SourceSkills/`。既有相同 GUID 安全复用，脚本和非表现组件从隔离副本引用图排除。在隔离目录完成源资产、GUID、已引用渲染依赖与清单的静态核查。真实挂载与渲染由任务 5、6 验证。 | 资源、静态验证 | 源清单和依赖校验 | A1–A3 | 1、3 的技能 ID | ☑ |
| 5 | 修改 `Assets/Scripts/Presentation/Gameplay/{RiccaPlayer,RiccaSkillSettings,SkillHitbox,WaveSettings}.cs` 以及 `Assets/Scripts/Presentation/UI/{SkillSelectionHud,WaveHud,CombatHud}.cs`。施放先预检，再提交动作和 MP，接通真实技能动作、武器表现、投射轨迹与有效窗内扫掠。记录技能选择/施放的终止兼容行为。编辑器接线工具只放在隔离 `Assets/Editor/Gameplay/BuildSourceSkillUpgrade.cs`。 | Presentation、Editor 验证 | L0、L2、L4 | A1–A6 | 3、4 | ☑ |
| 6 | 扩充 `Assets/Tests/PlayMode/RiccaSkillGameplayTests.cs`、`WaveRunGameplayTests.cs`、`CombatHudTests.cs` 及 `Assets/Tests/EditMode/{RiccaSkillAssetTests,DemoAssetTests}.cs`。保留旧输入和战斗要求，确因范围变更的测试断言改为验证新契约。对五项技能逐项实机验收并补齐中断、MP 临界、窗口外路径和源依赖失败用例。 | 测试、验证 | L1、L2、L4 | A1–A6 | 5 | ☑ |
| 7 | 在隔离工程跑全量编译、EditMode、PlayMode 和资源扫描，保存 XML、日志、截图、静态源清单与 U1–U10 核对。独立代码复审落到 `04-review-report.md`。发现问题后修复并重跑相关门禁。 | 验证、审查 | G1–G8、L0–L4 | A1–A6 | 6 | ☑ |
| 8 | 用户确认审阅结果与主编辑器释放受影响资源后，先核对主文件与 baseline，备份现有文件，按 `release-manifest.json` 安装已验证文件及 meta。用主工程逐项复核 SHA，保存 `installed-sha256.json`。 | 安装 | 安装哈希 | 仅清单资产 | 7、安装交接 | ☑ |
| 9 | 预览并确认 `knowledge/gameplay.md` 和 `knowledge/ui.md` 的本轮发现，更新两个版本的 `06-knowledge-update.md`。先验证 gameplay 与 ui 的两份增量，并让两个 dry-run 全部成功，再用 spec_tool 先归档 gameplay、后归档 ui。 | 交付 | C1–C8、规范确认 | 工作流产物 | 8、用户确认知识和归档 | ☑ |

### Phase 4 测试约束

Unity runner 以隔离目录的 `project/.editor.lock` 独占批处理，包含编译、PlayMode 和资源检查。启动前原子创建，记录本次令牌和进程，运行过程维持续租，退出 finally 中只释放本次令牌。若已有租约，核实后排队，或在隔离资源可用后继续。主工程编辑器不用于隔离测试。

任务 2 的目标测试先确认预期业务断言失败，编译错误不能作为红灯。任务 3 之后再次跑目标规则和旧回归。任务 6 在交付资源上记录每个动作与每条移动伤害路线的正、负证据。现有精确动画集合用例保留旧六段已用动作，同时用新源片段替换占位的运行预期。

## 2. 资产修改清单

| # | 目标路径 | 变更与验收 |
|---|---|---|
| A1 | `Assets/Art/Demo/Ricca/SourceSkills/AnimationClip/` | 十段授权源动作及 meta。记录源文件与目标一对一映射，记录旧音频事件和未解析装饰轨适配。源动作关键帧沿用，控制器提供实际播放路线。 |
| A2 | `Assets/Art/Demo/Ricca/SourceSkills/{GameObject,AnimatorController,Material,Mesh,Texture2D,Shader,Flare}/` | 收敛到五项实际使用的源粒子、渲染资源和依赖。对根 Prefab 去除旧脚本和伤害状态，再保留原可见层级。最终文件逐项出现在导入清单。 |
| A3 | `Assets/Art/Demo/Controllers/Ricca.controller`、`Assets/Prefabs/Demo/Ricca.prefab` | 五主动作、五方向或状态变体的 Animator 绑定，替换四种旧占位状态和效果引用。保留常规战斗状态、胶囊和现有层级。 |
| A4 | `Assets/Config/Gameplay/RiccaSkills.asset`、`Assets/Config/Gameplay/WaveSettings.asset` | 五项目录、运动盒和蓝耗接线，权重统一为 45。记录最终窗口和盒形校准值。 |
| A5 | `Assets/Prefabs/UI/CombatHud.prefab`、`Assets/Prefabs/UI/SkillSelectionHud.prefab` | 复用 Mana Fill 与原风格配置真实 MP，字幕明确 10 点成本。被动 HUD 继续只展示，技能面板在 WaveHud 交互 Canvas 接点击。 |
| A6 | `Assets/Scenes/Demo.unity`（只在确有绑定调整时） | 保留一个 EventSystem 和原始战斗接线。资产检查确认 Scene 与 Prefab 的覆盖引用一致。 |

导入资源和新脚本的 .meta 随对应资产验证。原占位文件在历史资源目录内保留回退，运行时控制器和可获技能目录使用新的源资产。

## 3. 依赖关系

基线 → 失败用例 → 应用规则 → 来源资产闭包与适配 → 配置、HUD、伤害盒接线 → 专项实机和全量回归 → 独立审核与安装 → 双模块归档。

旧波次入口保持 `TryGetSkillRewardPreview` 和 `TryChooseReward`。旧动画配置 `_effect` 保留类型并支持旧 Prefab。新资源使用 `_effectRoot`。运行安装范围为计划清单与经核查的闭包，源游戏工程只读。

## 4. 验证计划

| 验收 | 级别 | 测试与证据 |
|---|---|---|
| AC-1 原动作和特效 | L0、L2、L4 | 五个主动作通过真实技能奖励和鼠标选技。十段从源文件匹配并逐个 Animator 求值。记录实际可触发的投剑变体。检查真源效果、Shader、Mesh 和材质在相机内可见，无 Missing Script。核对旧占位退出运行。 |
| AC-2 技能目录和领取 | L1、L2 | 五项种子覆盖、四槽限制与去重、过期或重复拒绝、预告与结算一致、四槽满时升级后实际伤害不同。末波技能一次通知。 |
| AC-3 MP | L1、L2、L4 | 100 开局和重开满蓝、10 MP 接受并扣到 0、9 MP 拒绝、重复/取消和资源预检失败维持。真实点击模拟外部暂停及Animator 或必要效果无效。用户看到蓝条变化、缺蓝禁用、确认动作时恢复时间。施放被接受后受击仍保留 10 MP 扣减，并作废后续伤害批次。 |
| AC-4 同权及无效候选 | L1、L2 | 种子验证有效情况下 45/45/45。满级、无目录或缺蓝仍有唯一三选一；旧入口占位与原数据类型仍兼容。 |
| AC-5 Damage | L1、L2 | 五项逐个验证盒内外、窗内外、重复受击体和致死回调。投剑、冰球、短突击分别验证跨窗的粗时间步、挡墙截断和窗外轨迹、停用及过期批次。 |
| 既有战斗与性能 | L0–L4 | 全量 2022.3.52f1c1 编译、EditMode、PlayMode 和程序集/资产引用检查。1280×720、1920×1080 实机截取技能选择、蓝条、缺蓝及源 FX。采样 120 次预热稳态状态/HUD 与动作查询路径，逐项区分图形和测试外壳。 |

## 5. 验收对照

| 需求验收 | 任务 | 可交付证据 |
|---|---|---|
| AC-1 | 4–7 | 资产源清单、Animator 实际状态、图形证据和扫描 |
| AC-2 | 2–3、6–7 | 固定种子、奖励卡/槽位一致、原子拒绝 |
| AC-3 | 2–3、5–7 | 当前及最大 MP 快照、实际 HUD、资源失效场景 |
| AC-4 | 2–3、6–7 | 资格过滤和等权计算证据 |
| AC-5 | 2–3、5–7 | 实际盒伤害与移动路径正负样本 |

## 6. 回退方案

主工程跟踪可能覆盖的路径、初始 SHA-256 和归档工具状态。安装前重新校验使用中的主编辑器场景与资源，待原编辑者保存并释放后进行。闭包中新目标在发行清单标记 expected-absent，安装前若已存在则停止并重新核查 GUID 与内容。修改的现有文件按原路径备份。分批安装失败时，只还原当前哈希仍属于本轮的内容并移出本轮已添加的资源。已经由其他编辑者更新的文件保留差异，核对后再处理。源目录保持只读。

若 gameplay 归档成功而 ui 归档失败，保留已验证安装和 gameplay 的归档结果。记录部分归档及失败证据，修复后用工具重跑 ui。双模块均成功才关闭本轮。

## 7. 风险

| 风险 | 处理 |
|---|---|
| 源工程旧行为与动画事件无接收器 | 审核脚本链及事件，源表现接线与当前战斗规则分开，真实播放期间无流程警告 |
| 装饰曲线路径在 AssetRipper 导出未解析 | 检查能找到的原绑定，必要时排除明确与动作骨架无关的未绑定通道，记录剔除列表并证明所有技能能正常播放和命中 |
| 粗时间步提前越过 hit window | 用批次交集顺序和三种运动盒路径正负样本保护命中 |
| 导入依赖撞 GUID 或主工作区并行变化 | 复用已确认资源，目标哈希和素材来源变化时重新核验。主工程扫描独占资源后仅安装清单 |
| 冻结提交无法执行 | 当前工作区无 Git 差异历史，沿用基线、安装清单和备份核验 |

## 8. 任务动作与前置检查

- [x] 需求与两模块设计获用户确认，联合复审通过。
- [x] 两份规格增量通过 C1–C8。
- [x] 实施计划独立复审并获开工确认。
- [ ] 校验隔离工程、Unity 版本与基线。
- [ ] 校验源资产读取路径和安装清单。

## 9. 每批收尾三件事

保存证据与状态，核对实际改变路径与实际依赖，更新 `05-issues.md`、安装清单和可回退备份。
