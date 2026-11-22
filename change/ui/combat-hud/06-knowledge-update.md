# 知识库更新 — ui v0.0.1

## 1. 更新的知识模块

| 文档 | 操作 | 原因 |
|---|---|---|
| `knowledge/ui.md` | 新增 | 记录战斗 HUD 的数据绑定、渲染时序、取证手法和字体导入经验 |
| `knowledge/README.md` | 追加索引 | 可按 UI 模块定位上述经验 |

## 2. 变更详情

- 整理 Ricca 状态快照的接入点及 HUD 晚于伤害生产端刷新的要求。
- 记录 TMP 资源分批导入、PlayMode 测试时钟初始化，以及 Overlay Canvas 的相机取图方法。
- 保留数值变化时才更新 TMP、计时条用局部缩放的表现策略。

## 3. 索引更新

- 已在 `knowledge/README.md` 追加 `ui`。

## 4. 未沉淀项

HP、MP 和连击规则由规格负责。测试数、截图和安装清单留在本次执行记录。

## 5. 规格增量确认

- `07-spec-delta.md` 包含 R10–R12，C1–C8 已校验通过。
- 增量文件 SHA-256 为 `a1a37dade0720c3e83f998170b7dcd37460e40933ba44b90481a540e8274dade`。

## 6. 归档信息

- 版本 `ui/combat-hud v0.0.1`，归档目录 `change/ui/combat-hud/`。
- 工具合并目标为 `spec/ui.md`，新增 3 条规格。
