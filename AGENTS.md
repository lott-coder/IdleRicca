# IdleRicca— 工程纪律（AGENTS.md 工作区）

> 适用宿主包括 Codex、DSH 和 ZCode。Claude Code 的项目规范可放在 `.claude/rules/`。
> **本文件是 AI 每天看到的规范浓缩版**——它与 `docs/rules/` 必须自洽；冲突时以 `docs/rules/` 为准，并回头修本文件。

---

## 项目上下文

- **引擎**：Unity 2022.3.52f1c1（3D，Built-in Render Pipeline）
- **语言/框架**：C#（`.asmdef` 四层：Domain / Application / Infrastructure / Presentation）
- **权威模式**：本地权威（单机，默认待确认）（见 `docs/rules/架构.md §2`）
- **数据**：暂定本地 JSON + schema 版本（默认待确认，存档功能尚未实现）；**存档载入失败必须显性失败，禁止默认值开局**
- **模块**：core / gameplay / data / economy / progression / ui / audio / _misc（gameplay 已确认，其余沿用初始配置）
- **门禁**以本机 PowerShell 执行 Unity 批处理命令为默认配置，日志和测试结果保存在 `Logs/`。命令见 `docs/rules/验证与门禁.md §3`。
- **真相源**：`change/`（这次改了什么）→ `spec/`（现在必须怎样，**只读**）→ `knowledge/`（怎么做/踩过什么坑）

---

## 规范索引（按需查阅，不要全量读入）

工具链文档中的 `开发规范/` 对应本项目的 `docs/rules/`。

| 主题 | 文件 |
|---|---|
| 分层 / 权威 / 通信 / 数据 / 模块划分 | `docs/rules/架构.md` |
| C# 命名、序列化、生命周期、禁用 API | `docs/rules/编码约定.md` |
| asmdef 依赖、资源引用、UPM、CI | `docs/rules/工程与资源.md` |
| 证据等级 / 质量分级 / 数值门禁 / UI 验收 | `docs/rules/验证与门禁.md` |
| 阶段门禁与红线 | `docs/rules/工作流执行.md` |
| 无人值守边界 | `docs/rules/无人值守安全.md` |
| 人机协作纪律 | `docs/rules/AI协作纪律.md` |
| 多会话独占资源 | `docs/rules/多会话与独占资源.md` |
| 批量删改 | `docs/rules/代码清理.md` |
| 上下文预算 | `docs/rules/上下文效率.md` |
| 技能/代理怎么用 | `docs/rules/技能与代理使用.md` |

> 每篇末行有 `<!-- 已读到文末: X -->`。声称"符合某规范"却报不出该标记 → 按"未读"处理。

---

## 技能索引（8 个，用 skill 工具按名加载）

| 技能 | 何时用 |
|---|---|
| `workflow` | 走完整迭代（init / plan / verify / validate / archive / status） |
| `project-setup` | 首次导入配置、改模块或权威模式 |
| `brainstorm-design` | 需求模糊，先澄清 |
| `brainstorm-develop` | 需求明确，定技术方案 |
| `knowledge-get` | 查某模块现状（spec + knowledge） |
| `knowledge-set` | 沉淀踩坑与设计理由 |
| `systematic-debugging` | Bug 难复现 |
| `verification-before-completion` | 准备说"完成/修好"之前 |

---

## 命令别名

Codex 用 `$project-setup`、`$workflow` 等技能入口。用户按下表输入斜杠写法时，将它作为对话别名转给对应技能，并保留参数。

| 用户说 | 动作 |
|---|---|
| `/workflow init <type> <module> <sub>` | 加载 `workflow`，args=`init …` |
| `/workflow plan [--prd <p>] [--direct] [--unattended]` | 加载 `workflow`，args=`plan …` |
| `/workflow verify <phase>` / `validate` / `status` | 同上 |
| `/workflow archive [sub]` | 同上 |
| `/setup` | 加载 `project-setup` |
| `/brainstorm-design <主题>` | 加载 `brainstorm-design` |
| `/brainstorm-develop <需求>` | 加载 `brainstorm-develop` |

---

## 子代理

在 Codex 中按角色名调用 `.codex/agents/` 里的项目子代理。角色说明位于 `docs/agents/`。DSH 可从部署的 `agents/` 目录读取相同的角色文件，并传给其子代理工具。

| 文件 | 角色 | 何时派 |
|---|---|---|
| `prd-reader.md` | 读 PRD（图/PDF）转结构化需求 | Phase 0 |
| `plan-reviewer.md` | 独立审需求/设计/计划 | Phase 1–3 后 |
| `code-reviewer.md` | 独立审 C# 变更 | Phase 4 后 |
| `unity-systems-engineer.md` | 分层/可测性/性能评估 | Phase 2（可选） |
| `experience-designer.md` | 留存/变现/体验评审 | Phase 1（可选） |

**审阅类 agent 的结论必须落盘**（`change/{module}/_tmp/04-review-report.md`），口头结论不算。

交互阶段用当前宿主可用的提问工具请用户确认。工具不可用时，在对话中展示选项并等待明确回复。

---

## 关键红线（Unity）

- ❌ `Presentation` 直接改状态（唯一写入口是 `Application`）
- ❌ 跨层反向依赖（由 asmdef 强制，不得用反射绕过）
- ❌ `Update`/`LateUpdate`/`FixedUpdate` 内分配（`new`/LINQ/字符串拼接/`GetComponent`）
- ❌ 外部输入未净化（NaN / 越界 / 非法 ID）
- ❌ 存档载入失败静默用默认值
- ❌ 改测试以通过门禁
- ❌ 未拿到新鲜证据就说"完成"

---

## 工作流速查

```
/workflow init patch <模块> <子分类>   → 建迭代
/workflow plan [--direct]               → Phase 0–3
/workflow verify <phase>                → 单独触发审核
/workflow validate                      → 归档前校验（C1–C8）
/workflow archive <子分类>              → 归档 + delta 合并进 spec/
```

**硬规则**：执行完整工作流时，每阶段遵循「产物 → 独立 agent 审核 → 用户确认」，缺一不可进下一阶段。`spec/` 只能由归档工具更新。

**所有项目改动都须归档**。直接实现、Bug 修复、资产、配置和文档调整，即使未启动 `workflow`，也须在交付完成前将变更、原因和验证证据归档到 `change/`。涉及规格变更时附增量。归档受阻时记录待归档状态。最小记录和并行处理方式见 [工作流执行](docs/rules/工作流执行.md)。

---

## 真相源与工具

- `change/{module}/_tmp/` 进行中 → `change/{module}/{sub}/` 已归档
- `spec/{module}.md` 只读；`knowledge/{module}.md` 可写
- 归档与校验由工具执行，**不要手工合并**：

```bash
python tools/spec_tool.py validate --module <m>
python tools/spec_tool.py archive --module <m> --version vX.Y.Z --subcategory <s> --apply
python tools/spec_tool.py check-modules --module <m> --rules-dir docs/rules
```

`spec/` 初始为空是正常的——第一轮 `archive` 后才会生成基线。
