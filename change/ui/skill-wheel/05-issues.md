# 右下角四向技能轮盘

## 原因和改动

用户确认将选技界面放到屏幕右下角，蓝金圆盘的上、右、下、左各显示一个技能。轮盘旁显示专属图标、名称、等级和提示。底部状态和关闭键使用两行排版。透明全屏层接收选技交互，战斗画面中央保持清晰。

- `Assets/Prefabs/UI/SkillSelectionHud.prefab` 使用右下锚点、四向按钮和圆形图标遮罩。
- `Assets/Art/UI/SkillWheel/` 提供圆盘、按钮状态、圆形遮罩和说明牌等精灵。
- `Assets/Scripts/Presentation/UI/SkillSelectionHud.cs` 在 Awake 给各按钮 Image 应用可选的透明度命中阈值。轮盘使用 0.2。

## 验证

| 检查 | 结果 |
|---|---|
| Unity 2022.3.52f1c1 编译与装配 | [Author.log](../../../Logs/SkillWheel/Author.log)，退出 0，确认预制体标识一致。本轮轮盘版无 C# 错误或警告 |
| EditMode | [63 项通过](../../../Logs/SkillWheel/EditMode.xml) |
| PlayMode | [19 项通过](../../../Logs/SkillWheel/PlayMode.xml)，含隔离工程的圆形点击测试。4 个中心命中，16 个透明角排除，选技全屏层 alpha 为 0 |
| 更新前后 | [原居中选技](../../../Logs/BlueGoldHud/Skills/Screens/02-Ready-Skills-720p.png) · [右下轮盘 720p](../../../Logs/SkillWheel/Screens/02-Ready-Skills-720p.png) · [右下轮盘 1080p](../../../Logs/SkillWheel/Screens/02-Ready-Skills-1080p.png) |
| 状态 | [空槽](../../../Logs/SkillWheel/Screens/01-Empty-Slots-720p.png) · [缺蓝](../../../Logs/SkillWheel/Screens/03-Insufficient-MP-720p.png) · [资源不可用](../../../Logs/SkillWheel/Screens/04-Unavailable-Skill-720p.png) |
| 工程安装 | [安装清单](../../../Logs/SkillWheel/installed-sha256.json)，19 个文件与隔离工程 SHA-256 一致 |
