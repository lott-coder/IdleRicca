# 执行计划 — 波次与波次奖励

> 模块 gameplay · 子分类 wave-rewards · v0.0.1
> 日期 2026-11-22 · 状态 主工程已安装并核对，目录版本索引记录归档状态
> 依照 01-requirements.md、02-design.md 和 07-spec-delta.md 执行。

## 1. 任务清单

| # | 任务 | 输出文件 | 层 | 测试 | 资产 | 依赖 | 状态 |
|---|---|---|---|---|---|---|---|
| 1 | 建隔离副本，记录当前安装范围的哈希和基线。使用同版本 Unity 与 D3D11。 | `Logs/WaveRewardsVerification/project/`、`Logs/WaveRewardsVerification/baseline/` | 验证环境 | L0、L1、L2 基线 | 输入副本 | 开工确认 | ✓ |
| 2 | 先加规则用例。覆盖十波连续推进、间隔与数量、跨波尸体、失败回收、复现抽奖、重复与过期、叠加、死亡优先。只建使测试可编译的最小入口和类型，记录断言红测，任务 3 接续这些路径。 | `Assets/Tests/EditMode/WaveRunTests.cs` 和任务 3 中被测试引用的最小类型桩 | EditMode、测试契约 | L1 | — | 1 | ✓ |
| 3 | 加十波纯数据、奖励池、局内状态与快照。扩展既有占位和敌人生命绑定，接入普攻伤害、HP、移动与伤害守卫。保留现有战斗入口。 | `Assets/Scripts/Domain/Gameplay/{WaveDefinition,WaveReward}.cs`，`Assets/Scripts/Application/Gameplay/{WaveRun,WaveStatus,WaveRewardPool,EnemyPopulation,EnemyHealth,PlayerActions}.cs` | Domain、Application | L1 | — | 2 | ✓ |
| 4 | 加配置校验、场景进度接线、出生和销毁报告。守卫设备与替代输入，处理焦点、死亡演出和同场景重开。 | `Assets/Scripts/Presentation/Gameplay/{WaveSettings,EnemySpawner,EnemyActor,RiccaPlayer}.cs` | Presentation | L0、L2 | A1、A3 | 3 | ✓ |
| 5 | 加顶部波号和三个可选奖励按钮，处理死亡、通关和重开。配置默认值。用隔离工程的 Editor API 创建 prefab 和安全接线。仅安装最终资源与运行时代码。 | `Assets/Scripts/Presentation/UI/WaveHud.cs`，`Assets/Config/Gameplay/WaveSettings.asset`，`Assets/Prefabs/UI/WaveHud.prefab`，`Assets/Scenes/Demo.unity`，新文件对应 `.meta`；临时工具 `Logs/WaveRewardsVerification/project/Assets/Editor/BuildWaveAssets.cs` | Presentation、资源 | L0、L2、L4 | A1–A3 | 4 | ✓ |
| 6 | 加实际场景验证和截图。验证三选一时同帧输入拦截、光标与焦点、实际 HP 和普攻、死亡与重开、完整波次结束。先跑本轮测试，再跑全量，记录构建者的 U1–U10 自查。 | `Assets/Tests/PlayMode/WaveRunGameplayTests.cs`，`Logs/WaveRewardsVerification/`，`change/gameplay/_tmp/05-issues.md` | 测试、验证 | L0–L4 | A1–A3 | 5 | ✓ |
| 7 | 独立代码审核和 UI 清单复核，修复后在隔离工程重跑门禁。比对安装前主工程哈希，备份后只安装已验范围，校验安装哈希。 | `change/gameplay/_tmp/04-review-report.md`，`Logs/WaveRewardsVerification/baseline/`，`05-issues.md` | 审核、安装 | L0–L4、哈希 | A1–A3 | 6、代码审核确认 | ✓ |
| 8 | 用 knowledge-set 沉淀用法与问题。验收记录齐全后运行 spec_tool 校验与归档。 | `knowledge/gameplay.md`，`change/gameplay/_tmp/06-knowledge-update.md`，归档工具产物 | 交付 | C1–C8、G1–G8 | — | 7、知识更新确认 | 知识已写入，版本索引记录归档状态 |

## 2. 资产修改清单

使用隔离 Unity Editor 批处理制作。启动前检查独占条件，写入前后保留文件哈希。资源对应的 Unity 生成 `.meta` 随资产安装。

