---
name: task-plan-write
description: "Create or update the plan file in .ai/tasks/ with `agentkit.py new plan` from the kit plan template: scope with done-when and out-of-scope, the unit table with owners, verification, gates, and statuses, budget, risks, and a running summary kept current after every unit, then close it out when the work ends. Use when work needs a plan under the planning policy, when a planned unit changes state, or when planned work finishes."
category: planning
volatility: evolving
reviewed: 2026-10-09
---

# Write Task Plan

## Use When
- Work meets «When To Plan» in [planning.md](../../wiki/workflows/planning.md).
- A unit of an existing plan starts, finishes, gets blocked, or changes.
- Planned work ends and needs closeout.

## Do Not Use When
- Units and owners are not decided yet → `work-decompose` first.
- Specifying requirements → `requirements-spec-write`.
- The work does not meet «When To Plan» → proceed without a plan.

## Inputs
- The goal, done-when criteria, and unit table, from `work-decompose` or the request.
- A short kebab-case slug for the task, or the path of the existing plan.

## Steps
1. Look for an existing plan for this request: `rg -l "<slug|issue number|goal terms>" .ai/tasks/`. Update it instead of creating a second one, per «When To Plan».
2. Create a new plan with `agentkit.py new plan <slug>`; its location and name follow «Plan Storage» in planning.md.
3. Fill the request line and «Scope» in the [plan template](../../templates/plan.md): Goal, Done when as testable criteria, and Out of scope with a one-line reason per item.
4. Fill «Units» from the `work-decompose` table, or with one row per ordered action for single-owner work: owner, write scope, verification, gate, and a status per «Statuses» in planning.md. Fill «Budget» for parallel work, and list «Risks And Open Questions».
5. After every unit, update the plan before starting the next: set its status, and record in «Running Summary» the verification command and result per «Evidence Format» in [verification.md](../../wiki/workflows/verification.md), decisions, touched files, and deviations. A unit is `done` only when its verification ran in this task, per «Never Fake Success» in [integrity.md](../../wiki/principles/integrity.md).
6. When scope changes, edit «Scope» and «Units» before doing the new work. Pause and ask first when the change is material per «Stop, Pause, or Proceed» in integrity.md.
7. When the work ends, close out per «Closeout» in planning.md, registering leftover work with `github-issue-create`.

## Output
- The plan file path in `.ai/tasks/` and its current unit statuses.
- On closeout: the final state of every unit, the follow-up issues created, and confirmation that the plan file was deleted.
