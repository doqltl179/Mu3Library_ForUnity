# Control-Plane Routing

## When

- you need to choose between `orchestrator`, `task-planner`, `role-governor`, and `reviewer`,
- a framework or multi-step task needs planning, structural fit review, or quality review and the next control-plane owner is unclear,
- you want the shared control-plane owner matrix without re-reading several agent specs.

## Route Away When

- control-plane owner selection is no longer the question: use [../README.md](../README.md) for the top-level router.
- the need is specifically framework rationale, the bounded change loop, handoff format, or one agent's deltas: use [architecture.md](../architecture.md), [iteration-process.md](../workflow/iteration-process.md), [handoff-contract.md](../contracts/handoff-contract.md), or that individual agent spec.

## Owner Selection Matrix

| Task Shape | Primary Owner | Route Away When |
|---|---|---|
| A broad request must be decomposed into bounded units or the next owner is unclear | `orchestrator` | the current unit already has a clear owner and only needs planning, structural review, or quality review |
| The current assigned unit needs a step plan, progress tracking, or bounded replanning | `task-planner` | the main problem is cross-agent routing, structural fit, or quality approval |
| A non-trivial framework change needs continue-or-rework disposition for overlap, missing ownership, or routing ambiguity | `role-governor` | the issue is domain implementation or quality approval rather than structural governance |
| A completed or review-ready change needs regression, compatibility, verification, docs, or release-quality review | `reviewer` | the issue is structural fit or cross-agent routing rather than quality approval |

## Shared Routing Rules

- Start with `orchestrator` when the next owner is not yet clear.
- Use `task-planner` only after the current unit and current owner are clear.
- Use `role-governor` only for structural suitability.
- Use `reviewer` only for quality and verification.
- If both structural and quality review are needed, `orchestrator` coordinates the gates and records the resulting disposition.

## Boundary Rules

- `orchestrator` does not self-approve structural expansion.
- `task-planner` does not become a second router.
- `role-governor` does not execute delegated domain work.
- `reviewer` does not decide framework ownership.

## Coordination Links

- Use [handoff-contract.md](../contracts/handoff-contract.md) for packets between control-plane owners and specialists.
- Use [iteration-process.md](../workflow/iteration-process.md) when the current work changes the agent framework itself.
- When a control-plane owner detects a different primary owner, route back to `orchestrator` instead of silently widening scope.
