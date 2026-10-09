---
owns: "Single-source-of-truth rules: what an owner is, how to reference instead of copy, generated vs authored files, layer precedence, and rule types"
volatility: stable
reviewed: 2026-10-09
---

# Single Source of Truth

Every fact has exactly one owning file. Everything else links to it.

Why: a duplicated fact drifts, because only one copy gets fixed, and an agent then follows whichever copy it read first. One owner per fact keeps the kit free of contradictions and lets an agent stop reading after one page.

## When

- you are about to write a rule, value, list, procedure, or template anywhere,
- two files appear to say different things,
- you need to know whether a file may be edited by hand.

## Route Away When

- how to phrase and shape a page: [agent-first-writing.md](agent-first-writing.md), [page-spec.md](../authoring/page-spec.md),
- where session or project knowledge should be stored: [memory-policy.md](../operating-model/memory-policy.md).

## Rules

1. One fact, rule, value, list, procedure, or template has exactly one owning file.
2. Other files link to the owner. A link may carry one navigation phrase naming what the owner answers; it never restates the owner's conditions, values, or steps.
3. Before adding content, search for an existing owner: `rg` on the key terms, then the section index. Extend the owner instead of adding a neighbor.
4. When two files disagree, the owner wins. Fix or delete the other copy in the same change.
5. A change to behavior updates its owning docs in the same unit of work.
6. Inventories are generated from the files they list. Never hand-edit a generated file or a generated region.

## Ownership Map

| Content | Owner |
|---|---|
| A rule or policy | The wiki page whose `owns` field claims the question |
| A role's scope and boundaries | Its agent card under `core/agents/` or `.ai/project/agents/` |
| How to pick an owner | [routing.md](../operating-model/routing.md); the owner inventory is generated |
| An executable procedure | Its skill, which links to the policy pages it applies |
| Language or framework knowledge | Its stack pack under `core/stacks/` |
| Project facts and parameters | `.ai/project/profile.toml` and `.ai/project/wiki/` |
| Profile keys and their defaults | [profile.toml template](../../templates/project/profile.toml) |
| Tool file formats (which files each AI tool reads) | The adapter table in `tools/agentkit.py`, rendered into [tool-adapters.md](../integration/tool-adapters.md) |
| Departments, skill categories, stack kinds, review cadence, line budgets | The marked table on their owning page, parsed by `agentkit.py` |

## Generated vs Authored

- Authored (edit by hand): `core/**` and `.ai/project/**`, except generated regions.
- Generated (never edit): `AGENTS.md`, `CLAUDE.md`, `GEMINI.md`, the tool folders listed in [tool-adapters.md](../integration/tool-adapters.md), `.ai/generated/**`, every `CATALOG.md`, and every region between `<!-- agentkit:begin NAME -->` and `<!-- agentkit:end NAME -->`.
- Every generated file carries an `agentkit:generated` marker. `agentkit.py check` fails when a generated file drifts from its sources; `agentkit.py sync` regenerates them.

## Layers and Precedence

| Rank | Layer | May |
|---|---|---|
| 1 | The current user request | Override anything for that request only |
| 2 | Project overlay `.ai/project/` | Set parameters the core exposes, add project facts, narrow roles with local agents |
| 3 | Core kit | Define invariants and defaults |

- The overlay never contradicts a core invariant. When a project needs a core rule changed, propose it upstream with the `kit-upstream-propose` skill instead of forking the rule locally.
- Kit files inside an installed project (`.ai/kit/`) are read-only; `agentkit.py check` reports local edits.

## Rule Types

| Type | Meaning | Changed by |
|---|---|---|
| invariant | Holds regardless of project, tool, or model (for example: never fake success) | Structural review by `role-governor` only |
| policy | A chosen default, often parameterized by the profile | Team decision, recorded on its owning page |
| fact | External and time-sensitive: versions, tool formats, vendor behavior | Freshness review; the page is `volatility: volatile` and lists `sources` |
| scaffold | Compensates for a current model limitation | Model upgrades; see [model-calibration.md](../evolution/model-calibration.md) |

Tag a scaffold rule inline with `[scaffold]` so it can be found and retested. Other types are not tagged.

## Duplication Smells

- The same sentence in two source files (`agentkit.py check` warns).
- A router or index row that contains a condition or a value.
- A skill step that restates a policy instead of linking to it.
- An agent card that repeats the generic role protocol from [delegation.md](../operating-model/delegation.md).
- A project wiki page that restates a core rule.
