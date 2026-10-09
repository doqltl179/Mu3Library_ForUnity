---
owns: "Why the kit is structured as it is: layers, planes, departments, the composition model, and how generation enforces single ownership"
volatility: stable
reviewed: 2026-10-09
---

# Architecture

Stable design rationale. Open it to understand or change the structure; day-to-day work never needs it.

## When

- proposing a structural change (new plane, department, layer, or asset type),
- explaining why a rule or file lives where it does.

## Route Away When

- choosing an owner now: [routing.md](routing.md),
- the schema of one asset type: [authoring/README.md](../authoring/README.md).

## Layers

| Layer | Location | Owned by | Role |
|---|---|---|---|
| Core kit | `core/`, `tools/agentkit.py` (installed at `.ai/kit/`) | Kit maintainers, changed upstream | Invariants, defaults, roles, procedures, stack knowledge |
| Project overlay | `.ai/project/` | The project | Facts, parameters, active roles, bindings, local roles and procedures |
| Generated | Entry files, tool folders, `.ai/generated/` | `agentkit.py sync` | Tool-specific views of the two layers above |

The core is portable because everything project-specific is a parameter in the overlay. The generated layer exists because each AI tool reads a different file layout; rendering those layouts from one source keeps a single owner per fact while every tool gets native files.

## Planes

| Plane | Departments | Decides | Never |
|---|---|---|---|
| Governance | `governance` | What units exist, who owns them, in what order; whether the catalog's structure is sound | Implements domain work |
| Execution | `product`, `client`, `server`, `data-ai`, `platform`, `specialty`, `documentation`, `release` | How a unit's artifact is built | Redefines ownership or approves its own quality |
| Quality | `quality` | Whether evidence is sufficient to integrate | Fixes what it reviews |
| Evolution | `evolution` | What changed outside that the kit must absorb | Edits content beyond its register |

Separating planes prevents the two most common multi-agent failures: an implementer approving its own work, and a coordinator quietly doing implementation that no one reviews. Departments are listed in [agent-spec.md](../authoring/agent-spec.md).

## Composition Model

Roles are defined by surface, not language. A concrete specialist is composed at render time:

`role card` × `stack packs` × `project binding` → one rendered agent file per tool.

This gives fine-grained specialists (for example a Next.js frontend engineer, a Unity editor-tooling engineer, a FastAPI backend engineer) without one card per language-framework combination, which would duplicate role text across dozens of files and drift. Narrower project roles are local cards that `extends` a core card. See «Granularity Model» in [agent-spec.md](../authoring/agent-spec.md).

## Asset Types

| Asset | Answers | Spec |
|---|---|---|
| Wiki page | What must be true (rules, policies) | [page-spec.md](../authoring/page-spec.md) |
| Agent card | Who owns an artifact or decision | [agent-spec.md](../authoring/agent-spec.md) |
| Skill | How to do a task, step by step | [skill-spec.md](../authoring/skill-spec.md) |
| Stack pack | How a language or framework is used correctly | [stack-spec.md](../authoring/stack-spec.md) |

## Enforcement

- Inventories (catalogs, section indexes, the adapter table) are generated from source frontmatter, so they cannot drift from what they list.
- `agentkit.py check` enforces schemas, links, line budgets, generated drift, and kit integrity, and warns on duplicated sentences and overdue reviews.
- Freshness metadata on every source file drives scheduled review; see [evolution/README.md](../evolution/README.md).

## Change This Page When

- a layer, plane, department, or asset type is added, removed, or redefined (after `role-governor` review).
