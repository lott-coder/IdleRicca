# `AI工具链/tools/` 可执行工具

> 本目录把"流程里最关键、最容易做错的几步"从**提示词约定**变成**可执行代码**。
> 没有它，`/workflow archive` 只是一份"请 AI 照做"的说明书；有它，合并是可测、可回滚的。

---

## spec_tool.py

三层真相源（`change/` → `spec/`）的确定性工具。使用 Python 3.7+ 标准库。部署到 Unity 项目后，从项目根运行下方命令。在工具链源码仓库运行时，脚本位于 `AI工具链/tools/spec_tool.py`。

| 子命令 | 作用 | 会改文件吗 |
|---|---|---|
| `validate` | 校验 `07-spec-delta.md` 与 `spec/{module}.md` 一致性（C1–C8） | 否 |
| `check-spec` | 只体检 spec 自身（C4 编号唯一 / C8 标题卫生） | 否 |
| `check-modules` | 校验 `架构.md §6` 和 `versions.json` 的模块声明，检查 `change/`、`knowledge/`、`spec/` 的孤儿模块 | 否 |
| `merge` | 计算 delta → spec 合并结果；默认 dry-run | 加 `--apply` 才写 |
| `archive` | 归档事务：加锁 → 校验 → 合并 → 移文档 → 更新索引 | 加 `--apply` 才写 |

### 用法

```bash
# 归档前校验（不改文件）
python tools/spec_tool.py validate --module economy

# 模块分类一致性（改了模块表之后必跑）
python tools/spec_tool.py check-modules --module core

# spec 存量体检（编号唯一 / 标题卫生）
python tools/spec_tool.py check-spec --module economy

# 看合并结果（dry-run，强烈建议先看）
python tools/spec_tool.py merge --module economy --version v0.2.0 --subcategory purchase

# 正式归档（原子写 + 失败回滚 + 并发锁）
python tools/spec_tool.py archive --module economy --version v0.2.0 --subcategory purchase --description "商店购买与退款" --type minor --apply
```

项目根不在当前目录时加 `--root <path>`。规格或归档用了自定义目录时加 `--spec-dir` 或 `--change-dir`。

`check-modules` 会查找 `开发规范/`、`.claude/rules/` 和 `docs/rules/` 中的 `架构.md`。找到多份或使用自定义布局时，传 `--rules-dir <path>` 指定目录。

### 退出码

| 码 | 含义 | 处理 |
|---|---|---|
| 0 | 通过 | 继续 |
| 1 | 校验失败 | 按输出的 C{n} 逐条修 delta，**不要**改 spec |
| 2 | 用法/环境错误 | 检查路径与参数 |
| 3 | 缺基线 / 锁冲突 | 见下 |

---

## 校验项

| ID | 检查 | 级别 |
|---|---|---|
| C1 | delta 每个 `##` 标题必须是 `ADDED/MODIFIED/REMOVED R{n} — 标题` | error |
| C2 | `MODIFIED/REMOVED` 引用的 R{n} 在 spec 中存在 | error |
| C3 | `ADDED` 的 R{n} 不与现存冲突 | error |
| C4 | spec 内 R{n} 唯一 | error |
| C5 | 每条 `ADDED/MODIFIED` 至少一个 `### 场景:` | error |
| C6 | 每个场景各有一条非空的「当…」条件行和「则…」结果行 | error |
| C7 | 「则」不得只落在表现层（启发式） | warning |
| C8 | **spec 标题不得残留 `ADDED/MODIFIED/REMOVED` 前缀** | error |
| C9 | 模块分类一致由 `check-modules` 校验。设计文档未记录「在途契约终检」时，`validate` 给出提示 | error（分类）/ warning（契约记录） |

> **C8 是源包遗留缺陷的体检项**：原工具链的活规格里真实出现过 `## ADDED R2 — …` 这种脏标题，说明"追加时须剥离操作前缀"这条从未被明确规定。本工具在合并时直接剥离，并用 C8 存量体检。

---

## 合并规则（与旧提示词版本的差异）

| 项 | 旧（提示词） | 现在（工具） |
|---|---|---|
| ADDED 标题 | 原样追加 → 得到 `## ADDED R3` | **剥离前缀**，写为 `## R3 — 标题` |
| MODIFIED 标题 | "保留标题"（未说谁的） | `## R{n} — 新标题（MODIFIED vX.Y.Z）` |
| MODIFIED 边界 | 未定义 | 从 `## R{n}` 到下一个 `## ` 或 EOF |
| 版本戳 | "刷新"（未定义格式） | **双戳**：`> 由 change/… 生成（日期）`（人读，与历史产物一致）+ `> 最后归档 vX.Y.Z @ 日期`（机器可 grep）；已有 spec 缺机器戳时自动补插 |
| 重复执行 | 会重复写入 / C3 报错 | 匹配归档版本、子分类、增量哈希及规格条目后，重跑可沿用已合并规格 |
| 中途失败 | 文档已移走、spec 未更新 | 先合并规格，再移动文档和更新版本索引。发生可捕获的异常时回滚 |
| 并发 | 两会话互相覆盖 | `change/.archive.lock` 对规格写入和归档互斥 |
| 编码 | 未考虑 BOM | `utf-8-sig` 读取；BOM 不再导致首条需求被跳过 |

