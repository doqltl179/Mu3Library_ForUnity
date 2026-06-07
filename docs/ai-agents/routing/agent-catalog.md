# Agent Catalog

## When

- choose the current durable owner,
- confirm whether an owner is active,
- find the detailed agent spec after routing selects an owner.

Route away for shared matrices: [control-plane-routing.md](control-plane-routing.md) or [unity-specialist-routing.md](unity-specialist-routing.md). Route away for stable rationale: [architecture.md](../architecture.md).

## Ownership Rule

This page owns long-lived agent inventory only. Individual specs own role-specific deltas; shared routing rules live in routing pages; workflow assets live in [workflow-assets.md](../workflow/workflow-assets.md).

## Active Agents

| Agent | Plane | Status | Owns | Spec |
|---|---|---|---|---|
| `orchestrator` | Governance | Active | Decompose work, choose next owner, coordinate gates | [spec](../../../.github/agents/orchestrator.agent.md) |
| `role-governor` | Governance | Active | Structural suitability for overlap, missing ownership, and routing ambiguity | [spec](../../../.github/agents/role-governor.agent.md) |
| `task-planner` | Execution | Active | Current-unit plans and progress records | [spec](../../../.github/agents/task-planner.agent.md) |
| `unity-runtime` | Execution | Active | Non-gated runtime package work | [spec](../../../.github/agents/unity-runtime.agent.md) |
| `unity-editor` | Execution | Active | Non-gated editor tooling work | [spec](../../../.github/agents/unity-editor.agent.md) |
| `package-integration` | Execution | Active | Define-gated optional package integrations | [spec](../../../.github/agents/package-integration.agent.md) |
| `docs-sync` | Execution | Active | Multilingual README and CHANGELOG synchronization | [spec](../../../.github/agents/docs-sync.agent.md) |
| `release-manager` | Execution | Active | Versioning, package release metadata, tags, branches, and GitHub Releases | [spec](../../../.github/agents/release-manager.agent.md) |
| `sample-integrity` | Execution | Active | Package samples, imported sample footprints, manifests, and smoke checks | [spec](../../../.github/agents/sample-integrity.agent.md) |
| `unity` | Execution | Narrowed | Cross-boundary Unity package work that no narrower owner can hold alone | [spec](../../../.github/agents/unity.agent.md) |
| `cli-platform` | Execution | Active | Repository-local Python bootstrap and auxiliary CLI workflows | [spec](../../../.github/agents/cli-platform.agent.md) |
| `reviewer` | Quality | Active | Regression, API, assembly, define-gate, docs, release, and verification review | [spec](../../../.github/agents/reviewer.agent.md) |

## Deferred Candidates

No deferred agent candidates are active. Add a new agent only when a bounded ownership gap is found and passes `role-governor` suitability review.

## Supporting Protocols

| Need | Open |
|---|---|
| Control-plane owner matrix | [control-plane-routing.md](control-plane-routing.md) |
| Unity specialist owner matrix | [unity-specialist-routing.md](unity-specialist-routing.md) |
| Shared agent spec contract | [agent-spec-contract.md](../contracts/agent-spec-contract.md) |
| Handoff and memory scope | [handoff-contract.md](../contracts/handoff-contract.md) |
| Workflow asset inventory | [workflow-assets.md](../workflow/workflow-assets.md) |
