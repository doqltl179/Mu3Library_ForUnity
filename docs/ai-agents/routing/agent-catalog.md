# Agent Catalog

## When

- you need to choose the current owner for a task,
- you need to confirm whether an agent is active, narrowed, or deferred,
- you need the rollout-approved inventory without re-reading architecture prose.

## Route Away When

- the question is about stable framework design or wiki topology: use [architecture.md](../architecture.md),
- the question is about shared agent-spec structure: use [agent-spec-contract.md](../contracts/agent-spec-contract.md),
- the question is about shared control-plane or Unity owner-selection rules: use [control-plane-routing.md](control-plane-routing.md) or [unity-specialist-routing.md](unity-specialist-routing.md),
- the question is about workflow assets or the bounded review loop: use [workflow-assets.md](../workflow/workflow-assets.md) or [iteration-process.md](../workflow/iteration-process.md).

## Owns

- durable owner inventory and rollout-approved status.
- the routing index to detailed agent operating contracts under `.github/agents/`.

## Rollout Snapshot

### Foundation Scope

Iteration 1 established the foundation needed before broader agent expansion:

- a central orchestration model,
- a governance model for detecting role overlap,
- a constrained CLI/platform manager for auxiliary tooling,
- reusable skills for CLI bootstrap and role audits.

### Current Rollout State

Approved in the current rollout:

- memory and handoff protocol for the control plane,
- split Unity specialists for runtime, editor, and optional-package ownership,
- dedicated docs-sync ownership for multilingual README and CHANGELOG synchronization,
- dedicated release-manager ownership for versioning, release-scoped manifest metadata, branch and tag execution, release packaging, and GitHub Release execution,
- dedicated sample-integrity ownership for sample manifests, package samples, imported sample footprints, and sample smoke checks,
- a workflow-asset ideation entrypoint for pre-unit idea-bank shaping and package-whitespace discovery when package direction is unclear,
- a compile-only workflow asset that supports editor-safe verification evidence for reviewer without becoming a separate control-plane gate,
- a tooling-local Python CLI bootstrap under `tools/mu3_cli`,
- a tracked C# Dev Kit workspace flow that starts from the Built-In workspace by default and uses `mu3-cli csdevkit` helpers for context switching, diagnostics, support bundles, and drift checks.

No additional deferred ownership candidates are currently approved in the rollout backlog.

## Control Model

- Shared control-plane owner selection and gate order live in [control-plane-routing.md](control-plane-routing.md).
- Use this catalog for approved inventory and rollout status, not as a second control-plane routing summary.

## Current Control Plane

| Agent | Spec | Status | Primary Responsibility | Explicit Non-Goals | Review Trigger |
|---|---|---|---|---|---|
| `orchestrator` | [orchestrator.agent.md](../../.github/agents/orchestrator.agent.md) | New in iteration 1 | Decompose work, choose the next specialist, enforce the iteration loop | Deep domain implementation, structural self-approval | When sequencing becomes ambiguous or a second control plane appears |
| `role-governor` | [role-governor.agent.md](../../.github/agents/role-governor.agent.md) | New in iteration 1 | Detect overlap, missing ownership, and weak boundaries across agents | Feature delivery, code implementation, release execution | On every non-trivial framework iteration and when a new agent is proposed |

## Current Execution Plane

