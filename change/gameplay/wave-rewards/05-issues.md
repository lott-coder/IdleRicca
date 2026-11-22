# 问题与验证记录 — gameplay/wave-rewards v0.0.1

## 执行中遇到的问题

| # | 现象 | 处理 | 状态 |
|---|---|---|---|
| 1 | 工作区没有 Git 元数据。 | 以源文件和哈希追踪变更，安装前核对 138 个源路径。既有覆盖文件按原路径备份。 | 已记录 |
| 2 | 某次目标 EditMode 已写出全绿 XML，随后 Unity 云服务关停挂起。 | 仅停止隔离工程所属进程。全量门禁重新运行，按新报告核对结果。 | 已解决 |
| 3 | 波次较高时生成列表原会在首次超过第 1 波上限时扩容。 | 采用已校验的整局最大上限预分配。 | 已解决 |
| 4 | 空波次资产在初始化时会被当作无效非空配置。 | 空表沿用已有生成，非空非法配置显性失败。 | 已解决 |
| 5 | 初始截图没有重载延迟状态。 | PlayMode 注入到期状态，验证提示、按钮禁用与拒绝重复重载，再走实际重载。 | 已解决 |

## 当前阻断

独立代码和 UI 复审均无阻断。安装前备份与安装后哈希已完成。

## 自动验证

| 检查 | 证据 | 结果 |
|---|---|---|
| L0 / G1–G2 | [Release-Compile.log](../../../Logs/WaveRewardsVerification/Release-Compile.log) | 隔离 Unity 2022.3.52f1c1 D3D11 编译退出 0，新轮无 C# error 或 warning。 |
| L1 / G3 | [基线](../../../Logs/WaveRewardsVerification/Baseline-EditMode.xml)、[红测](../../../Logs/WaveRewardsVerification/WaveRules-Red.xml)、[最终](../../../Logs/WaveRewardsVerification/Release-EditMode.xml) | 原 35 条全绿。新增 10 条在最小桩时如预期失败，最终 45/45。 |
| L2 / G6 | [基线](../../../Logs/WaveRewardsVerification/Baseline-PlayMode.xml)、[最终](../../../Logs/WaveRewardsVerification/Release-PlayMode.xml) | 原 11 条全绿。最终 13/13。包括出怪到结局的真实 Demo 路径、输入交接、死亡动画、同场景两种重开。 |
| G4 | 源输入 SHA-256、asmdef 白名单核对 | 运行时和测试程序集依赖沿用既有配置。 |
| G5 / G8 | [Release-VerifyAssets.log](../../../Logs/WaveRewardsVerification/Release-VerifyAssets.log) | Demo 和波次 prefab 的 Missing Script 为 0，存在唯一刷怪器、WaveHud 和 EventSystem，十波有效配置引用保存。 |
| G7 | [技术设计](02-design.md) | 本迭代没有存档变更，往返门禁不适用。 |
| 安装核验 | [installed-sha256.json](../../../Logs/WaveRewardsVerification/installed-sha256.json)、[安装输入](../../../Logs/WaveRewardsVerification/baseline/sources.json) | 29 个目标文件均与隔离项目的已验证内容一致。 |

## 界面验证

| 用例 | 操作路径 | 观察 | 证据 |
|---|---|---|---|
| 战斗开始 | 打开构建的首场景 | 顶部居中可见 1/10，现有 HP/MP 界面可读。 | [720p](../../../Logs/WaveRewardsVerification/Screens/Wave01-720p.png)、[1080p](../../../Logs/WaveRewardsVerification/Screens/Wave01-1080p.png) |
| 普通波奖励 | 清波并查看奖励 | 三个选项的类型和等级可读，属性增量和技能占位有区别。 | [720p](../../../Logs/WaveRewardsVerification/Screens/WaveReward-720p.png)、[1080p](../../../Logs/WaveRewardsVerification/Screens/WaveReward-1080p.png) |
| 最后一波与通关 | 清第 10 波并选择其奖励 | 最后奖励标题可见。随后保持 10/10，显示 Run Complete 和 Restart。 | [最后奖励](../../../Logs/WaveRewardsVerification/Screens/FinalReward-1080p.png)、[通关](../../../Logs/WaveRewardsVerification/Screens/RunComplete-1080p.png) |
| 死亡重开 | 第二波生命归零，等待动画完成 | 1/10 与 Run Over 显示，重开恢复初始 HP 与攻击。 | [720p](../../../Logs/WaveRewardsVerification/Screens/RunOver-720p.png)、[1080p](../../../Logs/WaveRewardsVerification/Screens/RunOver-1080p.png) |
| 已超过重载等待时间 | 注入模拟的到期状态 | 显示 Reload delayed 且重开按钮禁用，避免重复载入。 | [720p](../../../Logs/WaveRewardsVerification/Screens/RestartDelayed-720p.png)、[1080p](../../../Logs/WaveRewardsVerification/Screens/RestartDelayed-1080p.png) |

## 构建者 UI 清单

| # | 核对 |
|---|---|
| U1 | 两种分辨率实拍。波号、文字、按钮完整可见。 |
| U2 | Windows 场景顶部牌设计留边 13 px，中心面板不会贴边。 |
| U3 | 未就绪隐藏，战斗显示波号，奖励和两类结局显示对应面板。重载待处理时禁用按钮，异常时显示提示。 |
| U4 | 死亡动画兜底 12 秒，重载反馈阈值 15 秒。超时状态以 PlayMode 注入和两档截图验证。 |
| U5 | 奖励按钮 198×74，重开按钮 212×58，按 1280×720 缩放。 |
| U6 | L2 对实际 UGUI 射线命中、点击后同帧状态和输入拦截有断言。未录制真实显示延迟。 |
| U7 | 简易英文文案沿用 Demo HUD 的语言风格，由当前界面脚本提供，留待 UI 美术迭代处理本地化。 |
| U8 | 奖励与结果面板覆盖场景，UGUI 射线命中实际按钮。 |
| U9 | 显示值和阶段发生变化时更新 TMP 和节点，未采集真机 Profiler。 |
| U10 | 点击任一有效奖励继续。死亡或通关经 Restart 回到第 1 波。 |

## 未验证项

这组证据来自自动化 PlayMode 与渲染取图。真人试玩的节奏、真机性能基线和可见点击响应录屏未采集。
