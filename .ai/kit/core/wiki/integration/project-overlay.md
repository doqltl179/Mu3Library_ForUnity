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
| `intake-pending.md` | Kit change proposals, made as «Promotion» in [memory-policy.md](../operating-model/memory-policy.md) allows, waiting to be filed upstream; exists only while the platform is unreachable ([change-intake.md](../evolution/change-intake.md)) |

## Profile

- Only keys present in the template exist; a key missing from the project profile takes the template default.
- Write every `[policy]`, `[hosting]`, and `[docs]` value the project confirmed, even one that equals the kit default. These values are the project's own branch, hosting, and document policy; an explicit value keeps it when a kit update changes a default.
- In the other sections keep only the keys the project sets differently. A key there that repeats a default pins it: when a kit update changes that default, the project keeps the old value without noticing. `check` lists such keys.
- `update` reports each key the profile leaves unset whose default changes, with the line that keeps the previous value; each key the profile sets that the kit no longer reads; and each key that still holds an old default the update changed.
- Activate only the agents and skills the project needs: every active agent and skill costs context in tools that list them.
- Bind each active execution agent to the paths it owns, the stack packs it applies, and the named commands that verify its paths with `[bindings.<agent>]`; the binding is rendered into that agent's files.
- Put the few rules that hold for every task in `project.guardrails`, one line each; they appear in `AGENTS.md` and count against its line budget. A replacement of a core policy section is such an entry, per «Layers and Precedence» in [ssot.md](../principles/ssot.md).
- Leave a command empty when it does not exist; agents then report the gap instead of guessing.
- After editing, run `agentkit.py sync` and `agentkit.py check`.

## Local Agents

- Use `extends: <core-agent>` to narrow a core role to part of this project (for example one engine's editor tooling, or one service). The local card states only the narrowing; the base card is rendered first.
- A local card without `extends` is for a surface no core card covers. When other projects would need it too, it is an upstream candidate per «Promotion» in [memory-policy.md](../operating-model/memory-policy.md).
- Local agents are always active and must have names distinct from core agents.

## Local Skills

- For procedures specific to this project (for example a release checklist for one store). Names must not reuse core skill names; disable a core skill in the profile to replace it.

## Project Wiki

- Record project facts the kit leaves open: architecture overview, domain glossary, module map, local conventions, environments, and the product planning values of «Project Values» in [idea-evaluation.md](../workflows/idea-evaluation.md).
- For a project whose user interface has navigable screens, windows, or scenes, keep a feature map in `wiki/features.md`, so an agent reaches a state without searching for it again; a command-line tool keeps none, since its help owns its commands. Give one entry per screen, window, or scene, and one per feature reached inside it, each with its canonical term per [domain-terms.md](../workflows/domain-terms.md), the file that defines it, and the steps or command (a `commands.*` key or a project script) that reach it in the running app, or a link to the spec whose user flow gives them. Mark steps nobody has run as unverified, and remove the mark when a run follows them. For a screen behind sign-in, name where a test account comes from (a seed file or an environment variable), never its values, per «Do Not Persist» in [memory-policy.md](../operating-model/memory-policy.md). A repository with several apps keeps one page per app, linked from `features.md`. `codebase-onboard` drafts the map; once it exists, a change that adds, renames, or removes a screen or feature, or changes how one is reached, updates it in the same unit.
- Same page shape as core pages ([page-spec.md](../authoring/page-spec.md)); the index region in `wiki/README.md` is generated.
- Never restate a core rule here; link to it. A page holds the detail of a replacement only when the replacement's `project.guardrails` entry links it; the page states only the project's rule and replaces nothing by itself.

## Lessons

- One line per lesson: the rule learned and why, newest last.
- Promote a lesson, and delete it from `lessons.md`, when «Promotion» in [memory-policy.md](../operating-model/memory-policy.md) says.
