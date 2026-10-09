---
owns: "The project overlay: its layout, how the profile is used, project-local agents and skills, the project wiki, and lessons"
volatility: evolving
reviewed: 2026-10-09
---

# Project Overlay

`.ai/project/` is where a project makes the kit its own without editing the kit. Precedence between layers is owned by «Layers and Precedence» in [ssot.md](../principles/ssot.md).

## When

- configuring the kit for a project,
- adding a project-specific role, procedure, or knowledge page.

## Route Away When

- each profile key's meaning and default: the [profile template](../../templates/project/profile.toml), which is the schema,
- writing a card, skill, or page: [authoring/README.md](../authoring/README.md).

## Layout

| Path | Holds |
|---|---|
| `profile.toml` | Project identity, tool targets, policy parameters, commands, doc paths, active agents and skills, bindings, stack paths, model mapping, upstream |
| `agents/<name>.md` | Project-local agent cards, usually narrowing a core card with `extends` |
| `skills/<name>/SKILL.md` | Project-local procedures |
| `wiki/` | Project knowledge pages; `wiki/README.md` is the project wiki index |
| `wiki/rules/` | Path-scoped project rules (`applies_to`), loaded automatically when matching files are touched |
| `lessons.md` | Project-specific lessons from corrections |
| `intake-pending.md` | Kit change proposals waiting to be filed upstream; exists only while the platform is unreachable ([change-intake.md](../evolution/change-intake.md)) |

## Profile

- Only keys present in the template exist; a key missing from the project profile takes the template default.
- Keep only the keys the project sets differently. A key that repeats a default pins it: when a kit update changes that default, the project keeps the old value without noticing. `check` lists such keys, and `update` reports keys that still hold an old default the update changed.
- Activate only the agents and skills the project needs: every active agent and skill costs context in tools that list them.
- Bind each active execution agent to the paths it owns, the stack packs it applies, and the named commands that verify its paths with `[bindings.<agent>]`; the binding is rendered into that agent's files.
- Put the few rules that hold for every task in `project.guardrails`, one line each; they appear in `AGENTS.md` and count against its line budget.
- Leave a command empty when it does not exist; agents then report the gap instead of guessing.
- After editing, run `agentkit.py sync` and `agentkit.py check`.

## Local Agents

- Use `extends: <core-agent>` to narrow a core role to part of this project (for example one engine's editor tooling, or one service). The local card states only the narrowing; the base card is rendered first.
- A local card without `extends` is for a surface no core card covers; also propose it upstream if other projects would need it.
- Local agents are always active and must have names distinct from core agents.

## Local Skills

- For procedures specific to this project (for example a release checklist for one store). Names must not reuse core skill names; disable a core skill in the profile to replace it.

## Project Wiki

- Record project facts the kit leaves open: architecture overview, domain glossary, module map, local conventions, environments.
- Same page shape as core pages ([page-spec.md](../authoring/page-spec.md)); the index region in `wiki/README.md` is generated.
- Never restate a core rule here; link to it.

## Lessons

- One line per lesson: the rule learned and why, newest last.
- Promote a lesson to a wiki page when it becomes a standing rule, or upstream when it is not project-specific; then delete it here. Owned by [memory-policy.md](../operating-model/memory-policy.md).
