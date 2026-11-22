# Demo 局内蓝金界面与图标

## 改动原因与范围

按用户逐项确认的方案，将 Demo 的波次徽章、技能选择、三选一奖励与本局结算更新为项目的蓝金骑士样式。保留四槽布局与点击入口。技能槽和技能奖励使用同一套专属图标。生命奖励使用暖红心形，普攻伤害使用暖金剑形。结算以标题和分隔线的颜色区分通关、失败与重载延迟。

- 更新 `Assets/Prefabs/UI/{WaveHud,SkillSelectionHud}.prefab` 的面板、图标、按钮和 TMP 布局。
- `Assets/Art/UI/BlueGoldHud/` 包含新图标、切角边框、按钮状态和图标目录，文字复用战斗 HUD 的描边材质。
- `Assets/Scripts/Presentation/UI/{WaveHud,SkillSelectionHud,HudIconSet}.cs` 按既有快照选择图标与结算颜色。选技、奖励和重开继续使用原有命令入口。
- 复查 720p 样图后将奖励文案设为 18，文字上移 2 个参考单位，留出长说明与卡片边框的间距。

## 验证记录

| 验证 | 证据 |
|---|---|
| Unity 2022.3.52f1c1 编译 | [Compile.log](../../../Logs/BlueGoldHud/Compile.log)，进程退出码 0，日志无 C# 错误或警告 |
| EditMode | [EditMode.xml](../../../Logs/BlueGoldHud/EditMode.xml)，63 通过 |
| PlayMode | [PlayMode.xml](../../../Logs/BlueGoldHud/PlayMode.xml)，18 通过，覆盖实际 UGUI 射线点击、缺蓝、资源失效、奖励、通关与重开 |
| 最终奖励布局复验 | [WavePlayMode.xml](../../../Logs/BlueGoldHud/WavePlayMode.xml)，2 通过，重新捕获 [普通奖励 720p](../../../Logs/BlueGoldHud/Wave/Screens/WaveReward-720p.png)、[普通奖励 1080p](../../../Logs/BlueGoldHud/Wave/Screens/WaveReward-1080p.png)、[最后奖励 1080p](../../../Logs/BlueGoldHud/Wave/Screens/FinalReward-1080p.png)、[失败](../../../Logs/BlueGoldHud/Wave/Screens/RunOver-720p.png)与[通关](../../../Logs/BlueGoldHud/Wave/Screens/RunComplete-720p.png) |
| 资产 | [Author.log](../../../Logs/BlueGoldHud/Author.log)，9 个不同的技能 ID 图标与空槽分别绑定，两个预制体共用图标目录，7 个图标均禁用射线检测，保留 Demo 的 HUD 绑定 |
| 工程安装 | [文件校验清单](../../../Logs/BlueGoldHud/installed-sha256.json)，53 个安装文件与隔离验证工程的 SHA-256 一致 |
| 更新前布局 | [奖励 720p](../../../Logs/WaveRewardsVerification/Screens/WaveReward-720p.png)、[选技 720p](../../../Logs/SkillPoolManaVerification/Screens/02-Ready-Skills-720p.png) |
| 更新后选技 | [720p](../../../Logs/BlueGoldHud/Skills/Screens/02-Ready-Skills-720p.png)、[1080p](../../../Logs/BlueGoldHud/Skills/Screens/02-Ready-Skills-1080p.png)、[缺蓝](../../../Logs/BlueGoldHud/Skills/Screens/03-Insufficient-MP-720p.png)、[资源不可用](../../../Logs/BlueGoldHud/Skills/Screens/04-Unavailable-Skill-720p.png) |

## UI 自查

| 条目 | 自查证据 |
|---|---|
| U1–U2 布局和安全边距 | 720p 与 1080p 两档 16:9 截图。波次徽章顶部留边，和 HP、MP 分别占位，弹窗居中，卡片图标与文案完整 |
| U3–U4 状态和等待 | 选技样图覆盖空槽、正常、缺蓝、资源失效。波次测试覆盖通关、死亡、真实重载及注入的延迟状态 |
| U5–U6 点击区和反馈 | 选技槽 272×76，三选一卡 214×132，重开 198×59。PlayMode 用 EventSystem 射线点击检验，Sprite Swap 提供悬停、按下和禁用图。无独立延迟录屏 |
| U7 文案 | 沿用既有英文 UI 文案与格式 |
| U8 层级 | 交互 Canvas 与遮罩保持原有入口。图标和装饰线均不截获射线，真实点击用例检查交互和同帧普攻隔离 |
| U9 稳态开销 | 刷新条件仍由选择编号、可用性、波次及阶段变化触发。[选技 HUD 暖机后的 120 次托管采样](../../../Logs/BlueGoldHud/Skills/SkillUiSteadyAllocations.txt) 为 0 B |
| U10 关闭和退出 | 选技使用 F 或 Esc。奖励使用三张卡片，结算使用 Restart。对应真实输入及重载用例覆盖 |
