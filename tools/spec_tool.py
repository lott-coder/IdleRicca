#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
spec_tool.py —— 三层真相源（change -> spec）的确定性工具

存在的理由：/workflow archive 的 delta 合并过去只是「提示词里的步骤」，
执行者是人或 AI，合并规则含糊会导致两类稳定缺陷：
  1) ADDED 条目的前缀被带进 spec，得到 "## ADDED R3" 这种脏标题；
  2) MODIFIED 之后看不出这条能力被改过。
以及三类工程隐患：无原子性、无并发锁、validate 无实现。

本脚本把 validate / merge / archive 变成可执行、可测试、失败可回滚的操作。

子命令
  validate     校验 _tmp/07-spec-delta.md 与 spec/{module}.md 的一致性（C1-C8）
  merge        计算 delta -> spec 合并结果；默认只预览，--apply 原子写入
  archive      归档事务：加锁 -> validate -> merge -> 移文档 -> 更新索引 -> 清空 _tmp
  check-spec   只体检 spec 自身（C4 / C8），用于存量基线

用法
  python spec_tool.py validate   --module economy
  python spec_tool.py merge      --module economy --version v0.2.0 --subcategory purchase [--apply]
  python spec_tool.py archive    --module economy --version v0.2.0 --subcategory purchase [--apply]
  python spec_tool.py check-spec --module economy

退出码
  0 通过    1 校验失败    2 用法/环境错误    3 缺基线或锁冲突
