# Contracts Wiki

This folder answers one question: what shared contract should the agent follow?

## Question Shape

- shared packet or section format,
- handoff packet or persistence rule,
- reusable contract that should not be recopied into several docs.

## Open First

| If you need to... | Open | Why |
|---|---|---|
| Keep multiple agent specs consistent without duplicating shared rules | [agent-spec-contract.md](agent-spec-contract.md) | Defines what stays local in an agent spec versus what moves to a shared wiki page |
| Prepare or review a handoff packet and decide memory scope | [handoff-contract.md](handoff-contract.md) | Defines the handoff packet, routing handoff rules, and review-aware persistence |

## Route Away When

- the question is about who owns the work: use [routing/README.md](../routing/README.md)
- the question is about repeatable process or workflow assets: use [workflow/README.md](../workflow/README.md)
- the question is about a concrete specialized procedure: use [guides/README.md](../guides/README.md)
- the question is still unclear: go back to [README.md](../README.md)

## Duplication Rule

- Do not restate these contracts in several agent or routing docs.
- Link to the owning contract page in this folder instead.