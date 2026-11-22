# 执行计划 — Demo 主界面战斗 HUD

> 模块 ui / combat-hud · v0.0.1  
> 日期 2026-11-22  
> 状态 已安装并验证，交付记录随规格归档

## 1. 任务清单

| # | 任务 | 输出文件 | 层 | 测试 | 资产 | 依赖 | 状态 |
|---|---|---|---|---|---|---|---|
| 1 | 搭建独立 UGUI 预制体。左上放 HP 与蓝色 MP，右侧放连击数字和细倒计时条。检查正常、空血和 2 连击摆位。 | `Assets/Prefabs/UI/CombatHud.prefab`、TMP 必需资源及对应 `.meta`，先在隔离副本保存 | Presentation、Editor | L4 预览 | A1、A3 | 用户确认本计划并启动执行 | ✓ |
| 2 | 核对上游接线。确认 HP 上限来源、真实扣血分支、归零守卫和 Demo 中 Ricca／EnemySpawner 的序列化绑定。记录输入哈希。 | `change/ui/_tmp/05-issues.md` | Application、Presentation | L0、L2 基线 | 读取 A2 | gameplay / core 已在主工程落地 | ✓ |
| 3 | 先写连击规则测试。覆盖首击、4.999 秒接续、5.000 秒到期、坏时钟和倒退时钟、已有连击时空挥、同挥多目标、无效命中、击杀和真实受伤。 | `Assets/Tests/EditMode/CombatComboTests.cs` | 测试 | L1 | — | 2 | ✓ |
| 4 | 在现有 `TryHit` 成功分支更新计数，在 gameplay 扣血分支清零，加游戏时间命令及只读快照。 | `Assets/Scripts/Application/Gameplay/PlayerActions.cs`、`Assets/Scripts/Application/Gameplay/PlayerCombatStatus.cs` | Application | L1 | — | 3 | ✓ |
| 5 | 给 Ricca 接入时间与只读状态。预制体绑定生命、连击数字、倒计时和强调。晚于所有伤害生产端绘制。 | `Assets/Scripts/Presentation/Gameplay/RiccaPlayer.cs`、`Assets/Scripts/Presentation/UI/CombatHud.cs`、`Assets/Scripts/Presentation/Game.Presentation.asmdef`、`docs/rules/工程与资源.md` | Presentation | L0、L2、L4 | A1、A2、A3 | 1、4 | ✓ 隔离验证 |
| 6 | 验证 Demo、受伤断连、计时动效、禁用与恢复。跑全量回归和独立代码／UI 审核，保存 UI 证据。 | `Assets/Tests/PlayMode/CombatHudTests.cs`、`Assets/Tests/PlayMode/Game.Tests.PlayMode.asmdef`、`Logs/CombatHudVerification/`、`change/ui/_tmp/04-review-report.md`、`change/ui/_tmp/05-issues.md` | 测试、验证 | L0–L4 | A1、A2、A3 | 5 | ✓ |
| 7 | 复核隔离副本与当前工作区，备份后安装已验证的精确文件范围。确认规格、更新知识并按流程归档。 | `knowledge/ui.md`、`knowledge/README.md`、`change/ui/_tmp/06-knowledge-update.md`、归档工具产物 | 交付 | 哈希、spec_tool | A1、A2、A3 | 6、代码审核用户确认 | ✓ 已安装、已更新知识 |

## 2. 资产修改清单

资产通过独立 Unity Editor 副本制作和保存。隔离副本存放于 `Logs/CombatHudVerification/project`。共享场景以 gameplay / core 的落地版为输入。

| # | 类型 | 资产路径 | 操作 | 验收方式 |
|---|---|---|---|---|
| A1 | Prefab | `Assets/Prefabs/UI/CombatHud.prefab` | 用 UGUI Canvas、CanvasScaler、Image 和 TMP 文本搭建 HUD，绑定绘制控件 | Inspector 引用完整。两种分辨率的默认和连击画面可读 |
| A2 | Scene | `Assets/Scenes/Demo.unity` | 在最新 Demo 上增加单一 HUD 实例，绑定该场景原有 Ricca | 第一个构建场景默认显示 HUD，敌人生成器与镜头引用有效 |
| A3 | 字体与材质 | `Assets/TextMesh Pro/` | 导入项目已安装 TMP 包所需的 Essential 字体、材质及设置，记录实际安装清单 | HP／MP 标签、数字和连击标签可见，字体与材质引用可解析 |

