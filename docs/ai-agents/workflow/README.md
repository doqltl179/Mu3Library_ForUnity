# Workflow Wiki

This folder answers one question: what repeatable process or workflow asset should the agent follow?

## Question Shape

- bounded framework-change process,
- reusable workflow assets,
- ideation flow before choosing a bounded implementation unit.

## Open First

| If you need to... | Open | Why |
|---|---|---|
| Follow the bounded unit -> review -> continue or rework loop | [iteration-process.md](iteration-process.md) | Defines the required framework-change process and stop conditions |
| Check whether a repeatable flow should stay as a workflow asset instead of becoming a new agent | [workflow-assets.md](workflow-assets.md) | Tracks reusable prompts, skills, hooks, and supporting workflow assets |
| Widen option space before choosing a bounded package direction | [development-idea-bank.md](development-idea-bank.md) | Defines the repository-shaped ideation workflow contract |

## Route Away When

- the question is about who owns the work: use [routing/README.md](../routing/README.md)
- the question is about a shared packet or section format: use [contracts/README.md](../contracts/README.md)
- the question is about a concrete specialized procedure: use [guides/README.md](../guides/README.md)
- the question is still unclear: go back to [README.md](../README.md)

## Duplication Rule

- Do not restate the bounded review loop or workflow-asset inventory in unrelated docs.
- Link to the smallest page in this folder that already owns the needed process or workflow contract.