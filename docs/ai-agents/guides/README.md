# Guides Wiki

This folder answers one question: how should the agent perform a concrete specialized edit safely?

## Question Shape

- concrete procedure for a specialized surface,
- task already has an owner but still needs a verified edit workflow,
- surface-specific validation rules rather than routing or shared contracts.

## Open First

| If you need to... | Open | Why |
|---|---|---|
| Edit Unity scene or prefab YAML directly | [unity-yaml-guide.md](unity-yaml-guide.md) | Defines the verified direct-YAML workflow, anchors, and validation rules for serialized Unity assets |

## Route Away When

- the question is about who owns the work: use [routing/README.md](../routing/README.md)
- the question is about a shared packet or section format: use [contracts/README.md](../contracts/README.md)
- the question is about repeatable process or workflow assets: use [workflow/README.md](../workflow/README.md)
- the question is still unclear: go back to [README.md](../README.md)

## Duplication Rule

- Do not restate a specialized edit procedure in routing, contract, or workflow docs.
- Link to the owning guide in this folder instead.