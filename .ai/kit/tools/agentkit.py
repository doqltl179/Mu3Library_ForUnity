#!/usr/bin/env python3
"""agentkit: render, check, install, and update the agent kit.

Stdlib only; requires Python 3.11+ (tomllib).
Usage: python <kit-root>/tools/agentkit.py <command> [options]
Kit root is `.ai/kit/` in an installed project and the repository root in the kit's own repository.
The rules this tool enforces are owned by the wiki under core/wiki/; this file owns only the mechanics
and the adapter table (which files each AI tool reads).
"""
from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
from dataclasses import dataclass, field
from pathlib import Path

if sys.version_info < (3, 11):
    sys.exit("agentkit requires Python 3.11 or newer (tomllib).")
import tomllib  # noqa: E402

KIT_ROOT = Path(__file__).resolve().parent.parent
CORE = KIT_ROOT / "core"
GEN_MARK = "agentkit:generated"
PAYLOAD = ["core", "tools/agentkit.py", "VERSION", "CHANGELOG.md", "LICENSE"]
NAME_RE = re.compile(r"^[a-z0-9]+(?:-[a-z0-9]+)*$")
DATE_RE = re.compile(r"^\d{4}-\d{2}-\d{2}$")
LINK_RE = re.compile(r"(!?\[[^\]\n]*\]\()([^)\s]+)((?:\s+\"[^\"]*\")?\))")
REGION_RE = re.compile(r"(<!-- agentkit:begin (?P<name>[\w-]+) -->\n)(?P<body>.*?)(<!-- agentkit:end (?P=name) -->)", re.S)

# Adapter table: the single owner of which files each AI tool reads. Rendered into tool-adapters.md.
ADAPTERS: dict[str, dict[str, str | None]] = {
    "claude": {
        "entry": "CLAUDE.md (imports AGENTS.md)",
        "agents": ".claude/agents/{name}.md",
        "skills": ".claude/skills/{name}/",
        "rules": ".claude/rules/stack-{id}.md (paths frontmatter)",
    },
    "codex": {
        "entry": "AGENTS.md (read natively)",
        "agents": ".codex/agents/{name}.toml",
        "skills": ".agents/skills/{name}/",
        "rules": None,
    },
    "copilot": {
        "entry": ".github/copilot-instructions.md (points to AGENTS.md)",
        "agents": ".github/agents/{name}.agent.md",
        "skills": ".github/skills/{name}/ (only when neither claude nor codex is a target; Copilot also reads their skill folders)",
        "rules": ".github/instructions/stack-{id}.instructions.md (applyTo)",
    },
    "cursor": {
        "entry": "AGENTS.md (read natively)",
        "agents": None,
        "skills": None,
        "rules": ".cursor/rules/stack-{id}.mdc (globs)",
    },
    "gemini": {
        "entry": "GEMINI.md (imports AGENTS.md)",
        "agents": None,
        "skills": None,
        "rules": None,
    },
}
# Paths where AI tools look for instruction files. Files here that agentkit did not generate are reported,
# because tools load them alongside the kit's files. Rendered into tool-adapters.md.
INSTRUCTION_SURFACES = [
    "**/AGENTS.md", "**/CLAUDE.md", "**/GEMINI.md", ".cursorrules", ".windsurfrules",
    ".claude/agents/**", ".claude/commands/**", ".claude/rules/**", ".claude/skills/**",
    ".codex/agents/**", ".agents/skills/**", ".cursor/rules/**",
    ".github/copilot-instructions.md", ".github/agents/**", ".github/instructions/**",
    ".github/prompts/**", ".github/skills/**",
]
EDITORCONFIG_SECTION = (
    "\n# agentkit: kit, overlay, and generated agent files stay UTF-8 without a byte-order mark,\n"
    "# because frontmatter parsers misread a BOM before the opening ---.\n"
    "[{.ai/**,AGENTS.md,CLAUDE.md,GEMINI.md,.claude/**,.codex/**,.agents/**,.cursor/**,"
    ".github/agents/**,.github/instructions/**,.github/skills/**,.github/copilot-instructions.md}]\n"
    "charset = utf-8\n"
)
READ_ONLY_TOOLS = {
    "claude": "Edit, Write, NotebookEdit",
    "copilot": '["read", "search", "execute", "web"]',
}


# --------------------------------------------------------------------------- utilities

def read_text(path: Path) -> str:
    # utf-8-sig drops a byte-order mark that editors add under `charset = utf-8-bom`.
    return path.read_text(encoding="utf-8-sig").replace("\r\n", "\n")


