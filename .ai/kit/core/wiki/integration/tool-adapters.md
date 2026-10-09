---
owns: "Which files agentkit generates for each AI tool, how roles and tiers map onto tool features, and known tool behaviors that shape the adapters"
volatility: volatile
reviewed: 2026-10-09
sources: ["https://code.claude.com/docs/en/subagents", "https://code.claude.com/docs/en/skills", "https://code.claude.com/docs/en/memory", "https://docs.github.com/en/copilot/reference/custom-agents-configuration", "https://developers.openai.com/codex/skills", "https://developers.openai.com/codex/subagents", "https://agents.md"]
---

# Tool Adapters

Every AI tool reads a different file layout. `agentkit.py sync` renders the same sources into each layout selected by `tools.targets`. The adapter table in `tools/agentkit.py` is the single owner of these mappings; the table below is generated from it.

## When

- choosing `tools.targets` for a project,
- a tool changed where or how it reads instructions, agents, skills, or rules.

## Route Away When

- tracking tool releases: [tech-radar.md](../evolution/tech-radar.md),
- changing an adapter: file an intake record per [change-intake.md](../evolution/change-intake.md), then edit `ADAPTERS` and the renderer in `tools/agentkit.py`.

## Generated Files

`AGENTS.md` (the startup page plus «This Project») and `.ai/generated/catalog.md` are generated for every project. Per tool:

<!-- agentkit:begin adapters -->
| Tool | Entry | Agents | Skills | Path-scoped stack rules |
|---|---|---|---|---|
| `claude` | `CLAUDE.md (imports AGENTS.md)` | `.claude/agents/{name}.md` | `.claude/skills/{name}/` | `.claude/rules/stack-{id}.md (paths frontmatter)` |
| `codex` | `AGENTS.md (read natively)` | `.codex/agents/{name}.toml` | `.agents/skills/{name}/` | — |
| `copilot` | `.github/copilot-instructions.md (points to AGENTS.md)` | `.github/agents/{name}.agent.md` | `.github/skills/{name}/ (only when neither claude nor codex is a target; Copilot also reads their skill folders)` | `.github/instructions/stack-{id}.instructions.md (applyTo)` |
| `cursor` | `AGENTS.md (read natively)` | — | — | `.cursor/rules/stack-{id}.mdc (globs)` |
| `gemini` | `GEMINI.md (imports AGENTS.md)` | — | — | — |
<!-- agentkit:end adapters -->

## Unmanaged Instruction Files

Tools also load instruction files that agentkit did not generate. `sync` and `check` report every file in these locations that is neither generated nor listed in `tools.keep_unmanaged`, because it is loaded alongside the kit's files and can contradict them:

<!-- agentkit:begin surfaces -->
- `**/AGENTS.md`
- `**/CLAUDE.md`
- `**/GEMINI.md`
- `.cursorrules`
- `.windsurfrules`
- `.claude/agents/**`
- `.claude/commands/**`
- `.claude/rules/**`
- `.claude/skills/**`
- `.codex/agents/**`
- `.agents/skills/**`
- `.cursor/rules/**`
- `.github/copilot-instructions.md`
- `.github/agents/**`
- `.github/instructions/**`
- `.github/prompts/**`
- `.github/skills/**`
<!-- agentkit:end surfaces -->

## Mappings

| Kit concept | Rendering |
|---|---|
| Agent card + project binding | One agent file per tool, body = card (base card first for `extends`) + protocol links + binding |
| `access: read-only` | Claude: `disallowedTools` removes edit tools · Copilot: `tools` limited to read, search, execute, web · Codex: `sandbox_mode = "read-only"` |
| `tier` | `model` set only when the profile maps the tier in `[models.<tool>]` |
| Skill | Folder copied with frontmatter reduced to `name` and `description` |
| Stack pack + paths | Path-scoped rule file (`stack-<id>`) where the tool supports one; otherwise reachable through bindings and the catalog |
| Project rule page with `applies_to` | Path-scoped rule file (`project-<page>`), rendered the same way |
| `project.guardrails` | A «Project Rules» list in `AGENTS.md` «This Project» |
| Links | Rewritten to project-root-relative paths; links inside a skill folder stay relative |

## Tool Notes

Facts as of 2026-10-09, per the sources above:

- Claude Code reads `CLAUDE.md`, and reads `AGENTS.md` only when no `CLAUDE.md` exists, so the generated `CLAUDE.md` imports `AGENTS.md`. It loads `CLAUDE.md` and `AGENTS.md` files from subdirectories when it reads files there, which is why the kit is installed as a copy without its own entry files.
- Claude Code reads project skills from `.claude/skills/` only, and path-scoped rules from `.claude/rules/` with a `paths` list.
- Codex reads `AGENTS.md`, skills from `.agents/skills/` between the working directory and the repository root, and custom agents from `.codex/agents/*.toml` (`name`, `description`, `developer_instructions`).
- Copilot reads `.github/copilot-instructions.md`, `AGENTS.md`, custom agents from `.github/agents/*.agent.md`, path-scoped instructions with `applyTo`, and skills from `.github/skills/`, `.claude/skills/`, and `.agents/skills/`; agentkit renders `.github/skills/` only when no other skill folder is rendered, to avoid duplicates.
- Cursor reads `AGENTS.md` and `.cursor/rules/*.mdc`; Gemini CLI reads `GEMINI.md`, which imports `AGENTS.md`.
- Skill and agent descriptions are what tools match requests against: keep the trigger first.
