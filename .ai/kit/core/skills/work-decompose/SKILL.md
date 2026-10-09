---
name: work-decompose
description: "Break a request into bounded units with one owner each: extract the goal and acceptance criteria, map the surfaces touched, select owners from the project catalog, find dependency edges, choose serial or parallel execution within the node budget, assign verification and review gates, and produce a unit table. Use when a request spans several owners or surfaces, or its owner is unclear."
category: planning
volatility: evolving
reviewed: 2026-10-09
---

# Decompose Work

## Use When
- A request spans more than one surface or owner.
- The right owner for a request is unclear.
- A plan needs its unit table before `task-plan-write` records it.

## Do Not Use When
- One owner clearly covers the whole request → select it per «Selection Procedure» in [routing.md](../../wiki/operating-model/routing.md) and dispatch directly.
- Requirements are undecided → `requirements-spec-write` first.
- Recording or updating the plan file → `task-plan-write`.

## Inputs
- The request and any linked issue, spec, or plan.
- `.ai/generated/catalog.md`: the active owners and their scopes.
- `[bindings.*]` in `.ai/project/profile.toml`: which paths each owner holds.

## Steps
1. State the goal in one sentence and list acceptance criteria as observable, testable outcomes. When interpretations diverge, pause and ask per «Stop, Pause, or Proceed» in [integrity.md](../../wiki/principles/integrity.md).
2. Map the surfaces the work touches: locate them with `rg -l` on key symbols and paths, reading only what the split needs per [context-budget.md](../../wiki/operating-model/context-budget.md). Record each surface's paths.
3. Split into units: one owner, one write scope, one verifiable outcome each. Select every owner per «Selection Procedure» in routing.md using `.ai/generated/catalog.md`; when none fits, follow «No Owner Fits» in routing.md.
4. Record each unit's write set and read set per «Scope Analysis» in [concurrency.md](../../wiki/operating-model/concurrency.md). Count the rule pages, profile, glossary, specs, and shared contracts a unit relies on as reads.
5. Order the units per «Ordering Rules» there: units that write the same thing are merged or ordered; a unit that reads what another writes waits for that unit's merge; cycles are merged or re-split. Write the reason on every `Depends on` edge.
6. Size the waves per «Capacity»: run `agentkit.py capacity`, give each unit a cost class by its heaviest step, and set the concurrent limit. Keep the unit count within «Node Budget».
7. Give each unit its verification per «Verification By Change Type» in [verification.md](../../wiki/workflows/verification.md) and its review gates per «Choosing Gates» in [review.md](../../wiki/workflows/review.md).
8. When the request is infeasible (an impossible criterion, conflicting constraints, a missing owner or command that blocks every path), stop and report the blocker instead of producing a partial table.
9. Set each unit's status per «Statuses» in [planning.md](../../wiki/workflows/planning.md) and build a dispatch packet for each ready unit per «Packet» in [handoff-contract.md](../../wiki/operating-model/handoff-contract.md).

## Output
- The goal and acceptance criteria.
- The unit table with the columns of «Units» in the [plan template](../../templates/plan.md): read and write sets, cost class, and each `Depends on` entry with its reason.
- The capacity result and the unit limits for «Budget» there.
- Dispatch packets for ready units, and any owner gap or blocker.
