---
owns: "Agent card schema, the granularity model (role card × stack pack × project binding), departments, tiers, access levels, and when to add a new agent"
volatility: evolving
reviewed: 2026-10-09
---

# Agent Card Spec

An agent card defines one owner: the surface it changes, the decisions it owns, and its boundaries. Cards are the source for every tool-specific agent file that `agentkit.py sync` renders.

## When

- adding, splitting, narrowing, or editing an agent card (core or project-local),
- deciding whether a need calls for a new agent, a stack pack, a local specialization, or a skill.

## Route Away When

- choosing an existing owner for a task: [routing.md](../operating-model/routing.md),
- how a selected owner runs and hands off: [delegation.md](../operating-model/delegation.md),
- the step-by-step procedure for adding one: the `kit-agent-add` skill; for auditing overlap: the `kit-role-audit` skill.

## Granularity Model

An owner is defined by the surface it changes and the decisions it owns, never by programming language alone. Language and framework knowledge is composed in at render time:

`role card` (surface and boundaries) × `stack packs` (language and framework knowledge) × `project binding` (paths, stacks, notes from the profile) = the concrete specialist for one project.

Example: `web-frontend-engineer` bound to `apps/web/**` with `typescript`, `react`, `nextjs` renders as a Next.js frontend specialist for that project; the same card bound with `vue` in another project renders as a Vue specialist. One card, no duplicated role text.

## What To Add

| Situation | Add |
|---|---|
| An existing surface uses a language or framework the kit lacks | A stack pack ([stack-spec.md](stack-spec.md)) |
| One project needs a narrower owner inside an existing surface (for example: engine editor tooling for a specific codebase) | A project-local card with `extends: <core-agent>` |
| A surface no card owns recurs across projects, with its own primary artifacts, decisions, and non-goals | A new core card, after `role-governor` review |
| A repeatable procedure several owners run | A skill ([skill-spec.md](skill-spec.md)) |
| A rule | The owning wiki page ([page-spec.md](page-spec.md)) |

A new core agent must: own a primary artifact or decision no card owns; name its neighbors in `Does Not Own`; never be the default owner for broad work; not be defined by a language; and pass the `kit-role-audit` skill.

## Departments

<!-- agentkit:table departments -->
| Department | Plane | Covers |
|---|---|---|
| `governance` | governance | Decomposition, owner selection, plan records, and structural fitness of the catalog |
| `product` | execution | Requirements, UX specifications, and architecture decisions before implementation |
| `client` | execution | Code that runs on the user's device: web UI, mobile, desktop, game client, graphics, firmware |
| `server` | execution | Request/response services, realtime systems, and databases |
| `data-ai` | execution | Data pipelines, analytics, machine learning, and LLM-powered product features |
| `platform` | execution | CI/CD, cloud infrastructure, observability, and developer tooling |
| `specialty` | execution | Cross-surface engineering work that is itself the deliverable: test automation, performance |
| `documentation` | execution | Human-facing project docs and agent-facing kit docs |
| `quality` | quality | Review lenses that approve or reject evidence without implementing fixes |
| `release` | execution | Versioning, release publication, and integration of multi-owner work |
| `evolution` | evolution | Detecting external change so the kit stays current |

Planes are explained in [architecture.md](../operating-model/architecture.md).

## Tiers

<!-- agentkit:table tiers -->
| Tier | Use for |
|---|---|
| `deep` | Judgment-heavy roles: decomposition, architecture, structural and quality review |
| `standard` | Implementation roles |
| `light` | Mechanical, well-specified roles |

The tier never names a model. The profile maps tiers to concrete models per tool (`[models.<tool>]`), so a model release changes one profile line, not every card.

## Access

<!-- agentkit:table access -->
| Access | Meaning |
|---|---|
| `read-only` | Reads, searches, and runs read-only commands; never edits project files. Rendered with edit tools removed where the tool supports it |
| `read-write` | May edit files inside its owned scope |

## Frontmatter

| Field | Required | Rule |
|---|---|---|
| `name` | yes | Lowercase kebab-case, ≤ 64 chars, equal to the file name without `.md`, unique across core and project |
| `description` | yes | ≤ 400 chars. First sentence: what it does. Then: "Use when …; not for …". Tools use this text to decide delegation |
| `department` | yes | A value from «Departments» |
| `tier` | yes | A value from «Tiers» |
| `access` | yes | A value from «Access» |
| `volatility` | yes | See [freshness-policy.md](../evolution/freshness-policy.md) |
| `reviewed` | yes | `YYYY-MM-DD` |
| `extends` | project cards only | The core card this card narrows |

## Body Shape

```markdown
# <Title>

Mission: <one sentence>.

## Owns
- <3–7 bullets: artifacts and decisions this role owns>

## Does Not Own
- <concern> → `<owner>`

## Domain Checks
- <3–6 role-specific checks performed before handing off>

## Skills
- `<skill>`, `<skill>`

## Output
- <1–3 role-specific deliverables; the generic report shape is in the handoff contract>
```

- Keep cards within the budget in [context-budget.md](../operating-model/context-budget.md).
- Do not repeat the generic role protocol, verification rules, or report format; [delegation.md](../operating-model/delegation.md) and [handoff-contract.md](../operating-model/handoff-contract.md) own them and every rendered agent links to them.
- Every `Does Not Own` target must be an existing agent name.
- A project card with `extends` states only how it narrows the base; the rendered file includes the base card first.

## Placement

- Core: `core/agents/<department>/<name>.md`.
- Project: `.ai/project/agents/<name>.md`.
