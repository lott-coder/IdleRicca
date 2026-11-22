# 执行计划 - v{X.Y.Z}

> 模块: {module}
> 子分类: {subCategory}
> 日期: YYYY-MM-DD

## 1. 任务清单

> 每项必须标注：输出文件路径 / 层 / 需要的测试 / 是否涉及资产。

| # | 任务 | 输出文件 | 层 | 测试 | 资产 | 依赖 | 状态 |
|---|------|---------|----|------|------|------|------|
| 1 | 定义领域类型 | `Assets/Scripts/Domain/Economy/PriceRule.cs` | Domain | L1 | — | — | ☐ |
| 2 | 实现结算服务 | `Assets/Scripts/Application/Economy/EconomyService.cs` | Application | L1 | — | 1 | ☐ |
| 3 | 存档分区与迁移 | `Assets/Scripts/Infrastructure/Save/SaveData.cs` | Infrastructure | L1 | — | 1 | ☐ |
| 4 | 商店界面绑定 | `Assets/Scripts/Presentation/Shop/ShopPanel.cs` | Presentation | L2/L4 | prefab | 2 | ☐ |

**状态**：☐ 未开始 / ◐ 进行中 / ✓ 完成 / ✗ 受阻

## 2. 资产修改清单（需在 Unity 内手工完成）

> 这部分 **AI 不得直接改文件**，只能给步骤 + 由人在 Editor 内操作。

| # | 类型 | 资产路径 | 操作 | 验收方式 |
|---|------|---------|------|---------|
| A1 | ScriptableObject | `Assets/Config/ShopConfig.asset` | 新增 `price` 字段并填值 | Inspector 无报错 |
| A2 | Prefab | `Assets/Prefabs/Shop/ShopPanel.prefab` | 新增购买按钮并绑定 | 运行时可点 |
| A3 | Scene | `Assets/Scenes/Main.unity` | 挂载 ShopPanel | Play 可见 |

## 3. 依赖关系

```
1 → 2 → 4
1 → 3
```

## 4. 验证计划

| 阶段 | 动作 | 通过标准 |
|---|---|---|
| 每完成 1 项 | 跑 EditMode 相关用例 | 全绿 |
| 全部完成 | 跑 EditMode 全量 | 全绿 |
| 涉及表现 | L2 PlayMode 关键路径 + L4 截图 | 无报错、界面符合预期 |
| 归档前 | 门禁 G1–G8（见 `开发规范/验证与门禁.md` §3） | 全通过 |

## 5. 验收标准对照

| AC | 覆盖任务 | 证据 | 状态 |
|---|---|---|---|
| AC-1 | 2, 4 | L1 `Buy_Succeeds` | ☐ |

## 6. 回滚方案

- 代码：`git revert <commit>` 或丢弃工作区改动
- 存档：若已上线 schema 变更，保留旧版本读写兼容至少一个版本周期
- 资产：Editor 内操作需先在版本控制里打 tag，便于回退

## 7. 风险

| 风险 | 影响 | 应对 |
|---|---|---|

## 8. 任务动作与前置检查

> 来源：源包批次计划表（`arch-fix-batches-*.md`）。**每个任务必须有一个动作动词**，否则执行时容易"顺手多改"。

| 动作 | 含义 | 边界 |
|---|---|---|
| **加** | 新增文件/类型/配置 | 不动既有逻辑 |
| **修** | 改既有实现 | 不扩大范围 |
| **删** | 移除死代码/废弃项 | 先 grep 引用，再删 |
| **并** | 合并重复实现 | 合并后行为必须等价 |
| **迁** | 移动/重命名 | 保留历史（`git mv`），一次一个目录 |

**前置检查（动手前逐条确认）**：
- [ ] 基线已记录：编译通过、相关测试全绿
- [ ] 工作区干净（或已存档在途改动）
- [ ] 要改的符号已用 `grep` 确认全部引用点
- [ ] 涉及存档/序列化字段的，已确认迁移方案
- [ ] 涉及资产/场景的，已确认由人操作（AI 不改）

## 9. 每批收尾三件事

> 每完成一个可独立验证的批次（不是整个迭代），做这三件：

1. **新鲜验证证据**：贴本次的测试输出 / 截图 / Profiler 数字（见 `开发规范/验证与门禁.md` §2）
2. **更新知识**：若踩到坑或产生新约束，写进 `knowledge/{module}.md`（可延后到 Phase 5，但必须在 `05-issues.md` 记一行）
3. **独立提交**：一个批次一个 commit，message 写清"做了什么 + 证据 + 回退方式"（AI 不自动 commit，由人执行）

**为什么按批次而不是按迭代**：迭代可能持续数天，中间失败时若只有一个大 commit，回滚会连带丢掉已通过的部分。