| Agent | Spec | Status | Primary Responsibility | Explicit Non-Goals | Review Trigger |
|---|---|---|---|---|---|
| `task-planner` | [task-planner.agent.md](../../.github/agents/task-planner.agent.md) | Existing | Turn the current assigned unit into small verifiable steps and maintain progress records | Cross-agent routing and long-term role governance | When plans drift from actual structure or verification is missing |
| `unity-runtime` | [unity-runtime.agent.md](../../.github/agents/unity-runtime.agent.md) | New in iteration 2 | Own non-gated runtime work for Base and URP packages | Editor tooling, define-gated integration ownership, framework routing | When runtime changes spill into editor, samples, or optional integration ownership |
| `unity-editor` | [unity-editor.agent.md](../../.github/agents/unity-editor.agent.md) | New in iteration 2 | Own non-gated editor tooling for Base and URP packages | Runtime ownership, define-gated integration ownership, framework routing | When editor changes spill into runtime or optional-package ownership |
| `package-integration` | [package-integration.agent.md](../../.github/agents/package-integration.agent.md) | New in iteration 2 | Own define-gated optional package integrations across runtime, editor, and package surfaces | Broad non-gated runtime/editor work and framework routing | When define symbols, asmdefs, or optional dependency surfaces change |
| `docs-sync` | [docs-sync.agent.md](../../.github/agents/docs-sync.agent.md) | New in iteration 3 | Own multilingual README and CHANGELOG synchronization based on verified source changes | Release execution, versioning, and primary feature implementation ownership | When doc drift appears across languages or doc sync and release flow become entangled |
| `release-manager` | [release-manager.agent.md](../../.github/agents/release-manager.agent.md) | New in iteration 3 | Own versioning, release-scoped manifest metadata, release packaging, branch and tag execution, and GitHub Release publication | Multilingual docs synchronization, optional-package manifest ownership, reviewer approval, and primary feature implementation ownership | When package versions, release-owned manifest metadata, release notes, branch sync, or GitHub Release execution are in scope |
| `sample-integrity` | [sample-integrity.agent.md](../../.github/agents/sample-integrity.agent.md) | New in iteration 3 | Own sample manifests, `Samples~` content, imported sample footprints, and sample smoke checks | Broad core runtime/editor implementation, release flow, and generic cross-boundary routing | When sample-only package work, import paths, or sample smoke checks are in scope |
| `unity` | [unity.agent.md](../../.github/agents/unity.agent.md) | Existing, narrowed in iteration 3 | Own genuinely cross-boundary Unity package changes that span narrower Unity specialists | Sample-only work, routine runtime-only, editor-only, or define-gated integration-only work | When multiple Unity specialists are touched and no narrower owner can hold the task alone |
| `cli-platform` | [cli-platform.agent.md](../../.github/agents/cli-platform.agent.md) | New in iteration 1 | Bootstrap Python environments and design auxiliary CLI workflows for repository tooling inside approved tooling roots | Unity gameplay/runtime feature implementation | When CLI scope starts leaking into product assemblies or package metadata |

`docs-sync` owns only the synchronized documentation surface inside release work. `release-manager` owns the actual release execution path through `.github/instructions/release.instructions.md` and `.github/instructions/git-workflow.instructions.md`.

## Current Quality Plane

| Agent | Spec | Status | Primary Responsibility | Explicit Non-Goals | Review Trigger |
|---|---|---|---|---|---|
| `reviewer` | [reviewer.agent.md](../../.github/agents/reviewer.agent.md) | Existing | Audit regressions, API stability, gates, docs sync, and verification evidence | Orchestration and long-term catalog governance | When public APIs, asmdefs, define gates, docs, or releases change |

## Deferred Candidates

No additional deferred candidates are active right now. Add new agents only after a new bounded ownership gap is found and passes suitability review.

## Workflow Asset Inventory Boundary

`agent-catalog.md` is the authoritative inventory for long-lived agent owners.

Prompt and skill inventory lives in `docs/ai-agents/workflow/workflow-assets.md`.
When a reusable flow is useful but does not create a new durable owner, document it there instead of expanding this catalog.

## Supporting Protocols

| Artifact | Purpose |
|---|---|
| `docs/ai-agents/contracts/handoff-contract.md` | Defines the structured handoff packet, memory scope matrix, and review-aware persistence rules |
| `docs/ai-agents/contracts/agent-spec-contract.md` | Defines which agent-spec sections stay local versus move into shared wiki pages |
| `docs/ai-agents/routing/control-plane-routing.md` | Defines the shared owner-selection matrix and gate-order rules for control-plane agents |
| `docs/ai-agents/routing/unity-specialist-routing.md` | Defines the shared owner-selection matrix and split rules for Unity specialist agents |
| `.github/instructions/memory-policy.instructions.md` | Enforces repository-specific memory routing rules during agent work |
| `docs/ai-agents/workflow/workflow-assets.md` | Maps reusable skills, prompts, hooks, and scripts that support the workflow asset plane |