---

## 缺基线（退出码 3）

首次归档需要已有规格基线或一份包含有效需求条目的增量。两者都缺时退出码为 3。

已有规格基线且本轮明确无行为变更时，给 `validate` 和 `archive` 加 `--no-delta`。工具仍校验现有规格。如果增量已存在，工具会要求先解决冲突。

---

## 预览、锁与回滚

- `archive` 和 `merge` 默认预览。只有 `--apply` 会写入。
- 正式操作共用 `change/.archive.lock`。文件可能保留，操作系统随进程退出释放文件锁。竞争中的写入返回 3，预览可继续。
- 归档发生可捕获的错误时，工具恢复原有文件和备份，移除本次创建的文件。回滚本身失败时会列出失败位置，并保留恢复副本。
- 工具会在规格头部记录增量哈希，并把增量文件放到最后归档。强制终止后，先核对 `_tmp/`、目标子分类及索引，再按同一组参数重跑。旧版生成且没有增量哈希的规格需要人工核对中断状态。

---

## 与源包 `archive.json` 的对照（迁移留痕）

源包的归档定义是 **9 个 step（提示词步骤）**。逐条对照如下——**先承认遗漏，再说明有意偏离**。

### 已补齐的 3 处（源包有、我第一版漏了）

| # | 源包要求 | 第一版问题 | 现在 |
|---|---|---|---|
| 1 | step 4：优化迭代时**合并 version.json（追加版本记录）** | 整体覆盖子分类 `version.json`，**历史 versions 被清空** | 读旧文件 → 按 version 去重追加 → 保留 description |
| 2 | step 6：模块**首次**建 spec 时，在 `spec/README.md` 索引**追加一行** | 完全没做，索引永远为空 | `update_spec_index()` 自动追加 `| 模块 | 规格 | 基线来源 |` 行 |
| 3 | step 6：spec 头部版本戳 `最后归档 v{X.Y.Z} @ {date}` | 只有描述性一行，不可机器检索 | 同时写 `> 由 change/... 生成` 与 `> 最后归档 vX @ date` |

### 有意偏离的 4 处（并说明理由）

| # | 源包做法 | 本工具做法 | 理由 |
|---|---|---|---|
| 1 | step 4 先移文档 → step 5 再校验 | **先合并 spec → 再移动文档** | 见下方"源包矛盾"：先移会让 delta 离开 `_tmp`，校验形同虚设；且中途失败不可恢复 |
| 2 | 靠 `versions.json` 里 `status=wip` 自动发现迭代 | 要求显式 `--module` / `--subcategory` | 自治模式下"自动发现"容易归档错模块；显式参数可审计 |
| 3 | 无并发保护 | `change/.archive.lock` | 多会话并行会互相覆盖 |
| 4 | 无幂等 | 检测已合并则跳过 | 合并成功但移档失败时的重跑会卡在 C3 |

### 源包自身的 4 处矛盾（照抄会继承缺陷）

| # | 矛盾 | 影响 |
|---|---|---|
| 1 | 描述说"从 `_tmp/` **移入**子分类"（step 4），step 5 却检查"若 `_tmp/07-spec-delta.md` 存在"，step 7 又"清空 `_tmp`" | 若 step 4 真是移入，step 5 的校验**永不触发**；若不清空，step 7 无意义 |
| 2 | step 6 规定戳为 `最后归档 v{X} @ {date}`，但真实产物（`10-pixel-beads/规格/economy.md`）写的是 `由 change/... 生成（日期）` | 规格与实现不一致，双方各写一套 |
| 3 | `verify.json` 追加标题为 `## 阶段: {phase}`，`plan-reviewer.md` 要求 `## 需求审核 / ## 设计审核 / ## 计划审核` | agent 按哪种写取决于它先读到谁 |
| 4 | `status.json` 查 `knowledge/{module}/`（目录），`knowledge-set` 写 `knowledge/{module}.md`（文件） | 状态查询永远显示"未同步" |

> 本工具统一采用**真实产物**的约定（矛盾 #2 取产物格式），并同时写机器可读戳，两边都不丢。
> 矛盾 #1 的处理方式即"有意偏离"第 1 条；#3/#4 在各自文档中已统一为 `## {阶段}审核` 与 `knowledge/{module}.md`。

---

## 尚存的自动化缺口（诚实声明）

| 项 | 现状 |
|---|---|
| 跨机器并发 | 锁只在单机生效 |
| 进程被强杀（无法捕获异常） | 可能留下部分归档，按「预览、锁与回滚」核对 |
| `UPROPERTY` / 序列化字段改名丢数据 | UE 侧仅人工检查项 |
| C7 权威端判定 | 词表启发式，可能误报 |

---

## 自测

在工具链源码仓库根目录运行回归测试。

```bash
python -m unittest discover -s tests -v
```
