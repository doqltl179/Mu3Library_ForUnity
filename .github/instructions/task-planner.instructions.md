---
description: "Task planning, task index, progress record, and verification-reporting rules for non-trivial work"
---

# Task Planner Rules

Use these rules for non-trivial work that needs 3+ steps, architectural decisions, or explicit verification.

## Operating Rules

- Plan before editing, but keep the plan proportional to the task.
- Maintain `tasks/todo.md` as the persistent local task index.
- Store non-trivial execution plans in `tasks/plans/*.md`.
- Name each new plan file with the UTC creation timestamp required by the [Task Record Policy](../../docs/ai-agents/plans/task-record-policy.md), and keep that timestamp unchanged while the plan is active.
- Use `docs/ai-agents/plans/plan-template.md` for the repository-standard plan shape.
- For a complex request with two or more concurrently ready units, use the graph plan contract in `docs/ai-agents/workflow/graph-engineering.md` instead of forcing the work into a serial step list.
- Treat each `tasks/plans/*.md` file as an active-work artifact, never task history. When a bounded unit finishes, first capture durable outcomes in their owning record, reset `tasks/todo.md` to the next active state, and delete the completed plan in the same closeout. Do not retain or archive completed plan files; see the [Task Record Policy](../../docs/ai-agents/plans/task-record-policy.md).
- Apply the Task Record Policy's approval-free closeout only to the exact completed plan created by the executing agent for the current task; never extend it to other plans or cleanup targets.
- Use `update_plan` or another interactive tracker when available, but do not block if it is unavailable.
- Execute in small, verifiable steps and update status immediately after each step.
- Keep one coordinator-local graph plan for the whole request. Record its revision/absolute path, total/concurrent node and credit budgets, plus each node's admission reason, cost, dependencies, owner, scope, allocation, verification, and status; do not create a nested plan per node.
- In graph work, record only `orchestrator`-approved topology, dependencies, scopes, readiness, allocation, and replanning decisions; `task-planner` does not decide them.
- If the current unit needs a different owner or structural review, route back to `orchestrator` or `role-governor`; do not create a second control plane.
- Capture a lesson in `tasks/lessons.md` only after a correction, durable approach change, or explicit user preference.

## Plan Format

Use the simple format below for serial work. Graph work uses the header, node table, statuses, and lifecycle defined in `docs/ai-agents/workflow/graph-engineering.md`.

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