def write_text(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(text)


def posix(path: str | Path) -> str:
    return str(path).replace("\\", "/")


def rel(path: Path, base: Path) -> str:
    return posix(os.path.relpath(path, base))


def file_hash(path: Path) -> str:
    data = path.read_bytes()
    try:
        data = data.decode("utf-8-sig").replace("\r\n", "\n").encode("utf-8")
    except UnicodeDecodeError:
        pass
    return hashlib.sha256(data).hexdigest()


def glob_re(pattern: str) -> re.Pattern[str]:
    out, i = "", 0
    while i < len(pattern):
        if pattern.startswith("**/", i):
            out, i = out + "(?:.*/)?", i + 3
        elif pattern.startswith("**", i):
            out, i = out + ".*", i + 2
        elif pattern[i] == "*":
            out, i = out + "[^/]*", i + 1
        elif pattern[i] == "?":
            out, i = out + "[^/]", i + 1
        else:
            out, i = out + re.escape(pattern[i]), i + 1
    return re.compile("^" + out + "$")


def today() -> dt.date:
    return dt.date.today()


# --------------------------------------------------------------------------- frontmatter

def _scalar(value: str) -> str:
    value = value.strip()
    if len(value) >= 2 and value[0] == value[-1] == '"':
        try:
            return json.loads(value)
        except json.JSONDecodeError:
            return value[1:-1]
    if len(value) >= 2 and value[0] == value[-1] == "'":
        return value[1:-1].replace("''", "'")
    return value


def _inline_list(value: str) -> list[str]:
    inner = value.strip()[1:-1]
    items, buf, quote = [], "", ""
    for ch in inner:
        if quote:
            buf += ch
            if ch == quote:
                quote = ""
        elif ch in "\"'":
            quote, buf = ch, buf + ch
        elif ch == ",":
            items.append(buf)
            buf = ""
        else:
            buf += ch
    if buf.strip():
        items.append(buf)
    return [_scalar(item) for item in items if item.strip()]


def parse_frontmatter(text: str) -> tuple[dict | None, str]:
    """Parse the flat YAML subset the kit uses: scalars, inline lists, and dash lists."""
    if not text.startswith("---\n"):
        return None, text
    end = text.find("\n---\n", 3)
    if end == -1:
        if text.endswith("\n---"):
            end = len(text) - 4
        else:
            return None, text
    block, body = text[4:end], text[end + 5:]
    meta: dict = {}
    key = None
    for line in block.split("\n"):
        if not line.strip() or line.lstrip().startswith("#"):
            continue
        item = re.match(r"^\s+-\s*(.*)$", line)
        if item and key is not None:
            if not isinstance(meta.get(key), list):
                meta[key] = []
            meta[key].append(_scalar(item.group(1)))
            continue
        match = re.match(r"^([A-Za-z_][\w-]*):\s*(.*)$", line)
        if not match:
            continue
        key, value = match.group(1), match.group(2)
        if value == "":
            meta[key] = []
        elif value.strip().startswith("["):
            meta[key] = _inline_list(value)
        else:
            meta[key] = _scalar(value)
    return meta, body


def yaml_str(value: str) -> str:
    return json.dumps(value, ensure_ascii=False)


# --------------------------------------------------------------------------- model

@dataclass
class Doc:
    path: Path
    meta: dict
    body: str
    origin: str  # "core" or "project"

    @property
    def name(self) -> str:
        return self.meta.get("name") or self.meta.get("id") or self.path.stem


@dataclass
class Report:
    errors: list[str] = field(default_factory=list)
    warnings: list[str] = field(default_factory=list)

    def error(self, msg: str) -> None:
        self.errors.append(msg)

    def warn(self, msg: str) -> None:
        self.warnings.append(msg)

    def emit(self) -> int:
        for msg in self.warnings:
            print(f"warning: {msg}")
        for msg in self.errors:
            print(f"error: {msg}")
        print(f"{len(self.errors)} error(s), {len(self.warnings)} warning(s)")
        return 1 if self.errors else 0


def parse_table(path: Path, name: str) -> list[list[str]]:
    lines = read_text(path).split("\n")
    marker = f"<!-- agentkit:table {name} -->"
    try:
        start = next(i for i, line in enumerate(lines) if line.strip() == marker)
    except StopIteration:
        raise SystemExit(f"agentkit: table '{name}' not found in {path}")
    rows: list[list[str]] = []
    for line in lines[start + 1:]:
        if not line.startswith("|"):
            if rows or line.strip():
                break
            continue
        cells = [c.strip().strip("`").strip() for c in line.strip().strip("|").split("|")]
        if all(set(c) <= set("-: ") for c in cells):
            continue
        rows.append(cells)
    return rows[1:]  # drop header


class Kit:
    """Everything loaded from the kit and one project overlay."""

    def __init__(self, project: Path):
        self.project = project
        self.kit_repo = project.resolve() == KIT_ROOT.resolve()
        self.overlay = project / ".ai" / "project"
        self.generated = project / ".ai" / "generated"
        self.kit_rel = "." if self.kit_repo else rel(KIT_ROOT, project)
        self.cli = f"python {posix(Path(self.kit_rel) / 'tools' / 'agentkit.py')}"
        self.version = read_text(KIT_ROOT / "VERSION").strip() if (KIT_ROOT / "VERSION").exists() else "0.0.0"
        spec = CORE / "wiki" / "authoring"
        self.departments = {r[0]: r[1] for r in parse_table(spec / "agent-spec.md", "departments")}
        self.tiers = [r[0] for r in parse_table(spec / "agent-spec.md", "tiers")]
        self.access = [r[0] for r in parse_table(spec / "agent-spec.md", "access")]
        self.categories = [r[0] for r in parse_table(spec / "skill-spec.md", "skill-categories")]
        self.kinds = {r[0]: r[1].rstrip("/").split("/")[-1] for r in parse_table(spec / "stack-spec.md", "stack-kinds")}
        self.cadence = {r[0]: int(r[1]) for r in parse_table(CORE / "wiki" / "evolution" / "freshness-policy.md", "cadence")}
        self.budgets = [(glob_re(r[0]), r[0], int(r[1]))
                        for r in parse_table(CORE / "wiki" / "operating-model" / "context-budget.md", "budgets")]
        self.profile = self._load_profile()
        self.agents = self._load_named(sorted((CORE / "agents").glob("*/*.md")), "core") \
            | self._load_named(sorted((self.overlay / "agents").glob("*.md")), "project")
        self.skills = self._load_named(sorted((CORE / "skills").glob("*/SKILL.md")), "core") \
            | self._load_named(sorted((self.overlay / "skills").glob("*/SKILL.md")), "project")
        self.stacks = self._load_named(sorted(p for p in (CORE / "stacks").glob("*/*.md")), "core")
        self.pages = [self._doc(p, "core") for p in [CORE / "START.md", *sorted((CORE / "wiki").rglob("*.md"))]] \
            + [self._doc(p, "project") for p in sorted((self.overlay / "wiki").rglob("*.md"))]

    # loading -------------------------------------------------------------
    @staticmethod
    def _doc(path: Path, origin: str) -> Doc:
        meta, body = parse_frontmatter(read_text(path))
        return Doc(path, meta or {}, body, origin)

    def _load_named(self, paths: list[Path], origin: str) -> dict[str, Doc]:
        docs: dict[str, Doc] = {}
        for path in paths:
            if path.name == "CATALOG.md":
                continue
            doc = self._doc(path, origin)
            docs[doc.name] = doc
        return docs

    def _load_profile(self) -> dict:
        defaults = tomllib.loads(read_text(CORE / "templates" / "project" / "profile.toml"))
        path = self.overlay / "profile.toml"
        user = tomllib.loads(read_text(path)) if path.exists() else {}
        self.user_profile = user
        self.default_profile = defaults

        def merge(base: dict, over: dict) -> dict:
            out = dict(base)
            for key, value in over.items():
                out[key] = merge(out[key], value) if isinstance(value, dict) and isinstance(out.get(key), dict) else value
            return out

        return merge(defaults, user)

    # selection -----------------------------------------------------------
    def _select(self, items: dict[str, Doc], section: str, group_key: str, report: Report | None) -> list[str]:
        conf = self.profile.get(section, {})
        chosen: set[str] = set()
        core_names = {n for n, d in items.items() if d.origin == "core"}
        for token in conf.get("enabled", []):
            if token == "*":
                chosen |= core_names
            elif token.startswith("@"):
                group = {n for n in core_names if items[n].meta.get(group_key) == token[1:]}
                if not group and report:
                    report.error(f"profile [{section}].enabled: no core item in group '{token}'")
                chosen |= group
            elif token in items:
                chosen.add(token)
            elif report:
                report.error(f"profile [{section}].enabled: unknown name '{token}'")
        for token in conf.get("disabled", []):
            if token not in items and report:
                report.error(f"profile [{section}].disabled: unknown name '{token}'")
            chosen.discard(token)
        chosen |= {n for n, d in items.items() if d.origin == "project"}
        return sorted(chosen)

    def active_agents(self, report: Report | None = None) -> list[str]:
        return self._select(self.agents, "agents", "department", report)

    def active_skills(self, report: Report | None = None) -> list[str]:
        return self._select(self.skills, "skills", "category", report)

    def bindings(self) -> dict[str, dict]:
        return self.profile.get("bindings", {})

    def active_stacks(self) -> list[str]:
        ids = set(self.profile.get("stacks", {}).get("active", []))
        for binding in self.bindings().values():
            ids |= set(binding.get("stacks", []))
        return sorted(i for i in ids if i in self.stacks)

    def stack_paths(self, stack_id: str) -> list[str]:
        override = self.profile.get("stack_paths", {}).get(stack_id)
        return list(override) if override is not None else list(self.stacks[stack_id].meta.get("applies_to") or [])

    def targets(self) -> list[str]:
        return [t for t in self.profile.get("tools", {}).get("targets", []) if t in ADAPTERS]


# --------------------------------------------------------------------------- rendering helpers

def rewrite_links(text: str, src_dir: Path, project: Path, dest_dir: Path | None = None,
                  keep_within: Path | None = None) -> str:
    """Rewrite relative links for a copy of `text` placed elsewhere.

    Links into `keep_within` (a folder copied as a whole) stay unchanged; every other link becomes
    project-root-relative, which is how agents resolve paths. `dest_dir` is accepted for call symmetry.
    """
    def repl(match: re.Match[str]) -> str:
        target = match.group(2)
        if target.startswith(("#", "/", "<")) or re.match(r"^[a-zA-Z][a-zA-Z0-9+.-]*:", target):
            return match.group(0)
        path_part, hash_, anchor = target.partition("#")
        absolute = Path(os.path.normpath(src_dir / path_part))
        if keep_within is not None and _within(absolute, keep_within):
            return match.group(0)  # the folder is copied as a whole, so in-folder links stay valid
        new = rel(absolute, project)
        return f"{match.group(1)}{new}{hash_}{anchor}{match.group(3)}"

    out, fence = [], False
    for line in text.split("\n"):
        if line.lstrip().startswith("```"):
            fence = not fence
        out.append(line if fence else LINK_RE.sub(repl, line))
    return "\n".join(out)


def _within(path: Path, root: Path) -> bool:
    try:
        Path(os.path.normpath(path)).relative_to(Path(os.path.normpath(root)))
        return True
    except ValueError:
        return False


def md_marker(kit: Kit, sources: list[str]) -> str:
    return (f"<!-- {GEN_MARK} from {', '.join(sources)}. Do not edit: change the source, then run "
            f"`{kit.cli} sync`. Paths are relative to the project root. -->")


def strip_h1(body: str) -> tuple[str, str]:
    lines = body.lstrip("\n").split("\n")
    if lines and lines[0].startswith("# "):
        return lines[0][2:].strip(), "\n".join(lines[1:]).lstrip("\n")
    return "", "\n".join(lines)


def demote_headings(body: str) -> str:
    out, fence = [], False
    for line in body.split("\n"):
        if line.lstrip().startswith("```"):
            fence = not fence
        out.append("#" + line if not fence and re.match(r"^#{1,5} ", line) else line)
    return "\n".join(out)


def param_text(value: object) -> str:
    """Show a profile value in one table cell."""
    if isinstance(value, bool):
        return "true" if value else "false"
    if isinstance(value, dict):
        return "; ".join(f"{k} → {param_text(v)}" for k, v in value.items())
    if isinstance(value, list):
        return ", ".join(str(v.get("name", v)) if isinstance(v, dict) else str(v) for v in value)
    return str(value)


def table_cell(text: str) -> str:
    return " ".join(str(text).split()).replace("|", "\\|")


# --------------------------------------------------------------------------- rendering

class Renderer:
    def __init__(self, kit: Kit):
        self.kit = kit
        self.p = kit.project
        self.files: dict[str, str | bytes] = {}
        self.regions: dict[Path, dict[str, str]] = {}

    def src(self, path: Path) -> str:
        return rel(path, self.p)

    def add(self, relpath: str, content: str | bytes) -> None:
        self.files[posix(relpath)] = content

    def render(self) -> "Renderer":
        kit = self.kit
        self.add("AGENTS.md", self.agents_md())
        targets = kit.targets()
        if "claude" in targets:
            self.add("CLAUDE.md", f"{md_marker(kit, ['AGENTS.md'])}\n\n@AGENTS.md\n")
        if "gemini" in targets:
            self.add("GEMINI.md", f"{md_marker(kit, ['AGENTS.md'])}\n\n@./AGENTS.md\n")
        if "copilot" in targets:
            self.add(".github/copilot-instructions.md",
                     f"{md_marker(kit, ['AGENTS.md'])}\n\nRead `AGENTS.md` at the repository root before any other "
                     "action. It is the single entry point for this repository's agent instructions; this file "
                     "only points to it.\n")
        for name in kit.active_agents():
            self.render_agent(name, targets)
        skill_roots = []
        if "claude" in targets:
            skill_roots.append(".claude/skills")
        if "codex" in targets:
            skill_roots.append(".agents/skills")
        if "copilot" in targets and not skill_roots:
            skill_roots.append(".github/skills")
        for name in kit.active_skills():
            for root in skill_roots:
                self.render_skill(name, root)
        for stack_id in kit.active_stacks():
            self.render_stack_rules(stack_id, targets)
        for page in kit.pages:
            if page.origin == "project" and page.meta.get("applies_to"):
                slug = page.path.stem.lower()
                self.render_path_rule(f"project-{slug}", page.meta.get("owns", slug), page, list(page.meta["applies_to"]),
                                      targets)
        self.add(".ai/generated/catalog.md", self.catalog_md())
        self.add(".ai/generated/manifest.json", "")  # placeholder; filled after all files are known
        self.index_regions()
        if kit.kit_repo:
            self.kit_catalogs()
            adapters_page = CORE / "wiki" / "integration" / "tool-adapters.md"
            self.regions.setdefault(adapters_page, {})["adapters"] = self.adapter_table()
            self.regions[adapters_page]["surfaces"] = "".join(f"- `{p}`\n" for p in INSTRUCTION_SURFACES)
        manifest = {"_generated": f"{GEN_MARK}; do not edit", "kit_version": kit.version,
                    "files": sorted(k for k in self.files if k != ".ai/generated/manifest.json")}
        self.files[".ai/generated/manifest.json"] = json.dumps(manifest, indent=2) + "\n"
        return self

    # entry --------------------------------------------------------------
    def agents_md(self) -> str:
        kit = self.kit
        start = CORE / "START.md"
        _, body = parse_frontmatter(read_text(start))
        body = rewrite_links(body.strip("\n"), start.parent, self.p)
        prof = kit.profile
        project = prof.get("project", {})
        lines = [md_marker(kit, [self.src(start), ".ai/project/profile.toml"]), "", body, "", "## This Project", ""]
        title = project.get("name") or self.p.name
        summary = project.get("summary") or "(no summary in `.ai/project/profile.toml`)"
        kit_where = ("this repository is the kit itself; kit root is the repository root" if kit.kit_repo
                     else f"`{kit.kit_rel}/` (read-only here)")
        lines += [
            f"- Project: **{title}** — {summary}",
            f"- Kit: version {kit.version}, {kit_where}. CLI: `{kit.cli} <sync|check|freshness|new|update>`",
            "- Overlay: `.ai/project/` · Owners and skills: `.ai/generated/catalog.md` · "
            "Project wiki: `.ai/project/wiki/README.md` · Lessons: `.ai/project/lessons.md`",
            f"- Human-facing language: `{project.get('language', 'en')}`",
            "",
        ]
        guardrails = [g for g in project.get("guardrails", []) if str(g).strip()]
        if guardrails:
            lines += ["### Project Rules", "", *[f"- {g}" for g in guardrails], ""]
        lines += ["### Commands", ""]
        commands = prof.get("commands", {})
        present = {k: v for k, v in commands.items() if v}
        missing = [k for k, v in commands.items() if not v]
        if present:
            lines += ["| Key | Command |", "|---|---|"]
            lines += [f"| `commands.{k}` | `{table_cell(v)}` |" for k, v in present.items()]
        if missing:
            lines += ["", f"Not available (report the gap, do not guess): {', '.join(f'`{k}`' for k in missing)}"]
        lines += ["", "### Parameters", "", "| Key | Value |", "|---|---|"]
        for section in ("policy", "hosting", "docs", "release"):
            for key, value in prof.get(section, {}).items():
                lines.append(f"| `{section}.{key}` | {table_cell(param_text(value)) or '—'} |")
        return "\n".join(lines).rstrip() + "\n"

    # agents -------------------------------------------------------------
    def agent_body(self, name: str) -> str:
        kit = self.kit
        doc = kit.agents[name]
        parts = []
        base_name = doc.meta.get("extends")
        if base_name and base_name in kit.agents:
            base = kit.agents[base_name]
            title, local = strip_h1(doc.body)
            parts.append(f"# {title or name}\n\n" + rewrite_links(local.strip("\n"), doc.path.parent, self.p))
            parts.append("## Scope\n\n"
                         f"This role narrows `{base_name}`. It owns only what «Owns» above lists; the base role below "
                         "still supplies the boundaries («Does Not Own»), «Domain Checks», and «Skills».")
            base_title, base_body = strip_h1(base.body)
            parts.append(f"## Base Role: {base_title or base_name}\n\n"
                         + demote_headings(rewrite_links(base_body.strip("\n"), base.path.parent, self.p)))
        else:
            parts.append(rewrite_links(doc.body.strip("\n"), doc.path.parent, self.p))
        wiki = CORE / "wiki" / "operating-model"
        parts.append("## Protocol\n\n"
                     f"- Act under the role protocol in [delegation.md]({rel(wiki / 'delegation.md', self.p)}) "
                     f"and return results in the shape defined in [handoff-contract.md]({rel(wiki / 'handoff-contract.md', self.p)}).\n"
                     "- Project facts, commands, and parameters are in `AGENTS.md` «This Project».")
        binding = kit.bindings().get(name, {})
        lines = ["## Project Binding", ""]
        paths = binding.get("paths", [])
        lines.append("- Paths: " + (", ".join(f"`{p}`" for p in paths) if paths
                                     else "none bound in `.ai/project/profile.toml`; confirm the scope with the caller."))
        stacks = [s for s in binding.get("stacks", []) if s in kit.stacks]
        if stacks:
            lines.append("- Stack packs (read before editing): "
                         + ", ".join(f"[{s}]({rel(kit.stacks[s].path, self.p)})" for s in stacks))
        commands = kit.profile.get("commands", {})
        bound = [c for c in binding.get("commands", []) if commands.get(c)]
        if bound:
            lines.append("- Commands for this role: " + ", ".join(f"`commands.{c}` (`{commands[c]}`)" for c in bound))
        if binding.get("notes"):
            lines.append(f"- Notes: {binding['notes']}")
        parts.append("\n".join(lines))
        return "\n\n".join(parts) + "\n"

    def render_agent(self, name: str, targets: list[str]) -> None:
        kit = self.kit
        doc = kit.agents[name]
        meta = dict(doc.meta)
        if meta.get("extends") in kit.agents:
            base_meta = kit.agents[meta["extends"]].meta
            for key in ("tier", "access"):
                meta.setdefault(key, base_meta.get(key))
        sources = [self.src(doc.path)]
        if meta.get("extends") in kit.agents:
            sources.insert(0, self.src(kit.agents[meta["extends"]].path))
        sources.append(".ai/project/profile.toml")
        marker = md_marker(kit, sources)
        body = self.agent_body(name)
        desc = meta.get("description", "")
        read_only = meta.get("access") == "read-only"
        tier = meta.get("tier", "")
        models = kit.profile.get("models", {})
        if "claude" in targets:
            fm = ["---", f"name: {name}", f"description: {yaml_str(desc)}"]
            if models.get("claude", {}).get(tier):
                fm.append(f"model: {models['claude'][tier]}")
            if read_only:
                fm.append(f"disallowedTools: {READ_ONLY_TOOLS['claude']}")
            self.add(f".claude/agents/{name}.md", "\n".join(fm + ["---", marker, "", body]))
        if "copilot" in targets:
            fm = ["---", f"name: {name}", f"description: {yaml_str(desc)}"]
            if read_only:
                fm.append(f"tools: {READ_ONLY_TOOLS['copilot']}")
            if models.get("copilot", {}).get(tier):
                fm.append(f"model: {yaml_str(models['copilot'][tier])}")
            self.add(f".github/agents/{name}.agent.md", "\n".join(fm + ["---", marker, "", body]))
        if "codex" in targets:
            lines = [f"# {GEN_MARK} from {', '.join(sources)}. Do not edit: change the source, then run `{kit.cli} sync`.",
                     f"name = {json.dumps(name)}", f"description = {json.dumps(desc, ensure_ascii=False)}"]
            if models.get("codex", {}).get(tier):
                lines.append(f"model = {json.dumps(models['codex'][tier])}")
            if read_only:
                lines.append('sandbox_mode = "read-only"')
            if "'''" in body:
                escaped = body.replace("\\", "\\\\").replace('"""', '\\"\\"\\"')
                lines.append(f'developer_instructions = """\n{escaped}"""')
            else:
                lines.append(f"developer_instructions = '''\n{body}'''")
            self.add(f".codex/agents/{name}.toml", "\n".join(lines) + "\n")

    # skills -------------------------------------------------------------
    def render_skill(self, name: str, root: str) -> None:
        doc = self.kit.skills[name]
        src_dir = doc.path.parent
        dest_dir = self.p / root / name
        for path in sorted(src_dir.rglob("*")):
            if not path.is_file():
                continue
            relpath = f"{root}/{name}/{rel(path, src_dir)}"
            if path.name == "SKILL.md":
                fm = ["---", f"name: {name}", f"description: {yaml_str(doc.meta.get('description', ''))}", "---"]
                body = rewrite_links(doc.body.strip("\n"), src_dir, self.p, dest_dir, src_dir)
                self.add(relpath, "\n".join(fm + [md_marker(self.kit, [self.src(doc.path)]), "", body]) + "\n")
            elif path.suffix == ".md":
                text = rewrite_links(read_text(path), path.parent, self.p, dest_dir / rel(path.parent, src_dir), src_dir)
                self.add(relpath, text)
            else:
                self.add(relpath, path.read_bytes())

    # stacks -------------------------------------------------------------
    def render_stack_rules(self, stack_id: str, targets: list[str]) -> None:
        doc = self.kit.stacks[stack_id]
        paths = self.kit.stack_paths(stack_id)
        if paths:
            self.render_path_rule(f"stack-{stack_id}", doc.meta.get("title", stack_id) + " conventions", doc, paths,
                                  targets)

    def render_path_rule(self, rule_id: str, description: str, doc: Doc, paths: list[str], targets: list[str]) -> None:
        sources = [self.src(doc.path)] + ([".ai/project/profile.toml"] if doc.origin == "core" else [])
        marker = md_marker(self.kit, sources)
        body = rewrite_links(doc.body.strip("\n"), doc.path.parent, self.p) + "\n"
        if "claude" in targets:
            fm = ["---", "paths:"] + [f"  - {yaml_str(p)}" for p in paths] + ["---"]
            self.add(f".claude/rules/{rule_id}.md", "\n".join(fm + [marker, "", body]))
        if "copilot" in targets:
            fm = ["---", f"applyTo: {yaml_str(','.join(paths))}", "---"]
            self.add(f".github/instructions/{rule_id}.instructions.md", "\n".join(fm + [marker, "", body]))
        if "cursor" in targets:
            fm = ["---", f"description: {yaml_str(description)}", f"globs: {','.join(paths)}", "alwaysApply: false", "---"]
            self.add(f".cursor/rules/{rule_id}.mdc", "\n".join(fm + [marker, "", body]))

    # catalogs -----------------------------------------------------------
    def catalog_md(self) -> str:
        kit = self.kit
        active = kit.active_agents()
        lines = [md_marker(kit, ["core/", ".ai/project/"]), "", "# Project Catalog", "",
                 "Active owners, skills, and stack packs for this project. Choose an owner with "
                 f"[routing.md]({rel(CORE / 'wiki' / 'operating-model' / 'routing.md', self.p)}).", "", "## Owners", ""]
        for dept, plane in kit.departments.items():
            names = [n for n in active if kit.agents[n].meta.get("department") == dept]
            if not names:
                continue
            lines += [f"### {dept} ({plane})", "", "| Owner | Use when | Binding |", "|---|---|---|"]
            for n in names:
                doc = kit.agents[n]
                b = kit.bindings().get(n, {})
                bound = "; ".join(x for x in [", ".join(f"`{p}`" for p in b.get("paths", [])),
                                              ", ".join(b.get("stacks", []))] if x) or "—"
                tag = f" (extends `{doc.meta['extends']}`)" if doc.meta.get("extends") else ""
                lines.append(f"| [`{n}`]({rel(doc.path, self.p)}){tag} | {table_cell(doc.meta.get('description', ''))} | {table_cell(bound)} |")
            lines.append("")
        inactive = sorted(n for n in kit.agents if n not in active)
        if inactive:
            lines += ["Inactive core owners (enable in `.ai/project/profile.toml` when work needs them): "
                      + ", ".join(f"`{n}`" for n in inactive), ""]
        lines += ["## Skills", ""]
        skills = kit.active_skills()
        for cat in kit.categories:
            names = [n for n in skills if kit.skills[n].meta.get("category") == cat]
            if not names:
                continue
            lines += [f"### {cat}", "", "| Skill | Use when |", "|---|---|"]
            lines += [f"| [`{n}`]({rel(kit.skills[n].path, self.p)}) | {table_cell(kit.skills[n].meta.get('description', ''))} |" for n in names]
            lines.append("")
        lines += ["## Stack Packs", ""]
        stacks = kit.active_stacks()
        if stacks:
            lines += ["| Pack | Kind | Paths | Bound to |", "|---|---|---|---|"]
            for s in stacks:
                owners = [n for n, b in kit.bindings().items() if s in b.get("stacks", [])]
                lines.append(f"| [`{s}`]({rel(kit.stacks[s].path, self.p)}) | {kit.stacks[s].meta.get('kind', '')} | "
                             f"{table_cell(', '.join(f'`{p}`' for p in kit.stack_paths(s))) or '—'} | {', '.join(owners) or '—'} |")
        else:
            lines.append("No stack packs active. Bind them in `.ai/project/profile.toml`.")
        return "\n".join(lines).rstrip() + "\n"

    def kit_catalogs(self) -> None:
        kit = self.kit
        out = [md_marker(kit, ["core/agents/"]), "", "# Agent Catalog", "",
               "Every core agent card. Projects activate a subset in `.ai/project/profile.toml`.", ""]
        for dept, plane in kit.departments.items():
            names = sorted(n for n, d in kit.agents.items() if d.origin == "core" and d.meta.get("department") == dept)
            if not names:
                continue
            out += [f"## {dept} ({plane})", "", "| Agent | Tier | Access | Use when |", "|---|---|---|---|"]
            out += [f"| [`{n}`]({rel(kit.agents[n].path, CORE / 'agents')}) | {kit.agents[n].meta.get('tier', '')} | "
                    f"{kit.agents[n].meta.get('access', '')} | {table_cell(kit.agents[n].meta.get('description', ''))} |" for n in names]
            out.append("")
        self.add("core/agents/CATALOG.md", "\n".join(out).rstrip() + "\n")
        out = [md_marker(kit, ["core/skills/"]), "", "# Skill Catalog", "", "Every core skill, by category.", ""]
        for cat in kit.categories:
            names = sorted(n for n, d in kit.skills.items() if d.origin == "core" and d.meta.get("category") == cat)
            if not names:
                continue
            out += [f"## {cat}", "", "| Skill | Use when |", "|---|---|"]
            out += [f"| [`{n}`]({rel(kit.skills[n].path, CORE / 'skills')}) | {table_cell(kit.skills[n].meta.get('description', ''))} |" for n in names]
            out.append("")
        self.add("core/skills/CATALOG.md", "\n".join(out).rstrip() + "\n")
        out = [md_marker(kit, ["core/stacks/"]), "", "# Stack Pack Catalog", "", "Every core stack pack, by kind.", ""]
        for kind in kit.kinds:
            ids = sorted(i for i, d in kit.stacks.items() if d.meta.get("kind") == kind)
            if not ids:
                continue
            out += [f"## {kind}", "", "| Pack | Default paths | Related |", "|---|---|---|"]
            for i in ids:
                d = kit.stacks[i]
                out.append(f"| [`{i}`]({rel(d.path, CORE / 'stacks')}) {table_cell(d.meta.get('title', ''))} | "
                           f"{table_cell(', '.join(f'`{p}`' for p in d.meta.get('applies_to') or [])) or '—'} | "
                           f"{', '.join(d.meta.get('related') or []) or '—'} |")
            out.append("")
        self.add("core/stacks/CATALOG.md", "\n".join(out).rstrip() + "\n")

    def index_regions(self) -> None:
        roots = [self.kit.overlay / "wiki"]
        if self.kit.kit_repo:
            roots.insert(0, CORE / "wiki")
        for root in roots:
            for readme in sorted(root.rglob("README.md")):
                if "<!-- agentkit:begin index -->" not in read_text(readme):
                    continue
                rows = []
                for sub in sorted(p for p in readme.parent.iterdir() if p.is_dir()):
                    sub_readme = sub / "README.md"
                    if sub_readme.exists():
                        meta, _ = parse_frontmatter(read_text(sub_readme))
                        rows.append((f"{sub.name}/README.md", f"**{sub.name}/** — {(meta or {}).get('owns', '')}"))
                    else:
                        for page in sorted(sub.glob("*.md")):
                            meta, _ = parse_frontmatter(read_text(page))
                            rows.append((f"{sub.name}/{page.name}", (meta or {}).get("owns", "")))
                for page in sorted(readme.parent.glob("*.md")):
                    if page.name == "README.md":
                        continue
                    meta, _ = parse_frontmatter(read_text(page))
                    rows.append((page.name, (meta or {}).get("owns", "")))
                body = "| Page | Owns |\n|---|---|\n" + "".join(f"| [{p}]({p}) | {table_cell(o)} |\n" for p, o in rows)
                self.regions.setdefault(readme, {})["index"] = body

    def adapter_table(self) -> str:
        lines = ["| Tool | Entry | Agents | Skills | Path-scoped stack rules |", "|---|---|---|---|---|"]
        for tool, spec in ADAPTERS.items():
            cells = [f"`{v}`" if v else "—" for v in (spec["entry"], spec["agents"], spec["skills"], spec["rules"])]
            lines.append(f"| `{tool}` | " + " | ".join(table_cell(c) for c in cells) + " |")
        return "\n".join(lines) + "\n"


def apply_regions(text: str, regions: dict[str, str]) -> str:
    def repl(match: re.Match[str]) -> str:
        name = match.group("name")
        if name not in regions:
            return match.group(0)
        return f"{match.group(1)}{regions[name]}{match.group(4)}"
    return REGION_RE.sub(repl, text)


# --------------------------------------------------------------------------- validation

def validate(kit: Kit, report: Report) -> None:
    names_seen: dict[str, Path] = {}
    for doc in kit.agents.values():
        m = doc.meta
        where = rel(doc.path, kit.project)
        for key in ("name", "description", "department", "tier", "access", "volatility", "reviewed"):
            if not m.get(key) and not (doc.meta.get("extends") and key in ("tier", "access")):
                report.error(f"{where}: missing frontmatter '{key}'")
        if m.get("name") != doc.path.stem:
            report.error(f"{where}: name must equal the file name")
        if m.get("department") and m["department"] not in kit.departments:
            report.error(f"{where}: unknown department '{m['department']}'")
        if doc.origin == "core" and m.get("department") and doc.path.parent.name != m["department"]:
            report.error(f"{where}: card must live in core/agents/{m['department']}/")
        if m.get("tier") and m["tier"] not in kit.tiers:
            report.error(f"{where}: unknown tier '{m['tier']}'")
        if m.get("access") and m["access"] not in kit.access:
            report.error(f"{where}: unknown access '{m['access']}'")
        if len(m.get("description", "")) > 400:
            report.error(f"{where}: description longer than 400 characters")
        if m.get("extends") and (m["extends"] not in kit.agents or kit.agents[m["extends"]].origin != "core"):
            report.error(f"{where}: extends unknown core agent '{m['extends']}'")
        if m.get("extends") and doc.origin == "core":
            report.error(f"{where}: only project cards may use 'extends'")
        for target in section_names(doc.body, "Does Not Own", arrow=True):
            if target not in kit.agents:
                report.error(f"{where}: 'Does Not Own' names unknown agent '{target}'")
        for skill in section_names(doc.body, "Skills"):
            if skill not in kit.skills:
                report.error(f"{where}: 'Skills' names unknown skill '{skill}'")
        _check_name(doc, names_seen, report, kit)
    active_skills = set(kit.active_skills())
    for name in kit.active_agents():
        missing = [s for s in section_names(kit.agents[name].body, "Skills") if s in kit.skills and s not in active_skills]
        if missing:
            report.warn(f"active agent '{name}' lists inactive skill(s) {', '.join(missing)}; enable them in "
                        "[skills] or accept that the role runs without them")
    names_seen = {}
    for doc in kit.skills.values():
        m = doc.meta
        where = rel(doc.path, kit.project)
        for key in ("name", "description", "category", "volatility", "reviewed"):
            if not m.get(key):
                report.error(f"{where}: missing frontmatter '{key}'")
        if m.get("name") != doc.path.parent.name:
            report.error(f"{where}: name must equal the folder name")
        if m.get("category") and m["category"] not in kit.categories:
            report.error(f"{where}: unknown category '{m['category']}'")
        if len(m.get("description", "")) > 600:
            report.error(f"{where}: description longer than 600 characters")
        _check_name(doc, names_seen, report, kit)
    for doc in kit.stacks.values():
        m = doc.meta
        where = rel(doc.path, kit.project)
        for key in ("id", "title", "kind", "volatility", "reviewed", "sources"):
            if not m.get(key):
                report.error(f"{where}: missing frontmatter '{key}'")
        if "applies_to" not in m:
            report.error(f"{where}: missing frontmatter 'applies_to' (use [] when paths cannot identify the stack)")
        if m.get("id") != doc.path.stem:
            report.error(f"{where}: id must equal the file name")
        if m.get("kind") not in kit.kinds:
            report.error(f"{where}: unknown kind '{m.get('kind')}'")
        elif doc.path.parent.name != kit.kinds[m["kind"]]:
            report.error(f"{where}: a '{m['kind']}' pack must live in core/stacks/{kit.kinds[m['kind']]}/")
        for other in m.get("related") or []:
            if other not in kit.stacks:
                report.error(f"{where}: related names unknown stack '{other}'")
    for doc in kit.pages:
        where = rel(doc.path, kit.project)
        if not doc.meta:
            report.error(f"{where}: missing frontmatter")
            continue
        for key in ("owns", "volatility", "reviewed"):
            if not doc.meta.get(key):
                report.error(f"{where}: missing frontmatter '{key}'")
        if doc.meta.get("volatility") == "volatile" and not doc.meta.get("sources"):
            report.error(f"{where}: volatile pages must list 'sources'")
        if doc.meta.get("applies_to") and doc.origin == "core":
            report.error(f"{where}: only project wiki pages take 'applies_to'; core path knowledge belongs in stack packs")
        if doc.meta.get("applies_to") and doc.origin == "project" and doc.path.parent != kit.overlay / "wiki" / "rules":
            report.error(f"{where}: pages with 'applies_to' load automatically and live in .ai/project/wiki/rules/")
    for doc in [*kit.agents.values(), *kit.skills.values(), *kit.stacks.values(), *kit.pages]:
        where = rel(doc.path, kit.project)
        vol, rev = doc.meta.get("volatility"), doc.meta.get("reviewed", "")
        if vol and vol not in kit.cadence:
            report.error(f"{where}: unknown volatility '{vol}'")
        if rev and not DATE_RE.match(str(rev)):
            report.error(f"{where}: reviewed must be YYYY-MM-DD")
    validate_profile(kit, report)


def _check_name(doc: Doc, seen: dict[str, Path], report: Report, kit: Kit) -> None:
    name = doc.meta.get("name", "")
    where = rel(doc.path, kit.project)
    if name and (not NAME_RE.match(name) or len(name) > 64):
        report.error(f"{where}: name must be lowercase kebab-case, at most 64 characters")
    if name in seen:
        report.error(f"{where}: duplicate name '{name}' (also {rel(seen[name], kit.project)})")
    seen[name] = doc.path


def section_names(body: str, heading: str, arrow: bool = False) -> list[str]:
    match = re.search(rf"^## {re.escape(heading)}\n(.*?)(?=^## |\Z)", body, re.S | re.M)
    if not match:
        return []
    names = []
    for line in match.group(1).split("\n"):
        if arrow:
            if "→" not in line:
                continue
            line = line.split("→", 1)[1]
        names += [n for n in re.findall(r"`([^`]+)`", line) if NAME_RE.match(n)]
    return names


LOCALE_PLACEHOLDERS = {"locale", "name", "stem", "ext"}


def validate_profile(kit: Kit, report: Report) -> None:
    prof = kit.profile
    docs = prof.get("docs", {})
    patterns = {"docs.locale_pattern": docs.get("locale_pattern", "")}
    patterns.update({f"docs.locale_paths.{k}": v for k, v in docs.get("locale_paths", {}).items()})
    for key, pattern in patterns.items():
        unknown = set(re.findall(r"\{(\w+)\}", str(pattern))) - LOCALE_PLACEHOLDERS
        if unknown:
            report.error(f"profile {key}: unknown placeholder(s) {', '.join(sorted(unknown))} "
                         f"(supported: {', '.join(sorted(LOCALE_PLACEHOLDERS))})")
        elif pattern and "{locale}" not in str(pattern):
            report.error(f"profile {key}: the pattern must contain {{locale}}")
    release = prof.get("release", {})
    if "{version}" not in str(release.get("tag_pattern", "{version}")):
        report.error("profile release.tag_pattern: must contain {version}")
    names = set()
    for index, package in enumerate(release.get("packages", [])):
        where = f"profile release.packages[{index}]"
        for field in ("name", "version_file", "tag_pattern"):
            if not package.get(field):
                report.error(f"{where}: missing '{field}'")
        if package.get("tag_pattern") and "{version}" not in package["tag_pattern"]:
            report.error(f"{where}: tag_pattern must contain {{version}}")
        if package.get("name") in names:
            report.error(f"{where}: duplicate package name '{package['name']}'")
        names.add(package.get("name"))
    for tool in prof.get("tools", {}).get("targets", []):
        if tool not in ADAPTERS:
            report.error(f"profile [tools].targets: unknown tool '{tool}' (supported: {', '.join(ADAPTERS)})")
    active = kit.active_agents(report)
    kit.active_skills(report)
    for agent, binding in kit.bindings().items():
        if agent not in kit.agents:
            report.error(f"profile [bindings.{agent}]: unknown agent")
        elif agent not in active:
            report.warn(f"profile [bindings.{agent}]: agent is not active")
        for stack in binding.get("stacks", []):
            if stack not in kit.stacks:
                report.error(f"profile [bindings.{agent}].stacks: unknown stack '{stack}'")
        for command in binding.get("commands", []):
            if command not in prof.get("commands", {}):
                report.error(f"profile [bindings.{agent}].commands: unknown command '{command}' (define it under [commands])")
            elif not prof["commands"][command]:
                report.warn(f"profile [bindings.{agent}].commands: '{command}' is empty")
    for stack in [*prof.get("stacks", {}).get("active", []), *prof.get("stack_paths", {})]:
        if stack not in kit.stacks:
            report.error(f"profile: unknown stack '{stack}'")
    for tool, mapping in prof.get("models", {}).items():
        if tool not in ADAPTERS:
            report.error(f"profile [models.{tool}]: unknown tool")
        for tier in mapping:
            if tier not in kit.tiers:
                report.error(f"profile [models.{tool}]: unknown tier '{tier}'")


def authored_markdown(kit: Kit) -> list[Path]:
    files = [p for p in CORE.rglob("*.md") if p.name != "CATALOG.md"]
    if kit.overlay.exists():
        files += list(kit.overlay.rglob("*.md"))
    if kit.kit_repo:
        files += [p for p in (KIT_ROOT / "README.md", KIT_ROOT / "CHANGELOG.md") if p.exists()]
        files += list((KIT_ROOT / "docs").rglob("*.md"))
    return sorted(set(files))


def check_links(kit: Kit, report: Report) -> None:
    for path in authored_markdown(kit):
        fence = False
        for number, line in enumerate(read_text(path).split("\n"), 1):
            if line.lstrip().startswith("```"):
                fence = not fence
            if fence:
                continue
            for match in LINK_RE.finditer(line):
                target = match.group(2)
                if target.startswith(("#", "/", "<")) or re.match(r"^[a-zA-Z][a-zA-Z0-9+.-]*:", target):
                    continue
                if not (path.parent / target.split("#", 1)[0]).exists():
                    report.error(f"{rel(path, kit.project)}:{number}: broken link '{target}'")


def check_budgets(kit: Kit, report: Report, rendered: dict[str, str | bytes]) -> None:
    candidates: list[tuple[str, int]] = []
    if kit.kit_repo:
        candidates += [(rel(p, KIT_ROOT), len(read_text(p).split("\n"))) for p in CORE.rglob("*.md") if p.name != "CATALOG.md"]
    if kit.overlay.exists():
        candidates += [(rel(p, kit.project), len(read_text(p).split("\n"))) for p in kit.overlay.rglob("*.md")]
    agents_md = rendered.get("AGENTS.md")
    if isinstance(agents_md, str):
        candidates.append(("AGENTS.md", len(agents_md.split("\n"))))
    for path, count in candidates:
        for regex, pattern, limit in kit.budgets:
            if regex.match(path):
                if count > limit:
                    report.error(f"{path}: {count} lines exceeds budget {limit} ({pattern})")
                break


def check_duplicates(kit: Kit, report: Report) -> None:
    seen: dict[str, str] = {}
    for path in authored_markdown(kit):
        if _within(path, CORE / "templates"):
            continue
        meta, body = parse_frontmatter(read_text(path))
        text = REGION_RE.sub("", body)
        fence = False
        for line in text.split("\n"):
            if line.lstrip().startswith("```"):
                fence = not fence
            if fence or line.lstrip().startswith(("|", "#", "<!--")):
                continue
            # Routing pointers ("concern → `owner`") and name lists are navigation, not restated rules.
            if "→" in line or not re.sub(r"`[^`]*`|[\s,\-*.]", "", line):
                continue
            if LINK_RE.search(line) and len(re.sub(r"\[[^\]]*\]\([^)]*\)|[^\w]", "", line)) < 40:
                continue
            norm = re.sub(r"^\s*(?:[-*]|\d+\.)\s+", "", line).strip().lower()
            norm = " ".join(norm.split())
            if len(norm) < 60:
                continue
            where = rel(path, kit.project)
            if norm in seen and seen[norm] != where:
                report.warn(f"possible SSOT duplicate in {where} and {seen[norm]}: \"{norm[:70]}…\"")
            seen.setdefault(norm, where)


def project_files(kit: Kit) -> list[str]:
    """Tracked and untracked, not ignored files of the project, as posix paths."""
    listed = subprocess.run(["git", "ls-files", "-co", "--exclude-standard"], cwd=kit.project,
                            capture_output=True, text=True, encoding="utf-8")
    if listed.returncode == 0:
        return [line for line in listed.stdout.splitlines() if line]
    roots = [kit.project / d for d in (".claude", ".codex", ".agents", ".cursor", ".github")]
    files = [rel(f, kit.project) for r in roots if r.exists() for f in r.rglob("*") if f.is_file()]
    return files + [n for n in ("AGENTS.md", "CLAUDE.md", "GEMINI.md", ".cursorrules", ".windsurfrules")
                    if (kit.project / n).exists()]


def unmanaged_instruction_files(kit: Kit, managed: set[str]) -> list[str]:
    patterns = [glob_re(p) for p in INSTRUCTION_SURFACES]
    keep = [glob_re(p) for p in kit.profile.get("tools", {}).get("keep_unmanaged", [])]
    worktrees = str(kit.profile.get("policy", {}).get("worktree_root", "")).strip("/")
    found = []
    for relpath in project_files(kit):
        if relpath.startswith(".ai/") or (worktrees and relpath.startswith(worktrees + "/")):
            continue
        if relpath in managed or not any(p.match(relpath) for p in patterns) or any(k.match(relpath) for k in keep):
            continue
        path = kit.project / relpath
        if not path.is_file():  # tracked but deleted in the working tree
            continue
        try:
            if GEN_MARK in read_text(path):
                continue
        except UnicodeDecodeError:
            pass
        found.append(relpath)
    return sorted(found)


def report_unmanaged(kit: Kit, managed: set[str], report: Report) -> None:
    for relpath in unmanaged_instruction_files(kit, managed):
        report.warn(f"unmanaged instruction file {relpath}: tools load it alongside the kit's files; adopt it per "
                    "«Adopt Existing Instructions» in installation.md, or list it in tools.keep_unmanaged")


def editorconfig_forces_bom(project: Path) -> bool:
    path = project / ".editorconfig"
    if not path.exists():
        return False
    text = read_text(path)
    return bool(re.search(r"^\s*charset\s*=\s*utf-8-bom\s*$", text, re.M | re.I)) and "# agentkit:" not in text


def check_profile_defaults(kit: Kit, report: Report) -> None:
    redundant = []
    for section, values in kit.user_profile.items():
        defaults = kit.default_profile.get(section)
        if section == "project" or not isinstance(values, dict) or not isinstance(defaults, dict):
            continue
        redundant += [f"{section}.{key}" for key, value in values.items() if key in defaults and defaults[key] == value]
    if redundant:
        report.warn(f"profile repeats {len(redundant)} kit default(s): {', '.join(redundant)}; remove them so "
                    "changed defaults in kit updates apply")


PLACEHOLDER_RE = re.compile(r"<[A-Za-z][^<>\n]*>")


def prose_only(text: str) -> str:
    """Text without fenced code blocks and code spans, where placeholders are shown on purpose."""
    out, fence = [], False
    for line in text.split("\n"):
        if line.lstrip().startswith("```"):
            fence = not fence
            continue
        if not fence:
            out.append(re.sub(r"`[^`]*`", "", line))
    return "\n".join(out)


def check_scaffolds(kit: Kit, report: Report) -> None:
    templates = CORE / "templates"
    tokens = set()
    for path in templates.rglob("*.md"):
        tokens |= set(PLACEHOLDER_RE.findall(prose_only(read_text(path))))
    docs = [*kit.agents.values(), *kit.skills.values(), *kit.stacks.values(), *kit.pages]
    for doc in docs:
        if _within(doc.path, templates):
            continue
        prose = prose_only(read_text(doc.path))
        hits = sorted(token for token in tokens if token in prose)
        if hits:
            report.warn(f"{rel(doc.path, kit.project)}: unfilled scaffold placeholder(s) {', '.join(hits[:3])}"
                        + (" …" if len(hits) > 3 else ""))
        if doc in kit.pages and doc.origin == "project" and doc.path.parent == kit.overlay / "wiki" / "rules" \
                and not doc.meta.get("applies_to"):
            report.warn(f"{rel(doc.path, kit.project)}: rules page without 'applies_to' globs never loads; add the "
                        "paths it governs or move it out of wiki/rules/")


def check_integrity(kit: Kit, report: Report) -> None:
    manifest_path = KIT_ROOT / "MANIFEST.json"
    if kit.kit_repo:
        return
    if not manifest_path.exists():
        report.warn("kit has no MANIFEST.json; local edits to the kit cannot be detected")
        return
    manifest = json.loads(read_text(manifest_path))
    for relpath, digest in manifest.get("files", {}).items():
        path = KIT_ROOT / relpath
        if not path.exists():
            report.error(f"kit file missing: {rel(path, kit.project)} (reinstall or update the kit)")
        elif file_hash(path) != digest:
            report.error(f"kit file edited locally: {rel(path, kit.project)} (kit files are read-only; "
                         "propose the change upstream with kit-upstream-propose)")


def check_drift(kit: Kit, renderer: Renderer, report: Report) -> None:
    for relpath, content in renderer.files.items():
        path = kit.project / relpath
        if not path.exists():
            report.error(f"generated file missing: {relpath} (run sync)")
            continue
        current = path.read_bytes() if isinstance(content, bytes) else read_text(path)
        if current != content:
            report.error(f"generated file out of date: {relpath} (run sync)")
    old = load_manifest(kit)
    for relpath in old:
        if relpath not in renderer.files and (kit.project / relpath).exists():
            report.error(f"stale generated file: {relpath} (run sync)")
    for path, regions in renderer.regions.items():
        text = read_text(path)
        if apply_regions(text, regions) != text:
            report.error(f"generated region out of date in {rel(path, kit.project)} (run sync)")


def overdue(kit: Kit, include_core: bool) -> list[tuple[dt.date, str, str]]:
    rows = []
    docs = [*kit.agents.values(), *kit.skills.values(), *kit.stacks.values(), *kit.pages]
    for doc in docs:
        if doc.origin == "core" and not include_core:
            continue
        vol, rev = doc.meta.get("volatility"), doc.meta.get("reviewed")
        if vol not in kit.cadence or not rev or not DATE_RE.match(str(rev)):
            continue
        due = dt.date.fromisoformat(str(rev)) + dt.timedelta(days=kit.cadence[vol])
        rows.append((due, vol, rel(doc.path, kit.project)))
    return sorted(rows)


def load_manifest(kit: Kit) -> list[str]:
    path = kit.generated / "manifest.json"
    if not path.exists():
        return []
    try:
        return json.loads(read_text(path)).get("files", [])
    except json.JSONDecodeError:
        return []


# --------------------------------------------------------------------------- commands

def cmd_sync(kit: Kit) -> int:
    report = Report()
    validate(kit, report)
    if report.errors:
        print("sync aborted: fix these errors first")
        return report.emit()
    renderer = Renderer(kit).render()
    old = set(load_manifest(kit))
    blocked = []
    for relpath in renderer.files:
        path = kit.project / relpath
        if path.exists() and relpath not in old and path.suffix in (".md", ".toml", ".mdc", ".json"):
            if GEN_MARK not in read_text(path):
                blocked.append(relpath)
    if blocked:
        for relpath in blocked:
            print(f"error: refusing to overwrite hand-written file {relpath}; move its unique content into "
                  ".ai/project/ (see the kit-install skill), delete it, and run sync again")
        return 2
    written = 0
    for relpath, content in renderer.files.items():
        path = kit.project / relpath
        if isinstance(content, bytes):
            if not path.exists() or path.read_bytes() != content:
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes(content)
                written += 1
        elif not path.exists() or read_text(path) != content:
            write_text(path, content)
            written += 1
    removed = 0
    for relpath in sorted(old - set(renderer.files)):
        path = kit.project / relpath
        if path.exists():
            path.unlink()
            removed += 1
            parent = path.parent
            while parent != kit.project and parent.exists() and not any(parent.iterdir()):
                parent.rmdir()
                parent = parent.parent
    for path, regions in renderer.regions.items():
        text = read_text(path)
        new = apply_regions(text, regions)
        if new != text:
            write_text(path, new)
            written += 1
    print(f"sync: {len(renderer.files)} generated files, {written} written, {removed} removed")
    report_unmanaged(kit, set(renderer.files), report)
    for msg in report.warnings:
        print(f"warning: {msg}")
    return 0


def cmd_check(kit: Kit) -> int:
    report = Report()
    validate(kit, report)
    check_links(kit, report)
    check_integrity(kit, report)
    check_duplicates(kit, report)
    check_scaffolds(kit, report)
    if not report.errors:
        renderer = Renderer(kit).render()
        check_drift(kit, renderer, report)
        check_budgets(kit, report, renderer.files)
        report_unmanaged(kit, set(renderer.files), report)
    if editorconfig_forces_bom(kit.project):
        report.warn(".editorconfig forces utf-8-bom without the agentkit section; add it per «Editor Settings» "
                    "in installation.md so editors keep agent files BOM-free")
    check_profile_defaults(kit, report)
    late = [r for r in overdue(kit, include_core=kit.kit_repo) if r[0] < today()]
    if late:
        report.warn(f"{len(late)} file(s) overdue for review (run freshness)")
    return report.emit()


def cmd_freshness(kit: Kit, show_all: bool, strict: bool, include_kit: bool) -> int:
    rows = overdue(kit, include_core=kit.kit_repo or include_kit)
    late = [r for r in rows if r[0] < today()]
    shown = rows if show_all else late
    for due, vol, path in shown:
        status = "OVERDUE" if due < today() else "ok"
        print(f"{status:8} due {due.isoformat()}  {vol:9} {path}")
    print(f"{len(late)} overdue of {len(rows)} tracked file(s)")
    return 1 if strict and late else 0


def fill(template: str, values: dict[str, str]) -> str:
    for key, value in values.items():
        template = template.replace("{{" + key + "}}", value)
    return template


def cmd_new(kit: Kit, args: argparse.Namespace) -> int:
    kind, name = args.kind, args.name
    if args.core and not kit.kit_repo:
        print("error: --core is only valid in the kit's own repository; kit files are read-only in projects")
        return 1
    values = {"name": name.split("/")[-1], "date": today().isoformat(), "department": args.department or "",
              "category": args.category or "", "kind": args.stack_kind or "", "extends": args.extends or "",
              "title": name.split("/")[-1].replace("-", " ").title()}
    templates = CORE / "templates"
    if kind == "plan":
        stamp = dt.datetime.now(dt.timezone.utc).strftime("%Y-%m-%dT%H-%M-%SZ")
        dest = kit.project / ".ai" / "tasks" / f"{stamp}-{name}.md"
        text = fill(read_text(templates / "plan.md"), values)
    elif kind == "agent":
        if not args.department and not args.extends:
            print("error: --department is required (or --extends for a project card)")
            return 1
        if args.extends and args.extends in kit.agents:
            values["department"] = values["department"] or kit.agents[args.extends].meta.get("department", "")
        dest = (CORE / "agents" / values["department"] / f"{name}.md") if args.core else (kit.overlay / "agents" / f"{name}.md")
        text = fill(read_text(templates / "agent.md"), values)
        if not args.extends:
            text = text.replace("extends: \n", "")
    elif kind == "skill":
        if not args.category:
            print("error: --category is required")
            return 1
        dest = (CORE / "skills" / name / "SKILL.md") if args.core else (kit.overlay / "skills" / name / "SKILL.md")
        text = fill(read_text(templates / "skill" / "SKILL.md"), values)
    elif kind == "stack":
        if not kit.kit_repo:
            print("error: stack packs are kit assets; add project knowledge to .ai/project/wiki/ or propose a pack upstream")
            return 1
        if args.stack_kind not in kit.kinds:
            print(f"error: --kind must be one of {', '.join(kit.kinds)}")
            return 1
        dest = CORE / "stacks" / kit.kinds[args.stack_kind] / f"{name}.md"
        text = fill(read_text(templates / "stack.md"), values)
    elif kind == "page":
        base = (CORE / "wiki") if args.core else (kit.overlay / "wiki")
        dest = base / f"{name}.md"
        text = fill(read_text(templates / "page.md"), values)
        if not args.core and name.startswith("rules/"):
            text = text.replace("\nreviewed: ", "\napplies_to: []\nreviewed: ", 1)
    else:
        print(f"error: unknown kind '{kind}'")
        return 1
    if dest.exists():
        print(f"error: {rel(dest, kit.project)} already exists")
        return 1
    write_text(dest, text)
    print(f"created {rel(dest, kit.project)}")
    return 0


def copy_payload(src_root: Path, dest: Path) -> dict[str, str]:
    digests: dict[str, str] = {}
    for item in PAYLOAD:
        src = src_root / item
        if not src.exists():
            continue
        files = [src] if src.is_file() else sorted(p for p in src.rglob("*") if p.is_file() and "__pycache__" not in p.parts)
        for path in files:
            relpath = rel(path, src_root)
            target = dest / relpath
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(path, target)
            digests[relpath] = file_hash(target)
    manifest = {"_generated": f"{GEN_MARK}; kit integrity manifest, do not edit",
                "version": read_text(src_root / "VERSION").strip(), "files": dict(sorted(digests.items()))}
    write_text(dest / "MANIFEST.json", json.dumps(manifest, indent=2) + "\n")
    return digests


def cmd_install(target_arg: str, tools: str | None) -> int:
    target = Path(target_arg).resolve()
    if not target.is_dir():
        print(f"error: {target} is not a directory")
        return 1
    if target == KIT_ROOT.resolve():
        print("error: the kit repository cannot install into itself")
        return 1
    kit_dest = target / ".ai" / "kit"
    if kit_dest.exists():
        print(f"error: {kit_dest} exists; use `update` instead")
        return 1
    copy_payload(KIT_ROOT, kit_dest)
    overlay = target / ".ai" / "project"
    if not overlay.exists():
        for path in sorted((CORE / "templates" / "project").rglob("*")):
            if path.is_file() and path.name != "profile.toml":
                dest = overlay / rel(path, CORE / "templates" / "project")
                write_text(dest, fill(read_text(path), {"date": today().isoformat()}))
        # The profile starts minimal: keys that only repeat a kit default would pin today's default forever.
        tools_block = ""
        if tools:
            listed = ", ".join(json.dumps(t.strip()) for t in tools.split(",") if t.strip())
            tools_block = f"\n[tools]\ntargets = [{listed}]\n"
        write_text(overlay / "profile.toml", fill(read_text(CORE / "templates" / "profile.starter.toml"),
                                                  {"name": json.dumps(target.name), "tools": tools_block}))
    profile = Kit(target).profile
    worktree_root = str(profile.get("policy", {}).get("worktree_root", "")).strip("/")
    wanted = [".ai/tasks/"] + ([f"{worktree_root}/"] if worktree_root else [])
    gitignore = target / ".gitignore"
    lines = read_text(gitignore).split("\n") if gitignore.exists() else []
    missing = [entry for entry in wanted if entry not in [line.strip() for line in lines]]
    if missing:
        prefix = "" if not lines or lines[-1] == "" else "\n"
        with open(gitignore, "a", encoding="utf-8", newline="\n") as fh:
            fh.write(f"{prefix}# agentkit working records and per-unit worktrees\n" + "".join(f"{e}\n" for e in missing))
    if editorconfig_forces_bom(target):
        with open(target / ".editorconfig", "a", encoding="utf-8", newline="\n") as fh:
            fh.write(EDITORCONFIG_SECTION)
        print("added an agentkit section to .editorconfig: agent files stay UTF-8 without BOM")
    print(f"installed kit {read_text(KIT_ROOT / 'VERSION').strip()} into {kit_dest}", flush=True)
    result = subprocess.run([sys.executable, str(kit_dest / "tools" / "agentkit.py"), "sync"], cwd=target)
    if result.returncode == 2:
        print("installed, adoption needed: hand-written instruction files block generation. Run the kit-install "
              "skill (or move their project facts into .ai/project/ and delete them), then run sync and check.")
        return 2
    print("next: fill in .ai/project/profile.toml (or run the kit-install skill), then run sync and check")
    return result.returncode


def parse_version(text: str) -> tuple[int, ...]:
    return tuple(int(x) for x in re.findall(r"\d+", text)[:3]) or (0,)


def changelog_between(path: Path, old: str, new: str) -> str:
    if not path.exists():
        return ""
    out, keep = [], False
    for line in read_text(path).split("\n"):
        heading = re.match(r"^## \[?(\d+\.\d+\.\d+)\]?", line)
        if heading:
            version = parse_version(heading.group(1))
            keep = parse_version(old) < version <= parse_version(new)
        elif line.startswith("## "):
            keep = False
        if keep:
            out.append(line)
    return "\n".join(out).strip()


def machine_memory() -> tuple[float | None, float | None]:
    """Total and available physical memory in GB, or (None, None) when it cannot be read."""
    gb = 1024 ** 3
    try:
        if sys.platform == "win32":
            import ctypes

            class MemoryStatus(ctypes.Structure):
                _fields_ = [("dwLength", ctypes.c_ulong), ("dwMemoryLoad", ctypes.c_ulong),
                            ("ullTotalPhys", ctypes.c_ulonglong), ("ullAvailPhys", ctypes.c_ulonglong),
                            ("ullTotalPageFile", ctypes.c_ulonglong), ("ullAvailPageFile", ctypes.c_ulonglong),
                            ("ullTotalVirtual", ctypes.c_ulonglong), ("ullAvailVirtual", ctypes.c_ulonglong),
                            ("ullAvailExtendedVirtual", ctypes.c_ulonglong)]

            status = MemoryStatus()
            status.dwLength = ctypes.sizeof(MemoryStatus)
            if ctypes.windll.kernel32.GlobalMemoryStatusEx(ctypes.byref(status)):
                return status.ullTotalPhys / gb, status.ullAvailPhys / gb
        elif sys.platform.startswith("linux"):
            info = {}
            for line in Path("/proc/meminfo").read_text().splitlines():
                key, _, rest = line.partition(":")
                info[key] = int(rest.split()[0]) * 1024
            return info["MemTotal"] / gb, info.get("MemAvailable", info.get("MemFree", 0)) / gb
        elif sys.platform == "darwin":
            total = int(subprocess.run(["sysctl", "-n", "hw.memsize"], capture_output=True, text=True).stdout.strip())
            vm = subprocess.run(["vm_stat"], capture_output=True, text=True).stdout
            page = int(re.search(r"page size of (\d+)", vm).group(1))
            pages = sum(int(m.group(1)) for m in re.finditer(r"Pages (?:free|inactive|speculative):\s+(\d+)", vm))
            return total / gb, pages * page / gb
    except (OSError, ValueError, KeyError, AttributeError):
        pass
    return None, None


def recommend_concurrency(total_gb: float | None, available_gb: float | None, cores: int | None,
                          classes: list[tuple[str, float, float]], reserve_fraction: float,
                          reserve_min_gb: float, cap: int) -> tuple[float | None, dict[str, tuple[int, str, bool]]]:
    """Units per cost class: (units, limiting factor, below_reserve). Rules: concurrency.md «Capacity»."""
    reserve = max(reserve_min_gb, reserve_fraction * total_gb) if total_gb is not None else None
    result: dict[str, tuple[int, str, bool]] = {}
    for name, memory_gb, per_core in classes:
        limits: dict[str, int] = {"CPU": max(1, int((cores or 1) * per_core))}
        if available_gb is not None and reserve is not None:
            limits["memory"] = int(max(available_gb - reserve, 0) // memory_gb)
        if cap > 0:
            limits["policy.max_parallel_units"] = cap
        factor, units = min(limits.items(), key=lambda item: item[1])
        result[name] = (max(1, units), factor, units == 0)
    return reserve, result


def cmd_capacity(kit: Kit) -> int:
    page = CORE / "wiki" / "operating-model" / "concurrency.md"
    classes = [(r[0], float(r[1]), float(r[2])) for r in parse_table(page, "capacity")]
    settings = {r[0]: float(r[1]) for r in parse_table(page, "capacity-reserve")}
    cap = int(kit.profile.get("policy", {}).get("max_parallel_units", 0) or 0)
    total, available = machine_memory()
    cores = os.cpu_count()
    reserve, result = recommend_concurrency(total, available, cores, classes, settings["reserve_fraction"],
                                            settings["reserve_min_gb"], cap)
    memory = (f"{total:.1f} GB memory, {available:.1f} GB available, reserve {reserve:.1f} GB"
              if total is not None and available is not None else "memory unknown")
    try:
        disk = f"{shutil.disk_usage(kit.project).free / 1024 ** 3:.1f} GB free disk"
    except OSError:
        disk = "free disk unknown"
    print(f"capacity: {cores or '?'} CPU cores · {memory} · {disk} · policy.max_parallel_units: {cap or 'not set'}")
    print(f"recommended concurrent units per cost class ({rel(page, kit.project)} «Capacity»):")
    for name, (units, factor, below) in result.items():
        note = "below the reserve: run one at a time and free memory first" if below else f"limited by {factor}"
        print(f"  {name:9} {units:3}   {note}")
    if total is None:
        print("memory could not be read: follow step 5 of «Capacity» for the fallback limits")
    return 0


RELEASE_TAG_RE = re.compile(r"refs/tags/(v\d+\.\d+\.\d+)$")


def latest_release_tag(ls_remote_output: str) -> str | None:
    """Pick the highest `vX.Y.Z` tag from `git ls-remote --tags --refs` output."""
    tags = [m.group(1) for line in ls_remote_output.splitlines() if (m := RELEASE_TAG_RE.search(line.strip()))]
    return max(tags, key=parse_version) if tags else None


def cmd_update(kit: Kit, source: str | None, ref: str | None) -> int:
    if kit.kit_repo:
        print("error: update runs in an installed project, not in the kit repository")
        return 1
    source = source or kit.profile.get("evolution", {}).get("upstream", "")
    if not source:
        print("error: no source; pass --from <kit-repo-url-or-path> or set evolution.upstream in the profile")
        return 1
    report = Report()
    check_integrity(kit, report)
    if report.errors:
        report.emit()
        print("update aborted: restore or upstream the local kit edits first")
        return 1
    with tempfile.TemporaryDirectory() as tmp:
        src = Path(source)
        if src.is_dir() and ref:
            print("error: --ref applies to a git URL; check out the wanted ref in the local kit checkout instead")
            return 1
        if not src.is_dir():
            if not ref:
                listed = subprocess.run(["git", "ls-remote", "--tags", "--refs", source],
                                        capture_output=True, text=True)
                ref = latest_release_tag(listed.stdout) if listed.returncode == 0 else None
                if not ref:
                    print(f"error: no release tag (vX.Y.Z) found at {source}; pass --ref <tag-or-branch> explicitly")
                    return 1
                print(f"using the latest release: {ref}")
            cmd = ["git", "clone", "--depth", "1", "--branch", ref, source, tmp]
            if subprocess.run(cmd).returncode != 0:
                print("error: git clone failed")
                return 1
            src = Path(tmp)
        if not (src / "core" / "START.md").exists():
            print(f"error: {source} is not an agentkit repository")
            return 1
        old_version = kit.version
        new_version = read_text(src / "VERSION").strip()
        old_defaults = kit.default_profile
        new_defaults = tomllib.loads(read_text(src / "core" / "templates" / "project" / "profile.toml"))
        manifest = json.loads(read_text(KIT_ROOT / "MANIFEST.json")) if (KIT_ROOT / "MANIFEST.json").exists() else {"files": {}}
        for relpath in manifest.get("files", {}):
            path = KIT_ROOT / relpath
            if path.exists():
                path.unlink()
        for path in sorted(KIT_ROOT.rglob("*"), reverse=True):
            if path.is_dir() and not any(path.iterdir()):
                path.rmdir()
        copy_payload(src, KIT_ROOT)
        notes = changelog_between(src / "CHANGELOG.md", old_version, new_version)
    print(f"kit updated {old_version} -> {new_version}")
    for section, values in kit.user_profile.items():
        if not isinstance(values, dict):
            continue
        for key, value in values.items():
            old = old_defaults.get(section, {}).get(key, None) if isinstance(old_defaults.get(section), dict) else None
            new = new_defaults.get(section, {}).get(key, None) if isinstance(new_defaults.get(section), dict) else None
            if old is not None and value == old and new != old:
                print(f"profile: {section}.{key} still holds the old default {old!r}; the new default is {new!r}. "
                      "Remove the key to adopt it, or keep it on purpose.")
    if notes:
        print("\nChangelog since your version (apply every Migration note):\n")
        print(notes)
    result = subprocess.run([sys.executable, str(KIT_ROOT / "tools" / "agentkit.py"), "sync"], cwd=kit.project)
    return result.returncode


# --------------------------------------------------------------------------- main

def default_project() -> Path:
    return KIT_ROOT.parent.parent if KIT_ROOT.parent.name == ".ai" else KIT_ROOT


def main(argv: list[str] | None = None) -> int:
    for stream in (sys.stdout, sys.stderr):
        if hasattr(stream, "reconfigure"):
            stream.reconfigure(encoding="utf-8", errors="replace")
    parser = argparse.ArgumentParser(prog="agentkit", description=__doc__.split("\n")[0])
    parser.add_argument("--project", help="project root (default: inferred from the kit location)")
    sub = parser.add_subparsers(dest="command", required=True)
    sub.add_parser("sync", help="render every generated file from the kit and the project overlay")
    sub.add_parser("check", help="validate sources, links, budgets, drift, and kit integrity")
    sub.add_parser("capacity", help="report CPU and memory and the recommended number of concurrent units")
    fresh = sub.add_parser("freshness", help="list files due or overdue for review")
    fresh.add_argument("--all", action="store_true", help="show every tracked file, not only overdue ones")
    fresh.add_argument("--strict", action="store_true", help="exit 1 when any file is overdue")
    fresh.add_argument("--kit", action="store_true", help="include kit files in an installed project")
    new = sub.add_parser("new", help="scaffold a plan, page, agent, skill, or stack from core/templates")
    new.add_argument("kind", choices=["plan", "page", "agent", "skill", "stack"])
    new.add_argument("name")
    new.add_argument("--core", action="store_true", help="create in core/ (kit repository only)")
    new.add_argument("--department")
    new.add_argument("--category")
    new.add_argument("--kind", dest="stack_kind")
    new.add_argument("--extends")
    inst = sub.add_parser("install", help="install this kit into a project directory")
    inst.add_argument("target")
    inst.add_argument("--tools", help="comma-separated tool targets, e.g. claude,codex,copilot")
    upd = sub.add_parser("update", help="replace the installed kit with a newer version")
    upd.add_argument("--from", dest="source", help="kit repository git URL or local checkout (default: evolution.upstream)")
    upd.add_argument("--ref", help="tag or branch to install from a git URL (default: the latest vX.Y.Z release tag)")
    args = parser.parse_args(argv)

    if args.command == "install":
        return cmd_install(args.target, args.tools)
    project = Path(args.project).resolve() if args.project else default_project()
    kit = Kit(project)
    if args.command == "sync":
        return cmd_sync(kit)
    if args.command == "check":
        return cmd_check(kit)
    if args.command == "capacity":
        return cmd_capacity(kit)
    if args.command == "freshness":
        return cmd_freshness(kit, args.all, args.strict, args.kit)
    if args.command == "new":
        return cmd_new(kit, args)
    if args.command == "update":
        return cmd_update(kit, args.source, args.ref)
    return 2


if __name__ == "__main__":
    sys.exit(main())