"""

import argparse
import errno
import hashlib
import json
import os
import re
import shutil
import sys
import tempfile
from collections import Counter
from datetime import date, datetime

if os.name == "nt":
    import msvcrt
else:
    import fcntl

try:  # 让 Windows 控制台按实际代码页输出，避免中文变乱码；仍以 utf-8 读写文件
    if os.name == "nt":
        import ctypes

        _cp = ctypes.windll.kernel32.GetConsoleOutputCP()
        _enc = {65001: "utf-8", 936: "gbk", 932: "cp932", 949: "cp949"}.get(_cp)
        if _enc:
            sys.stdout.reconfigure(encoding=_enc, errors="replace")
            sys.stderr.reconfigure(encoding=_enc, errors="replace")
    else:
        sys.stdout.reconfigure(errors="replace")
        sys.stderr.reconfigure(errors="replace")
except Exception:
    pass

SPEC_H2 = re.compile(r"^##\s+R(\d+)\s*[—–-]\s*(.+?)\s*$")
DELTA_H2 = re.compile(r"^##\s+(ADDED|MODIFIED|REMOVED)\s+R(0*[1-9][0-9]*)\s*[—–-]\s*(\S.*?)\s*$")
BAD_SPEC_H2 = re.compile(r"^##\s+(ADDED|MODIFIED|REMOVED)\s+R\d+")
ANY_H2 = re.compile(r"^##\s+")
SCEN = re.compile(r"^###\s*场景\s*[:：]")
ANY_H3 = re.compile(r"^###\s+")
SCEN_CLAUSE = re.compile(r"^\s*(?:[-*+]\s+)?([当则])\s*[:：]?\s*(\S.*?)\s*$")
STAMP = re.compile(r"^>\s*由\s*(?:change/|.+的 07-spec-delta\.md 归档合并生成（)")
DELTA_STAMP = "<!-- spec_tool delta sha256: "
RULES_DIRS = ("开发规范", ".claude/rules", "docs/rules")

CLIENT_WORDS = ("客户端", "UI", "界面", "UMG", "蓝图", "Widget", "显示", "本地")
AUTH_WORDS = ("服务端", "服务器", "HasAuthority", "Gameplay", "Application", "写入口", "权威")

LOCK_NAME = ".archive.lock"


# ---------------------------------------------------------------- 基础设施
def die(msg, code=2):
    print("[ERROR] " + msg)
    sys.exit(code)


def ok(msg):
    print("[OK]    " + msg)


def fail(msg):
    print("[FAIL]  " + msg)


def warn(msg):
    print("[WARN]  " + msg)


def read(path):
    # utf-8-sig：容忍 BOM。带 BOM 的文件若不剥离，首行标题正则失配，
    # 会导致整条需求被静默跳过（validate 假通过）。
    with open(path, "r", encoding="utf-8-sig") as f:
        return f.read()


def read_lines(path):
    return read(path).splitlines()


def write_atomic(path, text):
    """同目录临时文件 + os.replace，保证不会写出半个文件。"""
    d = os.path.dirname(os.path.abspath(path)) or "."
    os.makedirs(d, exist_ok=True)  # 首次归档时 spec/ 可能还不存在
    tmp = None
    try:
        with tempfile.NamedTemporaryFile(mode="w", dir=d, prefix=".tmp_", suffix=".tmp",
                                         delete=False, encoding="utf-8", newline="\n") as f:
            tmp = f.name
            f.write(text)
        if os.path.exists(path):
            shutil.copy2(path, path + ".bak")
        os.replace(tmp, path)
    finally:
        if tmp is not None and os.path.exists(tmp):
            os.remove(tmp)


class Section(object):
    def __init__(self, rid, title, body, raw_head):
        self.id = rid
        self.title = title
        self.body = body          # list[str]，不含标题行
        self.raw_head = raw_head  # 原始标题行


def split_sections(lines, pattern):
    """按 `## R{n}` / `## OP R{n}` 切段。返回 (preamble, [Section])。"""
    preamble, sections = [], []
    cur = None
    for line in lines:
        m = pattern.match(line)
        if m:
            if cur is not None:
                sections.append(cur)
            if pattern is SPEC_H2:
                cur = Section(int(m.group(1)), m.group(2), [], line)
            else:
                cur = Section(int(m.group(2)), m.group(3), [], line)
                cur.op = m.group(1)
            continue
        if cur is None:
            preamble.append(line)
        else:
            cur.body.append(line)
    if cur is not None:
        sections.append(cur)
    return preamble, sections


def scenario_blocks(body_lines):
    blocks, cur = [], None
    for line in body_lines:
        if SCEN.match(line):
            if cur is not None:
                blocks.append(cur)
            cur = [line]
        elif ANY_H3.match(line):
            if cur is not None:
                blocks.append(cur)
            cur = None
        elif cur is not None:
            cur.append(line)
    if cur is not None:
        blocks.append(cur)
    return blocks


SPEC_DIR = "spec"
CHANGE_DIR = "change"


def paths(root, module):
    return (
        os.path.join(root, CHANGE_DIR, module, "_tmp"),
        os.path.join(root, SPEC_DIR, module + ".md"),
        os.path.join(root, CHANGE_DIR, module, "version.json"),
    )


# ---------------------------------------------------------------- validate
def load_delta(tmp_dir):
    p = os.path.join(tmp_dir, "07-spec-delta.md")
    if not os.path.exists(p):
        return None
    return read_lines(p)


def check_delta_shape(lines):
    """C1：delta 里所有 `## ` 标题都必须是 ADDED/MODIFIED/REMOVED R{n}。"""
    errs = []
    for i, line in enumerate(lines, 1):
        if ANY_H2.match(line) and not DELTA_H2.match(line):
            errs.append("C1 第 %d 行标题不合法: %s" % (i, line.strip()))
    return errs


def read_iteration(root, module):
    tmp_dir, spec_path, _ = paths(root, module)
    if not os.path.isdir(tmp_dir):
        die("找不到迭代目录: " + tmp_dir, 2)
    spec_lines = read_lines(spec_path) if os.path.exists(spec_path) else None
    return load_delta(tmp_dir), spec_lines


def spec_errors(lines, sections):
    errors = []
    dup = sorted(rid for rid, count in Counter(s.id for s in sections).items() if count > 1)
    if dup:
        errors.append("C4 spec 内 R 编号重复: %s" % ", ".join("R%d" % d for d in dup))
    for i, line in enumerate(lines, 1):
        if BAD_SPEC_H2.match(line):
            errors.append("C8 spec 第 %d 行标题含操作前缀: %s" % (i, line.strip()))
    return errors


def validate_iteration(args, delta_lines, delta_secs, spec_lines, spec_secs, applied=False):
    if delta_lines is None and spec_lines is None:
        fail("C0 首次归档缺少基线和 07-spec-delta.md，请先补齐规格增量")
        return 3
    no_delta = getattr(args, "no_delta", False)
    if delta_lines is None and not no_delta:
        fail("C0 缺少 07-spec-delta.md（无行为变更时使用 --no-delta）")
        return 1
    if delta_lines is not None and no_delta:
        fail("C0 --no-delta 与已有 07-spec-delta.md 冲突")
        return 1

    errors = spec_errors(spec_lines or [], spec_secs)
    warnings = []
    if delta_lines is not None:
        errors.extend(check_delta_shape(delta_lines))
        if not delta_secs and not errors:
            errors.append("C1 delta 至少需要一条 ADDED、MODIFIED 或 REMOVED")

    ids_set = {s.id for s in spec_secs}
    seen = {}
    for s in delta_secs:
        if s.id in seen:
            errors.append("C1 delta 内重复定义 R%d（%s 与 %s）" % (s.id, seen[s.id], s.op))
        seen[s.id] = s.op

        if not applied and s.op in ("MODIFIED", "REMOVED") and s.id not in ids_set:
            errors.append("C2 %s R%d 在 spec 中不存在（防改/删幽灵需求）" % (s.op, s.id))
        if not applied and s.op == "ADDED" and s.id in ids_set:
            errors.append("C3 ADDED R%d 与现存 spec 冲突" % s.id)

        blocks = scenario_blocks(s.body)
        if s.op in ("ADDED", "MODIFIED") and not blocks:
            errors.append("C5 %s R%d 缺少 `### 场景:`" % (s.op, s.id))
        for heading, *body in blocks:
            clauses = [match for match in map(SCEN_CLAUSE.match, body)
                       if match and match.group(2) not in (":", "：")]
            labels = {m.group(1) for m in clauses}
            for label in ("当", "则"):
                if label not in labels:
                    errors.append("C6 R%d 场景缺少非空的「%s」行: %s" % (s.id, label, heading))
            then_part = " ".join(m.group(2) for m in clauses if m.group(1) == "则")
            hit_c = any(w in then_part for w in CLIENT_WORDS)
            hit_a = any(w in then_part for w in AUTH_WORDS)
            if hit_c and not hit_a:
                warnings.append("C7 R%d 的「则」疑似只落在表现层: %s" % (s.id, then_part))

    # C9（warning）：设计文档应记录"在途契约终检"（跨模块并行时的人工结论）
    tmp_dir, _, _ = paths(args.root, args.module)
    _design = os.path.join(tmp_dir, "02-design.md")
    if os.path.exists(_design) and "在途契约终检" not in read(_design):
        warnings.append("C9 02-design.md 缺少「在途契约终检」记录（无跨模块契约变更时可忽略）")

    for e in errors:
        fail(e)
    for w in warnings:
        warn(w)
    if errors:
        print("\n校验失败：%d 个 error / %d 个 warning" % (len(errors), len(warnings)))
        return 1
    if spec_lines is None:
        warn("spec/%s.md 不存在：本次归档将生成基线" % args.module)
    ok("C1-C8 通过（%d 条 delta 条目，%d 个 warning）" % (len(delta_secs), len(warnings)))
    return 0


def cmd_validate(args):
    delta_lines, spec_lines = read_iteration(args.root, args.module)
    _, delta_secs = split_sections(delta_lines or [], DELTA_H2)
    _, spec_secs = split_sections(spec_lines or [], SPEC_H2)
    return validate_iteration(args, delta_lines, delta_secs, spec_lines, spec_secs)


def cmd_check_spec(args):
    spec_path = os.path.join(args.root, SPEC_DIR, args.module + ".md")
    if not os.path.exists(spec_path):
        die("找不到 " + spec_path, 2)
    lines = read_lines(spec_path)
    _, secs = split_sections(lines, SPEC_H2)
    errs = spec_errors(lines, secs)
    for e in errs:
        fail(e)
    if errs:
        return 1
    ok("spec 体检通过（%d 条能力）" % len(secs))
    return 0


def cmd_check_modules(args):
    """校验模块声明和已有产物是否一致。"""
    dirs = (args.rules_dir,) if args.rules_dir else RULES_DIRS
    candidates = [os.path.join(args.root, directory, "架构.md") for directory in dirs]
    found = [path for path in candidates if os.path.isfile(path)]
    if not found:
        die("找不到架构.md，已检查: " + ", ".join(candidates))
    if len(found) > 1:
        die("找到多个架构.md，请用 --rules-dir 指定: " + ", ".join(found))
    section = re.search(r"^##\s+6[.．][^\n]*\n(.*?)(?=^##\s|\Z)", read(found[0]), re.M | re.S)
    table = set()
    if section:
        table = set(re.findall(r"^\|\s*`([A-Za-z0-9_-]+)`\s*\|", section.group(1), re.M))

    versions_path = os.path.join(args.root, CHANGE_DIR, "versions.json")
    gv = load_json(versions_path, {})
    if not isinstance(gv, dict) or not isinstance(gv.get("modules"), dict):
        die("全局版本索引缺少有效的 modules 对象: " + versions_path)
    declared = set(gv["modules"])

    ch_dir = os.path.join(args.root, CHANGE_DIR)
    change_mods = {d for d in os.listdir(ch_dir)
                   if os.path.isdir(os.path.join(ch_dir, d)) and not d.startswith(".")} if os.path.isdir(ch_dir) else set()

    kn_dir = os.path.join(args.root, "knowledge")
    knowledge_mods = {f[:-3] for f in os.listdir(kn_dir)
                      if f.endswith(".md") and not f.startswith("_") and f != "README.md"} if os.path.isdir(kn_dir) else set()

    sp_dir = os.path.join(args.root, SPEC_DIR)
    spec_mods = {f[:-3] for f in os.listdir(sp_dir)
                 if f.endswith(".md") and not f.startswith("_") and f != "README.md"} if os.path.isdir(sp_dir) else set()

    errs, warns = [], []
    if table and declared != table:
        errs.append("C9 架构.md §6 表 %s 与 versions.json %s 不一致" % (sorted(table), sorted(declared)))
    if not table:
        errs.append("C9 架构.md §6 未解析到模块表: " + found[0])
    for name, got in (("change/", change_mods), ("knowledge/", knowledge_mods), ("spec/", spec_mods)):
        extra = got - declared
        if extra:
            errs.append("C9 %s 下存在未登记模块: %s（先更新架构.md §6 与 versions.json）" % (name, ", ".join(sorted(extra))))
    missing = declared - change_mods
    if missing:
        warns.append("C9 已登记但尚未建目录的模块: %s（首次 init 时创建即可）" % ", ".join(sorted(missing)))

    for e in errs:
        fail(e)
    for w in warns:
        warn(w)
    if errs:
        return 1
    ok("模块分类一致（%d 个模块；change %d / knowledge %d / spec %d）"
       % (len(declared), len(change_mods), len(knowledge_mods), len(spec_mods)))
    return 0


# ---------------------------------------------------------------- merge
def stamp_prefix(args):
    return "> 由 %s/%s/%s %s 的 07-spec-delta.md 归档合并生成（" % (
        CHANGE_DIR, args.module, args.subcategory, args.version)


def matches_applied_sections(args, spec_secs, delta_secs):
    """校验已经合并的需求仍与增量一致。"""
    by_id = {s.id: s for s in spec_secs}
    for s in delta_secs:
        actual = by_id.get(s.id)
        if s.op == "REMOVED":
            if actual is not None:
                return False
            continue
        title = s.title + ("（MODIFIED %s）" % args.version if s.op == "MODIFIED" else "")
        if actual is None or actual.title != title:
            return False
        if "\n".join(actual.body).rstrip() != "\n".join(s.body).rstrip():
            return False
    return True


def build_merged(args, today, pre, secs, delta_secs, digest, exists):
    added = [s for s in delta_secs if s.op == "ADDED"]
    modified = {s.id: s for s in delta_secs if s.op == "MODIFIED"}
    removed = {s.id for s in delta_secs if s.op == "REMOVED"}

    out = []
    stamp = stamp_prefix(args) + today + "）。"
    # 源包 archive.json step 6 要求的机器可读归档戳（真实产物里没写，两者都保留）
    stamp2 = "> 最后归档 %s @ %s" % (args.version, today)
    readonly = "> 本文件是当前真相源（what）；只读——更新唯一入口是 /workflow archive 的 delta 合并。"

    if exists:
        pre = [line for line in pre if not line.startswith(DELTA_STAMP)]
        replaced = False
        for i, line in enumerate(pre):
            if STAMP.match(line):
                pre[i] = stamp
                replaced = True
                break
        if not replaced:
            pre.insert(1, stamp)
        hit2 = False
        for i, line in enumerate(pre):
            if line.startswith("> 最后归档 "):
                pre[i] = stamp2
                hit2 = True
                break
        if not hit2:
            pre.insert(pre.index(stamp) + 1, stamp2)
        pre.insert(pre.index(stamp2) + 1, digest)
        out.extend(pre)
    else:
        out.append("# %s 模块规格（活规格基线）" % args.module)
        out.append("")
        out.append(stamp)
        out.append(stamp2)
        out.append(digest)
        out.append(readonly)
        out.append("")
        out.append("---")
        out.append("")

    while out and out[-1].strip() == "":
        out.pop()

    def emit(section, title_suffix=""):
        out.append("")
        out.append("## R%d — %s%s" % (section.id, section.title, title_suffix))
        for line in section.body:
            out.append(line)
        while len(out) >= 2 and out[-1].strip() == "":
            out.pop()

    for s in secs:
        if s.id in removed:
            continue
        if s.id in modified:
            emit(modified.pop(s.id), "（MODIFIED %s）" % args.version)
        else:
            emit(s)

    for s in added:
        emit(s)

    out.append("")
    return "\n".join(out) + "\n"


def prepare_merge(args, today):
    delta_lines, spec_lines = read_iteration(args.root, args.module)
    _, delta_secs = split_sections(delta_lines or [], DELTA_H2)
    pre, secs = split_sections(spec_lines or [], SPEC_H2)
    delta_hash = hashlib.sha256("\n".join(delta_lines or []).encode("utf-8")).hexdigest()
    digest = DELTA_STAMP + delta_hash + " -->"
    same_archive = any(line.startswith(stamp_prefix(args)) for line in pre)
    applied = (bool(delta_secs) and same_archive and digest in pre
               and matches_applied_sections(args, secs, delta_secs))
    rc = validate_iteration(args, delta_lines, delta_secs, spec_lines, secs, applied=applied)
    if rc or delta_lines is None:
        return rc, None
    if applied:
        ok("该归档的增量已合并，保留现有规格")
        return 0, None
    if same_archive:
        fail("该子分类和版本已写入规格，但增量或规格条目不匹配；请核对归档状态")
        return 1, None
    return 0, build_merged(args, today, pre, secs, delta_secs, digest, spec_lines is not None)


def update_spec_index(write, root, module, sub, version, description, spec_path):
    """首次生成 spec 时在 spec/README.md 索引追加一行（源包 archive.json step 6 的要求）。"""
    idx = os.path.join(root, SPEC_DIR, "README.md")
    _, secs = split_sections(read_lines(spec_path), SPEC_H2)
    rng = ""
    if secs:
        lo, hi = min(s.id for s in secs), max(s.id for s in secs)
        rng = "，R%d" % lo if lo == hi else "，R%d-R%d" % (lo, hi)
    row = "| %s | [%s.md](%s.md) | change/%s/%s %s（%s%s） |" % (
        module, module, module, module, sub, version, description or sub, rng)

    if not os.path.exists(idx):
        write(idx, "# spec 索引\n\n"
              "> 活规格层（当前真相源）。禁止手改——唯一更新入口是 archive 的 delta 合并。\n\n"
              "| 模块 | 规格 | 基线来源 |\n|------|------|---------|\n" + row + "\n")
        return True

    lines = read_lines(idx)
    if any(re.match(r"^\|\s*%s\s*\|" % re.escape(module), l) for l in lines):
        return False
    last_tbl = max((i for i, l in enumerate(lines) if l.startswith("|")), default=None)
    if last_tbl is None:
        lines += ["", "| 模块 | 规格 | 基线来源 |", "|------|------|---------|", row]
    else:
        lines.insert(last_tbl + 1, row)
    write(idx, "\n".join(lines) + "\n")
    return True


def cmd_merge(args):
    today = args.date or date.today().isoformat()
    _, spec_path, _ = paths(args.root, args.module)
    if not args.apply:
        rc, text = prepare_merge(args, today)
        if text is not None:
            print("----- DRY RUN: 以下为合并结果（未写入） -----")
            print(text)
            print("----- 共 %d 行；加 --apply 才会写入 %s -----" % (len(text.splitlines()), spec_path))
        return rc

    lock = Lock(args.root)
    if not lock.acquire():
        return 3
    try:
        rc, text = prepare_merge(args, today)
        if text is not None:
            write_atomic(spec_path, text)
            ok("已写入 " + spec_path + "（旧版备份为 .bak）")
        return rc
    finally:
        lock.release()


# ---------------------------------------------------------------- archive
class Lock:
    def __init__(self, root):
        self.path = os.path.join(root, CHANGE_DIR, LOCK_NAME)
        self.file = None

    def acquire(self):
        self.file = open(self.path, "a+b")
        self.file.seek(0)
        try:
            if os.name == "nt":
                msvcrt.locking(self.file.fileno(), msvcrt.LK_NBLCK, 1)
            else:
                fcntl.flock(self.file.fileno(), fcntl.LOCK_EX | fcntl.LOCK_NB)
        except OSError as ex:
            self.release()
            if ex.errno not in (errno.EACCES, errno.EAGAIN, errno.EDEADLK):
                raise
            warn("另一个进程持有归档锁: " + self.path)
            return False
        return True

    def release(self):
        self.file.close()
        self.file = None


class ArchiveTransaction:
    def __init__(self):
        self.scratch = tempfile.mkdtemp(prefix="unity-spec-archive-")
        self.undo = []

    def ensure_dir(self, path):
        if not os.path.isdir(path):
            self.ensure_dir(os.path.dirname(path))
            os.mkdir(path)
            self.undo.append((os.rmdir, (path,)))

    def preserve(self, path):
        if os.path.lexists(path):
            if not os.path.isfile(path) or os.path.islink(path):
                raise OSError("归档只能覆盖普通文件: " + path)
            saved = os.path.join(self.scratch, str(len(self.undo)), os.path.basename(path))
            os.makedirs(os.path.dirname(saved))
            shutil.copy2(path, saved)
            self.undo.append((shutil.copy2, (saved, path)))
        else:
            self.undo.append((self.remove_new_file, (path,)))

    @staticmethod
    def remove_new_file(path):
        if os.path.lexists(path):
            os.remove(path)

    def write(self, path, text):
        self.ensure_dir(os.path.dirname(path))
        self.preserve(path)
        if os.path.exists(path):
            self.preserve(path + ".bak")
        write_atomic(path, text)

    def move(self, src, dst):
        if os.path.lexists(dst):
            if not os.path.isfile(src):
                raise OSError("归档目录同名，无法覆盖: " + dst)
            self.preserve(dst)
            self.preserve(dst + ".owbak")
            shutil.copy2(dst, dst + ".owbak")
        os.replace(src, dst)
        self.undo.append((os.replace, (dst, src)))

    def rollback(self):
        restored = True
        for operation, arguments in reversed(self.undo):
            try:
                operation(*arguments)
            except OSError as ex:
                restored = False
                fail("回滚失败 %s: %s" % (arguments[-1], ex))
        return restored

    def clear(self):
        try:
            shutil.rmtree(self.scratch)
        except OSError as ex:
            warn("无法清理归档临时副本 %s: %s" % (self.scratch, ex))


def load_json(path, default):
    if not os.path.exists(path):
        return default
    return json.loads(read(path))


def cmd_archive(args):
    root, module = args.root, args.module
    tmp_dir, spec_path, mod_ver = paths(root, module)
    sub = args.subcategory
    version = args.version
    today = args.date or date.today().isoformat()
    target = os.path.join(root, CHANGE_DIR, module, sub)

    if not os.path.isdir(tmp_dir):
        die("找不到迭代目录: " + tmp_dir)
    if not sub or not version or os.path.normcase(os.path.abspath(target)) == os.path.normcase(tmp_dir):
        die("归档必须指定有效的 --subcategory 和 --version")

    if not args.apply:
        rc, text = prepare_merge(args, today)
        if rc == 0:
            if text is not None:
                print(text)
            ok("DRY RUN: 将归档到 %s（未改动项目文件）" % target)
        return rc

    lock = Lock(root)
    if not lock.acquire():
        return 3

    try:
        rc, text = prepare_merge(args, today)
        if rc != 0:
            return rc
        return apply_archive(args, today, tmp_dir, spec_path, mod_ver, target, text)
    finally:
        lock.release()


def apply_archive(args, today, tmp_dir, spec_path, mod_ver, target, text):
    root, module = args.root, args.module
    sub, version = args.subcategory, args.version
    sub_ver_path = os.path.join(target, "version.json")
    gpath = os.path.join(root, CHANGE_DIR, "versions.json")
    transaction = ArchiveTransaction()
    keep_recovery = False

    try:
        # 写入前读取索引，并保留子分类的版本历史。
        old_sub = load_json(sub_ver_path, {})
        versions = old_sub.get("versions", [])
        if not any(v.get("version") == version for v in versions):
            versions.append({"version": version, "date": today,
                             "type": args.type or "minor",
                             "description": args.description or sub,
                             "status": "archived"})
        sub_ver = {
            "subCategory": sub,
            "parentModule": module,
            "currentVersion": version,
            "status": "archived",
            "description": args.description or old_sub.get("description") or sub,
            "versions": versions,
        }

        mv = load_json(mod_ver, {"module": module, "currentSubCategory": None,
                                 "status": "idle", "subCategories": {}})
        mv.setdefault("subCategories", {})[sub] = {
            "description": args.description or sub,
            "currentVersion": version,
            "status": "archived",
            "path": "change/%s/%s" % (module, sub),
        }
        mv["currentSubCategory"] = None
        mv["status"] = "idle"

        gv = load_json(gpath, {"schemaVersion": 1, "modules": {}})
        mod_entry = gv.setdefault("modules", {}).setdefault(
            module, {"currentSubCategory": None, "status": "idle", "subCategories": {}})
        mod_entry["currentSubCategory"] = None
        mod_entry["status"] = "idle"
        mod_entry.setdefault("subCategories", {})[sub] = {
            "currentVersion": version, "status": "archived",
            "path": "change/%s/%s" % (module, sub)}
        gv["updatedAt"] = today

        documents = [name for name in sorted(os.listdir(tmp_dir)) if not name.startswith(".")]
        delta_name = "07-spec-delta.md"

        if text is not None:
            transaction.write(spec_path, text)
            ok("spec 已合并: " + spec_path)
        if not args.no_delta and update_spec_index(
                transaction.write, root, module, sub, version, args.description, spec_path):
            ok("spec 索引已追加: " + os.path.join(SPEC_DIR, "README.md"))

        transaction.ensure_dir(target)
        for name in documents:
            if name != delta_name:
                transaction.move(os.path.join(tmp_dir, name), os.path.join(target, name))

        for path, data in ((sub_ver_path, sub_ver), (mod_ver, mv), (gpath, gv)):
            transaction.write(path, json.dumps(data, ensure_ascii=False, indent=2) + "\n")
        # 最后移动增量，之前若中断仍保留重跑所需的输入。
        if delta_name in documents:
            transaction.move(os.path.join(tmp_dir, delta_name), os.path.join(target, delta_name))

        ok("文档已归档到 %s（%d 项），版本索引已更新" % (target, len(documents)))
        ok("归档完成: %s/%s %s" % (module, sub, version))
        return 0

    except (Exception, KeyboardInterrupt) as ex:
        fail("归档失败，开始回滚: %s" % ex)
        keep_recovery = not transaction.rollback()
        if keep_recovery:
            fail("回滚未完成，保留恢复副本: " + transaction.scratch)
        else:
            ok("已回滚到归档前状态")
        return 2
    finally:
        if not keep_recovery:
            transaction.clear()


# ---------------------------------------------------------------- CLI
def main():
    ap = argparse.ArgumentParser(prog="spec_tool.py", description="三层真相源工具")
    sub = ap.add_subparsers(dest="cmd", required=True)

    def common(p):
        p.add_argument("--root", default=".", help="项目根（含 change/ spec/）")
        p.add_argument("--module", required=True)
        p.add_argument("--spec-dir", default="spec", help="活规格目录名（默认 spec）")
        p.add_argument("--change-dir", default="change", help="变更档案目录名（默认 change）")

    p = sub.add_parser("validate")
    common(p)
    p.add_argument("--no-delta", action="store_true", help="仅用于有规格基线且无行为变更的迭代")
    p.set_defaults(func=cmd_validate)

    p = sub.add_parser("check-spec")
    common(p)
    p.set_defaults(func=cmd_check_spec)

    p = sub.add_parser("check-modules")
    common(p)
    p.add_argument("--rules-dir", help="含架构.md 的规范目录，相对于项目根")
    p.set_defaults(func=cmd_check_modules)

    p = sub.add_parser("merge")
    common(p)
    p.add_argument("--version", required=True)
    p.add_argument("--subcategory", required=True)
    p.add_argument("--date", default=None)
    p.add_argument("--apply", action="store_true")
    p.set_defaults(func=cmd_merge)

    p = sub.add_parser("archive")
    common(p)
    p.add_argument("--version", required=True)
    p.add_argument("--subcategory", required=True)
    p.add_argument("--description", default=None)
    p.add_argument("--type", default="minor", choices=["major", "minor", "patch"])
    p.add_argument("--date", default=None)
    p.add_argument("--apply", action="store_true")
    p.add_argument("--no-delta", action="store_true", help="仅用于有规格基线且无行为变更的迭代")
    p.set_defaults(func=cmd_archive)

    args = ap.parse_args()
    args.root = os.path.abspath(args.root)

    global SPEC_DIR, CHANGE_DIR
    SPEC_DIR = args.spec_dir
    CHANGE_DIR = args.change_dir

    try:
        return args.func(args)
    except (ValueError, OSError) as ex:
        print("[ERROR] " + str(ex), file=sys.stderr)
        return 2


if __name__ == "__main__":
    sys.exit(main())
