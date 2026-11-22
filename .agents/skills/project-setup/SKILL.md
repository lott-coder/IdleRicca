---
name: project-setup
description: 首次导入本工具链时的项目初始化。询问并落盘三处项目特定配置（模块分类 / 权威模式 / 门禁命令），同步写入项目主文件（CLAUDE.md / AGENTS.md）、架构.md 与 change/versions.json，最后跑 check-modules 自检。可重复运行（幂等）。
user-invocable: true
allowed-tools: AskUserQuestion, Read, Edit, Write, Glob, Grep, Bash
version: 1.0.0
---

# Project Setup 技能（首次导入初始化）

把本工具链导入新项目后**运行一次**。它做三件事：

1. 问清**只有你知道**的三个决策（模块分类 / 权威模式 / 门禁命令）
2. 把它落盘到该去的地方（`架构.md`、`change/versions.json`、`验证与门禁.md`、项目主文件）
3. 跑一次自检，告诉你还差什么

> 对应源包的 `/setup`（首次导入初始化），但对象从"回复称呼"换成"项目上下文"——迁移包里真正会导错的是这三处配置。

---

## 何时运行

- 新项目部署完本包之后的**第一步**
- 用户输入 `/setup` 时
- 项目要改模块分类 / 换权威模式时（可重复运行，幂等覆盖）

---

## 步骤 0 · 解析参数

| 参数 | 说明 |
|---|---|
| `/setup` | 走完整三问 |
| `/setup <a,b,c>` | 直接采用给定的模块名（逗号分隔），跳过第 1 问 |
| `--auto` | 全部采用推荐值，并在产物中标 **【默认待确认】** |

---

## 步骤 1 · 探测宿主与主文件

用 `Glob` / `Read` 探测项目根：

| 项目情况 | 目标主文件 |
|---|---|
| 存在 `AGENTS.md` | `AGENTS.md` |
| 存在 `CLAUDE.md` | `CLAUDE.md` |
| 两者都在 | **两者都写**，保持内容一致 |
| 都不在 | 有 `.agents/`、`.zcode/` 或 `.dsh/` → 建 `AGENTS.md`；否则建 `CLAUDE.md` |

根据当前宿主确定 `{rulesDir}`。Claude Code 使用 `.claude/rules`，Codex、ZCode、DSH 等使用 `docs/rules`。确认该目录的 `架构.md` 和 `验证与门禁.md`，以及项目根的 `change/versions.json` 和 `tools/spec_tool.py` 都已落位。缺任一时提示先按 `适配说明.md` 完成解包。

---

## 步骤 2 · 三问（逐问，一次一个）

用当前宿主可用的交互提问工具逐题收集答案。工具不可用时，把选项发到对话并等待回复。

### 问 1 · 模块分类

> 「你项目的业务模块有哪些？建议按业务域切，不要按技术层。」

| 选项 | 内容 |
|---|---|
| A（推荐） | 沿用默认 `core / data / economy / progression / ui / audio / _misc` |
| B | 我给自定义列表（用户填写） |
| C | 让我先看代码再建议（AI 扫 `Assets/Scripts` 或 `Source/` 后给 2–3 套方案） |

**判定标准**：一个模块应能用一句话讲清"负责游戏的哪一块"；只能讲清"它是个工具"的，不是模块（放 `Utility`/`Infrastructure`）。

### 问 2 · 权威模式

> 「这个项目是单机还是联机？」

| 选项 | 含义 |
|---|---|
| A 本地权威 | 单机/离线；`Application` 唯一写入口 |
| B 专用服务器权威 | 联机；服务端 `Application` 唯一写入口 |
| C 暂不确定 | 先按 A 落盘，标【待定】，架构文档留 TODO |

**不可混用**：混用会让"谁说了算"变成运行时问题（见 `架构.md §2`）。

### 问 3 · 门禁命令

> 「编译与测试分别怎么跑？」

| 选项 | 内容 |
|---|---|
| A（推荐） | 用包内默认：`Unity -quit -batchmode -projectPath . -logFile -` / `-runTests …` |
| B | 我给自定义命令（用户填写，逐条替换 G1–G8） |
| C | 暂时没有 CI | 保留默认命令并在文档标注「本地手动执行」 |

---

## 步骤 3 · 同步配置（三处，必须全改）

| # | 文件 | 改什么 |
|---|---|---|
| 1 | `{rulesDir}/架构.md` | §6 的模块表：替换为最终模块列表，**保持** `\| \`module\` \| 范围 \|` 两列格式（`check-modules` 靠它解析） |
| 2 | `change/versions.json` | `modules` 替换为同样列表，每项 `{"currentSubCategory": null, "status": "idle", "subCategories": {}}`；更新 `updatedAt` |
| 3 | `{rulesDir}/验证与门禁.md` | §3 的 G1–G8 命令替换为实际命令（没有 CI 则保留并加「本地手动」标注） |

**同时**：`架构.md §2` 的权威模式改为所选值；若选 C 则加 `> TODO(待定): 权威模式` 标记。

---

## 步骤 4 · 写入主文件「项目上下文」段（幂等）

在目标主文件**靠前位置**写入或整段替换：

```markdown
## 项目上下文（由 /setup 生成）

- **引擎**：Unity {版本}
- **权威模式**：{本地权威 / 专用服务器权威}{【待定】?}
- **分层**：Domain / Application / Infrastructure / Presentation（asmdef 强制）
- **数据**：本地 JSON + schema 版本（或：云端，见架构 §5）
- **模块**：{a / b / c / …}
- **门禁**：G1 编译 → `…`；G3 测试 → `…`
- **真相源**：`change/`（改了什么）→ `spec/`（现在必须怎样，只读）→ `knowledge/`（怎么做）
- **本工作流入口**：`/workflow init|plan|verify|archive`
- **规范目录**：`{rulesDir}/`。工具链文档中的 `开发规范/` 路径从这里查找。
```

**规则**：
- 已有同名段 → **整段替换**（不要产生两段）
- 双宿主项目 → `CLAUDE.md` 与 `AGENTS.md` 都写，保持一致
- 写在项目文件里（团队共享），**不写 AI 私有 memory**

---

## 步骤 5 · 自检

```bash
python tools/spec_tool.py check-modules --module {任一模块} --root . --rules-dir "{rulesDir}"
```

| 结果 | 含义 | 处理 |
|---|---|---|
| `[OK] 模块分类一致` | 三处已对齐 | 继续 |
| `[FAIL] … 不一致` | 架构表 / versions.json / 目录名有出入 | 按提示改，重跑 |
| `[WARN] 已登记但尚未建目录` | 正常（首次 init 才建） | 忽略 |

---

## 步骤 6 · 输出下一步

```
✅ 导入配置完成
   模块：{a / b / c …}
   权威：{…}
   门禁：{…}

下一步：
1. /workflow init patch <模块> <子分类>   # 建第一个迭代
2. /workflow plan --direct               # 跑完一轮
3. archive 后才会生成 spec/{模块}.md（此前 spec/ 为空是正常的）
```

---

## 注意

- **不替用户决定**：三问给出推荐与理由，但必须由用户选择；被跳过则按推荐落盘并标【默认待确认】
- **不猜模块**：用户说"你看着办"时，先扫代码给 2–3 套方案让用户选，不要直接落盘
- **不改代码**：本技能只改配置与文档，不碰 `Assets/`、`Source/`
- **可重复运行**：改模块或换权威模式后重跑即可，幂等覆盖
- **失败不静默**：任一处写入失败，必须明确报出文件与原因，并回滚该文件（先读后写，写前备份 `.bak`）
