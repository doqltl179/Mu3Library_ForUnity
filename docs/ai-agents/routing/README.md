# Routing Wiki

This folder answers one question: who owns the current work?

## Question Shape

- owner selection,
- rollout-approved owner inventory,
- shared owner-selection before opening a specific agent spec.

## Open First

| If you need to... | Open | Why |
|---|---|---|
| Confirm which agents are approved and active right now | [agent-catalog.md](agent-catalog.md) | Tracks durable owners, rollout status, and links to each agent spec |
| Choose among control-plane agents | [control-plane-routing.md](control-plane-routing.md) | Centralizes shared owner-selection and gate-order rules for control-plane work |
| Choose among Unity specialist agents | [unity-specialist-routing.md](unity-specialist-routing.md) | Centralizes shared owner-selection and split rules for Unity package work |

## Route Away When

- the question is about a shared packet or section format: use [contracts/README.md](../contracts/README.md)
- the question is about repeatable process or workflow assets: use [workflow/README.md](../workflow/README.md)
- the question is about a concrete specialized procedure: use [guides/README.md](../guides/README.md)
- the question is still unclear: go back to [README.md](../README.md)

## Duplication Rule

- Do not create another routing summary for the same owner decision.
- Link to the smallest routing page in this folder that already owns it.