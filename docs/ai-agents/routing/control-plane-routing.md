# Control-Plane Routing

## When

- you need to choose between `orchestrator`, `task-planner`, `role-governor`, and `reviewer`,
- a framework or multi-step task needs planning, structural fit review, or quality review and the next control-plane owner is unclear,
- you want the shared control-plane owner matrix without re-reading several agent specs.

## Route Away When

- the question is about stable framework design: use [architecture.md](../architecture.md),
- the question is about the bounded framework-change loop: use [iteration-process.md](../workflow/iteration-process.md),
- the question is about handoff packet format or persistence rules: use [handoff-contract.md](../contracts/handoff-contract.md),
- the question is about role-specific deltas for one control-plane agent: use that individual agent spec.

## Owns

- the shared owner-selection matrix and routing rules for the control-plane agent family.

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
- Use `role-governor` for structural suitability, not feature delivery or quality approval.
- Use `reviewer` for quality and verification, not structural governance.
- If both structural and quality review are needed, `orchestrator` coordinates the gates and records the resulting disposition.

## Shared Non-Overlap Rules

- `orchestrator` does not self-approve structural expansion.
- `task-planner` does not become a second router.
- `role-governor` does not execute delegated domain work.
- `reviewer` does not decide framework ownership.

## Shared Coordination Rules

- Use [handoff-contract.md](../contracts/handoff-contract.md) for packets between control-plane owners and specialists.
- Use [iteration-process.md](../workflow/iteration-process.md) when the current work changes the agent framework itself.
- When a control-plane owner detects a different primary owner, route back to `orchestrator` instead of silently widening scope.

## Duplication Rule

- Do not restate this owner-selection matrix in every control-plane agent spec.
- Individual control-plane agent docs should link here and document only their role-specific deltas.