| # | 类型 | 资产路径 | 操作 | 验收方式 |
|---|---|---|---|---|
| A1 | ScriptableObject | `Assets/Config/Gameplay/WaveSettings.asset` | 填入十行波次、三类权重和三个等级。 | 值匹配设计，错误配置显性报告。 |
| A2 | Prefab | `Assets/Prefabs/UI/WaveHud.prefab` | 生成顶部 TMP 波号、三按钮及结果页，绑定 CanvasScaler 与 GraphicRaycaster。 | 实际显示、可点击，两种分辨率无裁切。 |
| A3 | Scene | `Assets/Scenes/Demo.unity` | 在现有 spawner 绑定 A1，新增 A2 实例及所需的单个 EventSystem。 | Demo 仍是构建第一场景。原导航、角色、相机和 CombatHud 引用有效。 |

## 3. 依赖关系

开工 → 基线 → L1 用例 → 规则 → 场景桥接 → 配置和界面 → 验证 → 代码审核与确认 → 安装 → 知识与归档。

## 4. 验证计划

| 阶段 | 动作 | 通过标准 |
|---|---|---|
| 基线与编译 | 使用 Unity 2022.3.52f1c1 有图形设备的独立编译。保留初始化和日志。 | 无 C# 编译错误，适用既有测试状态有记录。 |
| 规则 | 单独执行新用例，确认先失败的内容与预期相符。实现后全跑 Game.Tests.EditMode。 | AC-1–AC-6 的纯状态断言通过，既有规则用例保留。 |
| 场景 | 全跑 Game.Tests.PlayMode，覆盖真实玩法和已保存的场景。 | 通常路线与负路径可触发，旧测试无新增失败。 |
| 界面 | 捕获 1280×720、1920×1080 的战斗、三选一、最后奖励、死亡和通关画面。 | 标签、数据、按钮可读。实际 UI 事件测试核对选取和同帧输入。另记录真人观察状态。 |
| 安装与归档 | 核对适用 G1–G8、U1–U10、规格 C1–C8、安装目标和输入哈希。哈希覆盖 Build Settings、asmdef、锁定包及 Demo 依赖的角色、现有 HUD、生成配置与导航。 | 无阻断。报告列出具体结果与未验证项。 |

## 5. 验收标准对照

| AC | 覆盖任务 | 证据 | 状态 |
|---|---|---|---|
| AC-1、AC-2 | 2、3、4、6 | 模拟时钟、失败回收、旧尸体占位、第 4 和第 6 个名额、实际首波阵容 | ✓ |
| AC-3、AC-4 | 2、3、5、6 | 改变随机输入、选择幂等、真实 HP 和攻击伤害，技能事件计数 | ✓ |
| AC-5 | 2、3、4、5、6 | 完整 1–10，11 波不可达，实际 EventSystem 点击与同帧设备输入 | ✓ |
| AC-6 | 2、3、4、5、6 | 同帧死亡优先、死亡结尾、两种重开、配置基准和加成清理 | ✓ |
| AC-7 | 5、6 | 上述两种分辨率截图、四类状态和最后奖励变体 | ✓ |

## 6. 回滚方案

在安装前将每个既有覆盖文件与其 `.meta` 按原路径备份，记录安装前后 SHA-256。回退时先确认当前哈希对应本轮安装内容，再恢复覆盖项和移出新增项。若已有后续修改，只回退对应变更并复核 Demo 引用。活规格通过归档工具更新，归档前可用 dry-run 查看影响。

## 7. 风险

| 风险 | 应对 |
|---|---|
| 场景正由另一个 Unity 会话使用 | 隔离工程运行批处理。安装前重读工作区源文件。发现冲突时只重新接合受影响路径并验证。 |
| UI 接管导致相机、攻击或鼠标失效 | 验证真实 UI 点击、同帧模拟设备输入和焦点回归。 |
| 后续波大于原上限 | L1 断言实际注册第 4 和第 6 个实例，并在 Demo 读取当前波配置。 |
| 空候选或加载未成功 | 复用有限的刷怪尝试和死亡动画超时。载入失败显性提示，可再次提交重开。 |

## 8. 任务动作与前置检查

- [x] 需求和设计已独立复审并获用户确认。
- [x] 已核对既有生成、战斗、场景与 HUD 入口。配置资产新增独立引用。
- [x] 用户确认本计划并开始修改玩法。
- [x] 建立隔离基线和源文件哈希。
- [x] 验证 Unity 版本与批处理独占状态。

## 9. 每批收尾

保存新鲜测试与截图，更新任务状态和问题记录，复核安装清单与哈希。当前工作区采用源文件备份和哈希追踪。
