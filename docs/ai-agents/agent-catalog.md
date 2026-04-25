# Agent Catalog

## Control Model

- `orchestrator` owns specialist selection and cross-unit sequencing.
- `task-planner` owns plan detail for the current assigned unit.
- `role-governor` owns the structural continue-or-rework disposition.
- `reviewer` owns verification and quality approval.

## Current Control Plane

| Agent | Status | Primary Responsibility | Explicit Non-Goals | Review Trigger |
|---|---|---|---|---|
| `orchestrator` | New in iteration 1 | Decompose work, choose the next specialist, enforce the iteration loop | Deep domain implementation, structural self-approval | When sequencing becomes ambiguous or a second control plane appears |
| `role-governor` | New in iteration 1 | Detect overlap, missing ownership, and weak boundaries across agents | Feature delivery, code implementation, release execution | On every non-trivial framework iteration and when a new agent is proposed |

## Current Execution Plane

| Agent | Status | Primary Responsibility | Explicit Non-Goals | Review Trigger |
|---|---|---|---|---|
| `task-planner` | Existing | Turn the current assigned unit into small verifiable steps and maintain progress records | Cross-agent routing and long-term role governance | When plans drift from actual structure or verification is missing |
| `unity-runtime` | New in iteration 2 | Own non-gated runtime work for Base and URP packages | Editor tooling, define-gated integration ownership, framework routing | When runtime changes spill into editor, samples, or optional integration ownership |
| `unity-editor` | New in iteration 2 | Own non-gated editor tooling for Base and URP packages | Runtime ownership, define-gated integration ownership, framework routing | When editor changes spill into runtime or optional-package ownership |
| `package-integration` | New in iteration 2 | Own define-gated optional package integrations across runtime, editor, and package surfaces | Broad non-gated runtime/editor work and framework routing | When define symbols, asmdefs, or optional dependency surfaces change |
| `docs-sync` | New in iteration 3 | Own multilingual README and CHANGELOG synchronization based on verified source changes | Release execution, versioning, and primary feature implementation ownership | When doc drift appears across languages or doc sync and release flow become entangled |
| `release-manager` | New in iteration 3 | Own versioning, release-scoped manifest metadata, release packaging, branch and tag execution, and GitHub Release publication | Multilingual docs synchronization, optional-package manifest ownership, reviewer approval, and primary feature implementation ownership | When package versions, release-owned manifest metadata, release notes, branch sync, or GitHub Release execution are in scope |
| `sample-integrity` | New in iteration 3 | Own sample manifests, `Samples~` content, imported sample footprints, and sample smoke checks | Broad core runtime/editor implementation, release flow, and generic cross-boundary routing | When sample-only package work, import paths, or sample smoke checks are in scope |
| `unity` | Existing, narrowed in iteration 3 | Own genuinely cross-boundary Unity package changes that span narrower Unity specialists | Sample-only work, routine runtime-only, editor-only, or define-gated integration-only work | When multiple Unity specialists are touched and no narrower owner can hold the task alone |
| `cli-platform` | New in iteration 1 | Bootstrap Python environments and design auxiliary CLI workflows for repository tooling inside approved tooling roots | Unity gameplay/runtime feature implementation | When CLI scope starts leaking into product assemblies or package metadata |

`docs-sync` owns only the synchronized documentation surface inside release work. `release-manager` owns the actual release execution path through `.github/instructions/release.instructions.md` and `.github/instructions/git-workflow.instructions.md`.

## Current Quality Plane

| Agent | Status | Primary Responsibility | Explicit Non-Goals | Review Trigger |
|---|---|---|---|---|
| `reviewer` | Existing | Audit regressions, API stability, gates, docs sync, and verification evidence | Orchestration and long-term catalog governance | When public APIs, asmdefs, define gates, docs, or releases change |

## Deferred Candidates

No additional deferred candidates are active right now. Add new agents only after a new bounded ownership gap is found and passes suitability review.

## Skills Introduced In Iteration 1

| Skill | Owner | Purpose |
|---|---|---|
| `bootstrap-python-cli` | `cli-platform` | Design or refine auxiliary Python CLI tooling and environment bootstrap flow |
| `agent-role-audit` | `role-governor` | Audit overlap, missing ownership, and split/merge decisions for agents |
| `unity-compile-gate` | Workflow asset plane | Run compile-only verification, wait for completion, and block the next unit until compile state is known |

## Supporting Protocols

| Artifact | Purpose |
|---|---|
| `docs/ai-agents/handoff-contract.md` | Defines the structured handoff packet, memory scope matrix, and review-aware persistence rules |
| `.github/instructions/memory-policy.instructions.md` | Enforces repository-specific memory routing rules during agent work |
| `docs/ai-agents/workflow-assets.md` | Maps reusable skills, prompts, hooks, and scripts such as the compile-only gate |