Ricca 的初始生命和 prefab 配置由 gameplay 交付。HUD 的绘制图形全部禁用射线命中。

## 3. 依赖关系

```text
用户确认执行 → 独立布局预览 ──────────────────┐
gameplay / core 落地 → 接口核对 → L1 用例 → 连击实现 → Demo 接线 → 全量验证
```

共享文件接线从 gameplay / core 落地后的主工程复制。若在隔离副本验证期间检测到源哈希变动，只重做受影响的改动和回归。

## 4. 验证计划

| 阶段 | 动作 | 通过标准 |
|---|---|---|
| 上游接线核验 | 对照实际 Application 扣血方法、最大 HP 来源、Ricca 归零和场景结构 | HP 写入口唯一。初始配置、现有生成器及相机绑定能在 Unity 读取 |
| 应用规则 | 跑新增 EditMode 用例，再跑全量 EditMode | 去重、计时、实际受伤和无效伤害的断言通过，原玩法用例保留 |
| 场景路径 | 使用有图形设备的全量 PlayMode。Demo 验证装配，固定近战用例验证真实敌人碰撞和普攻 | HUD 帧与血量写入一致，空挥不续时，连击在受伤和超时后隐藏 |
| 视觉与性能 | 留存同场景默认画面和激活连击两组截图，记录数字强调和倒计时变化。采集待机、连击可见和数字强调期间的 GC、Canvas 更新和布局重建情况 | 1280×720 与 1920×1080 均可读。有效命中使填充回满。记录测试机 GC 与 UI Profiler 证据 |
| 归档前 | 核对适用 G1–G8、U1–U10 和 C1–C8 | 无阻断，记录具体测试结果、视觉证据及不适用项目 |

## 5. 验收标准对照

| AC | 覆盖任务 | 证据 | 状态 |
|---|---|---|---|
| AC-1、AC-2 | 1、2、5、6 | 初始、受伤、空血和重载结果，HP／MP 截图 | ✓ |
| AC-3、AC-4、AC-5 | 3、4、6 | EditMode 精度边界，PlayMode 出击、共享受伤命令、HUD 状态 | ✓ |
| AC-6、AC-7 | 1、5、6 | 两种分辨率截图、控件填充断言、连击过程录屏 | ✓ |

## 6. 回滚方案

安装前将本轮覆盖的源文件及 `.meta` 备份到 `Logs/CombatHudVerification/baseline/`，记录安装前后哈希。回退时先比对当前文件与安装后哈希。一致时恢复原文件或移出本轮新增文件。存在后续修改时只撤除对应的 HUD 改动，核对保留的 Demo 引用。Gameplay 与 core 的归档由各自迭代处理。

## 7. 风险

| 风险 | 影响 | 应对 |
|---|---|---|
| 敌人 AI 的实际接口与当前设计不同 | 无法直接挂接写入口或读取最大 HP | Phase 4 前回查实现，必要时修订计划并复核 |
| 当前编辑器正打开 Demo | 同目录批处理会争用现有 Unity 进程 | 使用独立副本。安装前核对最新源文件哈希，保留可回退备份 |
| 城市真实光照与布局预览差异 | 局部文本反差不足 | 通过实际 Demo 截图调整对比度并复拍 |

## 8. 任务动作与前置检查

- [x] 已核对 UGUI 与 TMP 已在项目依赖内。
- [x] 已确认接线顺序，HUD 依赖敌人 AI 落地后的 Ricca 状态和 Demo。
- [x] 已确认计划与开工。
- [x] 已比对 gameplay 上游实现与主工程共享文件。全部 60 个交接文件哈希与已验证版本一致。

## 9. 每批收尾三件事

1. 保存本轮的新鲜测试、截图和输入哈希。
2. 将场景接线、Unity 验证和确有必要的经验记入问题或知识文档。
3. 对照安装清单核对已验证副本与主工程的内容。当前工作区使用哈希和本地备份记录变更。
