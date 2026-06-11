---
applyTo: '**'
---

# Task Planner Rules

Use these rules for non-trivial work that needs 3+ steps, architectural decisions, or explicit verification.

## Operating Rules

- Plan before editing, but keep the plan proportional to the task.
- Maintain `tasks/todo.md` as the persistent local task index.
- Store non-trivial execution plans in `tasks/plans/*.md`.
- Use `docs/ai-agents/plans/plan-template.md` for the repository-standard plan shape.
- When a bounded unit finishes, reset `tasks/todo.md` to the next active state and delete the completed low-value plan by default once its durable outcome is captured elsewhere.
- Use `update_plan` or another interactive tracker when available, but do not block if it is unavailable.
- Execute in small, verifiable steps and update status immediately after each step.
- If the current unit needs a different owner or structural review, route back to `orchestrator` or `role-governor`; do not create a second control plane.
- Capture a lesson in `tasks/lessons.md` only after a correction, durable approach change, or explicit user preference.

## Plan Format

```markdown
# <Task Title>

## Scope

- Goal:
- Out of scope:

## Relevant Files

- Must inspect:
- May inspect:

## Risks

- API / behavior:
- Assets / serialization:
- Verification gap:

## Steps

| # | Task | Status | Details |
|---|---|---|---|
| 1 | Analyze requirements | Not Started | Review the smallest relevant files first |
| 2 | Implement changes | Not Started | Keep edits scoped to the stated goal |
| 3 | Verify changes | Not Started | Run the smallest meaningful checks |
| 4 | Final report | Not Started | Summarize changed files, verification, and remaining risks |
```

Statuses: `Not Started`, `In Progress`, `Completed`, `Blocked`, `Failed`.

## Verification Rules

- Verify impacted areas after changes.
- Never claim a build, test, compile, or check succeeded unless it was actually run.
- If full verification cannot run, report the gap and residual risk.
