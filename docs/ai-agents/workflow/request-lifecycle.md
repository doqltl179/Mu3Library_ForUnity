# Request Lifecycle

## When

- any user request starts and the order of work is the question,
- you need the stop-or-pause rule before implementing,
- you need to decide whether a request is one bounded unit or a graph.

This page owns the order in which one request is handled. It does not own owner selection, branch mechanics, or code rules; each step links to the page that does.

## Route Away When

- the framework itself is what changes: [iteration-process.md](iteration-process.md),
- only owner selection is unclear: [routing/README.md](../routing/README.md),
- only branch or commit sequencing is unclear: [git-workflow.md](git-workflow.md).

## Required Order

1. Extract scope, exclusions, and completion conditions from the request. Treat the current user request as authoritative; `tasks/plans/` records do not outrank it.
2. Assess functional feasibility and intended scope before implementing.
   - If the outcome is functionally impossible, stop and report why work cannot proceed.
   - If it is feasible but needs a material scope expansion, a major change, or a user choice that is not yours to make, pause and request that decision.
   - Do not invent a workaround rule or alternate behavior to make an unimplementable or underspecified request look complete. See [coding-rules.md](../coding-rules.md).
3. Select the smallest owner for the work in [routing/README.md](../routing/README.md), then read the smallest owning surface: the package page from [packages/README.md](../packages/README.md) for package work, plus [coding-rules.md](../coding-rules.md).
4. Run the branch preflight from [git-workflow.md](git-workflow.md) before the first edit.
5. Decide the shape of execution:
   - One bounded unit stays in the current worktree.
   - A complex request with two or more independently writable units goes to [graph-engineering.md](graph-engineering.md). Record the minimum justified nodes within the credit budget, serialize true conflicts, and create only nodes that pass the admission gate.
6. Implement inside the assigned write boundary, and sync the docs the change affects in the same unit. See [docs-sync.instructions.md](../../../.github/instructions/docs-sync.instructions.md).
7. Verify. Run the compile or safety checks that [verification.instructions.md](../../../.github/instructions/verification.instructions.md) selects for the touched surface.
8. For graph work, integrate through the fan-in order in [graph-engineering.md](graph-engineering.md), then run combined verification once on the integration branch.
9. Report the verification evidence, what was integrated, what was dropped and why, and every remaining undecided or manually verified item.

## Keep The Work Bounded

- Answer with the smallest clear result that completes the request; search narrowly with `rg` or a small file list. Detailed procedure: [token-budget.md](token-budget.md).
- Prefer bounded, non-overlapping work units with one owner per concern.
- Pass work between owners with the packet in [handoff-contract.md](../contracts/handoff-contract.md).
- Record a plan for non-trivial work using [plans/README.md](../plans/README.md).

## Notes

- Unity Editor runs, device runs, and any push are done only when separately requested.
- If a step's canonical page contradicts this order, the canonical page owns the detail and this page owns only the sequence.
