# 问题与验证记录 — Ricca 技能系统 v0.0.1

## 1. 执行中遇到的问题

| # | 时间 | 现象 | 阶段 | 处理 | 状态 |
|---|------|------|------|------|------|
| 1 | 2026-11-22 | 当前工程未检测到 Git 工作树，git rev-parse 返回 128 | 设计 | 规格增量和设计保存在本地，冻结提交未执行 | 待具备 Git 检出 |
| 2 | 2026-11-22 | 首次规则转绿验证发现技能连击用例误把前一波普攻已经产生的连击值当成 0 | 规则测试 | 保留前置清波并以施放前快照对照施放后连击。新用例转绿 8/8。 | 已解决 |
| 3 | 2026-11-22 | 专项 PlayMode 第一次投递真实点击时，CombatHud Canvas 无 GraphicRaycaster。 | 场景验收 | 定位到 Canvas 射线能力。最终接线见第 7 项。 | 已解决 |
| 4 | 2026-11-22 | 现有波次 UI 测试仍断言技能奖励为占位。新 PlayMode 用例直接访问 Domain 技能枚举。 | 测试兼容 | 现有测试改为断言新授予技能名称及等级。仅测试 asmdef 增加 Domain 直接引用。 | 已解决 |
| 5 | 2026-11-22 | HUD 退出前的全局时间可能已被外部暂停接管，或 HUD 在首帧刷新前已停用。 | 独立审查 | 按所有权还原时间，并在 HUD 解绑时取消角色选择。专项 PlayMode 覆盖。 | 已解决 |
| 6 | 2026-11-22 | Demo 资产回归固定断言 Ricca 控制器只有 6 段。 | 全量 EditMode | 继续精确匹配资源集合，保留原六段并明确加入本轮四段。敌人集合要求仍为五段。 | 复验通过 |
| 7 | 2026-11-22 | 全量 PlayMode 捕捉战斗 HUD 不再被动的回归。首轮为技能加的 CombatHud 射线组件被旧 HUD 行为测试拒绝。 | 场景集成 | 技能面板改为挂到已有 WaveHud 交互 Canvas。保留 CombatHud 仅展示，原测试及真实点击验收一同复跑。 | 复验通过 |

## 2. 阻断项（需人工决策）

| # | 阻断内容 | 来源 | 影响 | 待决策点 |
|---|---------|------|------|---------|

## 3. 与计划的偏差

| 计划项 | 实际 | 原因 |
|---|---|---|
| | | |

## 4. 验证记录

### 4.1 自动验证

| # | 用例 | 级别 | 证据 | 结果 |
|---|---|---|---|---|
| T1 | 隔离基线 | L0、L1、L2 | [编译](../../../Logs/RiccaSkillsVerification/Baseline-Compile.log)、[EditMode](../../../Logs/RiccaSkillsVerification/Baseline-EditMode.xml)、[PlayMode](../../../Logs/RiccaSkillsVerification/Baseline-PlayMode.xml) | 编译退出 0，45/45 和 13/13 |
| T2 | 新规则红绿 | L1 | [红](../../../Logs/RiccaSkillsVerification/SkillRules-Red.xml)、[绿](../../../Logs/RiccaSkillsVerification/SkillRules-Green2.xml) | 7 条新行为在接口桩失败，1 条旧路径验证通过。实现后 8/8 |
| T3 | 最终编译、规则及动画资产 | L0、L1 | [编译](../../../Logs/RiccaSkillsVerification/Release2-Compile.log)、[EditMode](../../../Logs/RiccaSkillsVerification/Release2-EditMode.xml)、[扫描](../../../Logs/RiccaSkillsVerification/Release2-VerifyAssets.log) | 0 C# 编译错误和警告，54/54，四动画和两特效已绑定，Demo 无 Missing Script |
| T4 | 最终场景回归 | L2 | [PlayMode](../../../Logs/RiccaSkillsVerification/Release2-PlayMode.xml) | 14/14。四次奖励和逐个真实 UGUI 点击施放，定向盒多目标和去重，选择时间恢复及旧 HUD 被动输入回归 |
| T5 | 规格与程序集 | C1–C8、G4、G7 | [delta](07-spec-delta.md)、[程序集规则](../../../docs/rules/工程与资源.md) | 4 条 delta 校验通过，0 警告。仅 PlayMode 测试增 Domain 引用，现有运行时依赖保持。局内状态无存档迁移 |
| T6 | 安装 | SHA-256 | [安装清单](../../../Logs/RiccaSkillsVerification/installed-sha256.json)、[发行清单](../../../Logs/RiccaSkillsVerification/release-manifest.json) | 63 个源文件、资产和 meta 与隔离验证一致。13 个覆盖目标已备份 |

### 4.2 画面、输入与时间

| # | 前置条件 | 操作 | 证据 | 结果 |
|---|---|---|---|---|
| M1 | 新局、四槽空 | 按 F，再按 Esc | [720p](../../../Logs/RiccaSkillsVerification/Screens/01-Empty-Slots-720p.png) · [1080p](../../../Logs/RiccaSkillsVerification/Screens/01-Empty-Slots-1080p.png) | 四格禁用，可见获取与关闭提示。原速不变 |
| M2 | 四项已获技能 | 按 F，再点击各技能 | [720p](../../../Logs/RiccaSkillsVerification/Screens/02-Ready-Skills-720p.png) · [1080p](../../../Logs/RiccaSkillsVerification/Screens/02-Ready-Skills-1080p.png) | 两档布局无裁切，显示名称、等级和范围。射线命中按钮，点击当帧退出减速并进入技能动画 |
| M3 | 已获得 Sweep、Ground Burst | 点击并在命中窗捕获 | [Sweep](../../../Logs/RiccaSkillsVerification/Screens/03-Sweep-1080p.png) · [Ground Burst](../../../Logs/RiccaSkillsVerification/Screens/04-Ground-Burst-1080p.png) | 前方橙金横扫和脚下蓝色光环均在画面可见 |
| M4 | 状态与 HUD 预热 | 测 120 次稳定调用 | [托管分配](../../../Logs/RiccaSkillsVerification/SkillUiSteadyAllocations.txt) | 受测状态读取和 HUD 更新合计 0 B |

UI 检查结果为 U1、U2、U3、U5、U8、U10 已核对。U4 同步面板无加载等待。U6 自动测试断言点击当帧动作。U7 英文文案本地化待接入。U9 分配证据限于上表的 HUD 和状态路径。

### 4.3 未验证项

| 项 | 状态 | 影响 |
|---|---|---|
| 真人试玩节奏 | 自动实景测试以固定种子验证首次奖励后可施放和四槽。尚无真人随机局记录 | 普通一局首次拿到技能的观感待真人试玩 |
| Windows Player 全帧性能 | 当前证据来自隔离 Unity Editor 和作用域内的托管分配采样 | 未宣称独立播放器的全帧性能 |



## 5. 无人值守执行完成报告（仅 `--unattended`）

| Phase | 状态 | 阻断 | 警告 | 待人工事项 |
|-------|------|------|------|-----------|
| P1 | | | | |
| P2 | | | | |
| P3 | | | | |
| P4 | | | | |

## 6. 遗留与技术债

| 项 | 影响 | 建议处理时机 |
|---|---|---|
