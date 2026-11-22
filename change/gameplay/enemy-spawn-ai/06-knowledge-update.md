# 知识库更新 - gameplay v0.0.1

## 1. 更新的知识模块

| 文档 | 操作 | 原因 |
|---|---|---|
| knowledge/gameplay.md | 增量更新 | 记录场景名额交接、City 导航烘焙及近战几何的维护方式。 |

## 2. 变更详情

- 新增 EnemyPopulation、EnemyActions 及 Unity 适配器的职责和配置关系。
- 修正敌人归零时的受击体释放说明。模型可完成死亡动作，名额由实际销毁归还。
- 保存跨帧进度、启用 Trigger 物理形状、NavMesh 投影及 PathComplete 的排查方法。
- City 静态碰撞变化后，在打开的 Demo 中用项目 Editor 菜单更新导航资产。

## 3. 索引更新

gameplay 已在 knowledge/README.md 建立入口。

## 4. 未沉淀项

默认数值及行为契约交由两份规格增量。一次性安装与验证环境保留在本轮验证记录。

## 5. 规格增量确认

Gameplay 与 Core 规格增量均通过 C1–C8 并通过归档预览。

## 6. 归档信息

- gameplay/enemy-spawn-ai v0.0.1，ADDED 3、MODIFIED 2。
- core/city-enemy-startup v0.0.1，MODIFIED 1。

两项归档均由工具执行。已回查版本索引、工具归一化后的增量哈希与活规格。
