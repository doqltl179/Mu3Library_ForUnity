---
owns: "When work needs a written plan, where plans live, plan statuses, and how plans are closed"
volatility: evolving
reviewed: 2026-10-09
---

# Planning

## When

- work has three or more steps, an architectural choice, several owners, or explicit verification,
- a session may be interrupted or compacted before the work ends.

## Route Away When

- splitting a request into owned units: the `work-decompose` skill,
- writing the plan file: the `task-plan-write` skill.

## When To Plan

| Work | Plan |
|---|---|
| Single small change with an obvious owner | None; state the intent in one line |
| Three or more steps, or an architectural choice | A plan file |
| Several owners or parallel units | One plan for the whole request, with the unit table from `work-decompose`; never one plan per unit |

Keep the plan proportional: a plan that costs more to maintain than the work it tracks is waste.

## Plan Storage

- Plans live in `.ai/tasks/`, which is not committed. Create one with `agentkit.py new plan <slug>` from the [plan template](../../templates/plan.md).
- File names start with the UTC creation timestamp, so listing the folder shows work in the order it began. Never rename an active plan.
- A unit's plan lives in its worktree's `.ai/tasks/`; a plan coordinating several units lives in the main checkout's `.ai/tasks/`. Removing a worktree deletes its plan, so close the plan first.
- The plan is a working record, not a source of truth: the current request outranks it, and durable outcomes belong in their owning pages.

## Statuses

`not-started`, `in-progress`, `blocked`, `done`, `dropped`. Update a step's status as soon as it changes; a `blocked` step names what unblocks it.

## Closeout

1. Move every durable outcome to its owner (code, wiki page, profile, lessons) per [memory-policy.md](../operating-model/memory-policy.md).
2. Register leftover work per «Leftovers Become Issues» in [issues-and-prs.md](issues-and-prs.md).
3. Delete the plan file. A completed plan left behind is later mistaken for active work. Deleting a plan you created for the finished task needs no further confirmation; any other plan does.
