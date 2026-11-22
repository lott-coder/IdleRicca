# UI 模块知识

> 模块 ui  
> 关联层 Application / Presentation  
> 关键类型 PlayerActions、PlayerCombatStatus、RiccaPlayer、CombatHud、SkillSelectionHud  
> 最后更新 2026-11-22

## 架构概述

战斗 HUD 复用 Ricca 的 Application 状态和受伤结算。敌人攻击使用同一个扣血入口，让血量与断连一起结算。UGUI 从只读快照刷新。场景实例通过序列化覆盖绑定同场景 Ricca，预制体中的控件引用随资产保存。

## 关键类型

| 类型 | 层 | 职责 |
|---|---|---|
| `PlayerActions`、`PlayerCombatStatus` | Application | 战斗结算与只读生命、连击快照 |
| `RiccaPlayer` | Presentation | 采集 Unity 游戏时间，提供就绪的快照 |
| `CombatHud` | Presentation | 绘制 UGUI 状态并播放命中反馈 |

## 接口与事件

- `IPlayerCommands.AdvanceCombatTime` 接收已缩放的绝对游戏秒数。EditMode 测试显式传入时间，PlayMode 测试在角色第一次正常采样后发送测试命令。
- `RiccaPlayer.TryGetCombatStatus` 完成就绪校验。HUD 用一次查询获得整组数值，避免在不同更新阶段读到不一致的生命和连击。

## 数据

战斗界面读取场景内运行状态。字号、颜色和边距来自 UGUI 预制体。使用已锁定的 TMP 标准字体及其依赖。

## 使用约束

- 在修改任一伤害生产端的执行顺序时，同时验证 HUD 绘制顺序。EnemyActor 在 LateUpdate 结算敌方命中，HUD 的默认执行顺序需要处于其后。
- 数字使用预分配字符缓冲，值变化时交给 TMP。连击计时在可见时通过左侧支点缩放剩余条，避免动态布局组重排。

## 踩过的坑

| 现象 | 根因 | 正确做法 |
|---|---|---|
| 同一批处理内调用 TMP 导入后仍读不到字体 | CLI 包读取在首次资产导入后仍未完成 | 先用 Unity `-importPackage` 单独导入锁定版本的 Essential Resources，再启动预制体生成。 |
| 重新加载 Demo 后，测试直接注入的两次命中被下一帧清空 | 正常游戏时钟继续前进，而测试在 Ricca 第一次 Update 前以 0 记录了命中 | 让角色完成首次 Update 再通过命令注入测试命中。 |
| `Camera.Render` 取图只有游戏场景 | Screen Space Overlay 不随相机渲染到 RenderTexture | 自动化取证时用同一相机临时捕获 Screen Space Camera，完成后恢复 Overlay、分辨率和缩放设置。 |

## 蓝条与技能可用状态

- 被动战斗 HUD 从同一组快照取得 HP 与 MP。TMP 数值共用预分配的字符缓冲，只在当前或最大值变化时重绘，比例条沿左侧支点缩放。
- 技能面板同时比较选择编号、可用槽位和蓝量门槛。资源在面板打开期间失效时，组编号可以不变，槽位仍需重绘。使用实际 UGUI 点击和两种分辨率截图核对满蓝、扣蓝、缺蓝与资源不可用的反馈。
