---
owns: "How an owner is run (subagent dispatch or role adoption) and the role protocol every owner follows"
volatility: evolving
reviewed: 2026-10-09
---

# Delegation

Every rendered agent file links here. This page owns the protocol that is the same for every role, so cards only state what is specific to them.

## When

- acting as, or dispatching, an owner.

## Route Away When

- choosing the owner: [routing.md](routing.md),
- the packet passed between owners: [handoff-contract.md](handoff-contract.md),
- whether and how many units run at the same time, and fan-in: [concurrency.md](concurrency.md).

## Running An Owner

| Tool capability | How to run the owner |
|---|---|
| The tool supports subagents and a rendered agent exists | Dispatch the subagent with a handoff packet; keep the main session as coordinator |
| No subagents, or the owner is not rendered | Adopt the role: read its card, then follow this protocol in the main session |
| The owner is `orchestrator` | Run it in the main session; subagents usually cannot dispatch further subagents, so a dispatched orchestrator returns a unit plan instead of dispatching |

Dispatch costs tokens and coordination. Use it when the unit is independent, large, or benefits from a fresh context; adopt the role for small units.

## Role Protocol

1. Read the card, the «Project Binding» in the rendered file (paths, stack packs, notes), and the bound stack packs before editing.
2. Stay inside `Owns` and the bound paths. When work falls under `Does Not Own`, stop that part and hand it off with a packet; do not do it "just this once".
3. Run the role's `Domain Checks` and the verification the change needs before reporting.
4. Never approve your own work; quality gates belong to the quality plane.
5. Report in the shape of [handoff-contract.md](handoff-contract.md), including anything you could not do.
6. Do not create further units or dispatch other owners unless you are `orchestrator`; report newly found scope instead.
