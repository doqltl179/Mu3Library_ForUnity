---
owns: "How the agent organization runs: its structure, the order of a request, owner selection, delegation, handoffs, context use, and memory"
volatility: stable
reviewed: 2026-10-09
---

# Operating Model

Open this section when the question is about how work moves through the agent organization rather than about one workflow's rules.

<!-- agentkit:begin index -->
| Page | Owns |
|---|---|
| [architecture.md](architecture.md) | Why the kit is structured as it is: layers, planes, departments, the composition model, and how generation enforces single ownership |
| [concurrency.md](concurrency.md) | Whether units may run at the same time and how many: read and write scope analysis, ordering rules, machine capacity, the node budget, and fan-in |
| [context-budget.md](context-budget.md) | Token and context economy: how to read, search, and summarize narrowly, and the line budgets per file type |
| [delegation.md](delegation.md) | How an owner is run (subagent dispatch or role adoption) and the role protocol every owner follows |
| [handoff-contract.md](handoff-contract.md) | The packet passed between owners and the shape and language of the final report to the user |
| [memory-policy.md](memory-policy.md) | Where each kind of knowledge is stored (session, plan, project lessons and wiki, kit upstream, tool memory), what is never persisted, and how knowledge is promoted |
| [request-lifecycle.md](request-lifecycle.md) | The required order for handling one request, from confirming the request to the final report and cleanup |
| [routing.md](routing.md) | How to select the owner for a unit of work, break ties, and act when no owner fits |
<!-- agentkit:end index -->
