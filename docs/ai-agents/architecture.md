# Multi-Agent Architecture

## Intent

Mu3Library needs a multi-agent framework that can scale without turning into a set of overlapping prompts. The design goal is not to maximize agent count. The design goal is to create a small control plane that can coordinate several specialized agents safely.

## Architecture Layers

### 1. Governance Plane

- `orchestrator`: decomposes work, chooses the next agent, enforces the iteration loop, and owns the current task graph.
- `role-governor`: audits agent boundaries, overlap, duplication risk, and owns the structural suitability disposition for each non-trivial framework iteration.

These agents do not own Unity feature implementation. They own coordination and structural fitness.

### 2. Execution Plane

- `task-planner`: turns the current assigned unit into verifiable steps and keeps progress records aligned. It does not own cross-agent routing when the orchestrator is active.
- `unity-runtime`: owns non-gated runtime work for Base and URP packages.
- `unity-editor`: owns non-gated editor tooling work for Base and URP packages.
- `package-integration`: owns define-gated optional package integrations across runtime, editor, and package surfaces.
- `docs-sync`: owns multilingual README and CHANGELOG synchronization after verified implementation changes.
- `release-manager`: owns versioning, release-scoped manifest metadata, release packaging, branch and tag execution, and GitHub Release publication through the documented release workflow.
- `sample-integrity`: owns package sample manifests, `Samples~` content, imported sample footprints, and sample smoke-check flows.
- `unity`: remains available only for genuinely cross-boundary Unity package tasks that cannot be cleanly assigned to one narrower Unity specialist.
- `cli-platform`: manages auxiliary Python CLI and environment bootstrap work for repository tooling.

`docs-sync` owns only the synchronized documentation surface inside a release flow. `release-manager` owns the actual release execution path, and `reviewer` remains the release-readiness gate.

Execution-plane agents should produce concrete artifacts. They should not redefine the whole system while working.

### 3. Quality Plane

- `reviewer`: checks regressions, public API safety, define guards, assembly boundaries, docs sync, and release readiness.
- future verification specialists can be added later if the reviewer becomes too broad.

### 4. Workflow Asset Plane

- `instructions`: always-on or scoped operating rules.
- `skills`: reusable task bundles with stable inputs and outputs.
- `prompts`: narrow entry points for repeatable user-facing tasks.
- `hooks`: deterministic guardrails for preflight checks.

Current workflow assets include a compile-only gate implemented as a skill, prompt entrypoints, a hook, and supporting scripts. That workflow exists to wait for compile completion before the next unit proceeds without introducing a new compile-specific execution owner.

Supporting documents for this plane should also define:

- memory routing policy,
- structured handoff packets,
- review-aware persistence transitions.

### 5. Memory Plane

- repository memory stores durable project facts and conventions.
- session memory stores current-task state.
- user memory stores cross-project user preferences.

Orchestrator decides what is durable enough to persist after the requested review gates complete. If a handoff note is only useful until the current suitability review completes, keep it in session memory. Promote it to repository memory only after all requested review gates validate the convention.

The detailed routing and packet format are defined in `handoff-contract.md` and `.github/instructions/memory-policy.instructions.md`.

Source owners can propose persistence, but orchestrator records final promotion after review.

## Iteration Model

Every non-trivial framework change follows this loop:

1. Select one bounded feature unit.
2. Add or refine the smallest useful artifact for that unit.
3. Run a structural suitability review through `role-governor` and add `reviewer` when quality or verification approval is also needed.
4. Continue only if the change fits the whole design.
5. If the fit is poor, rework the structure before adding more layers.

This prevents agent sprawl and keeps the framework legible.

## Agent Spec Contract

Every agent document should define the following fields explicitly:

- Mission
- Primary responsibilities
- Non-goals
- Required inputs
- Expected outputs
- Escalation triggers
- Coordination dependencies
- Review triggers

If any of those fields are vague, the agent is not ready to add.

## Boundaries For Newly Requested Managers

### CLI Manager

The requested CLI manager is structurally valid only if it stays narrow:

- It owns Python environment bootstrap, CLI command architecture, and automation UX.
- It does not own Unity runtime/editor feature logic.
- It should prefer auxiliary tooling directories and package metadata instead of touching product assemblies.
- It must declare safe roots and forbidden roots so that tooling work does not leak into shipped package surfaces.

That is why iteration 1 introduces `cli-platform` rather than a broad system administrator.

### Agent Role Manager

The requested agent role manager is structurally valid only as a governance function:

- It audits role overlap and missing ownership.
- It recommends merge, split, remove, or re-scope actions.
- It does not implement the delegated work itself.
- It owns the structural continue-or-rework decision for each non-trivial framework iteration.

That is why iteration 1 introduces `role-governor` rather than a manager that tries to do both governance and delivery.

## Near-Term Expansion Candidates

These are intentionally deferred until the current split structure proves stable:

No additional deferred agents are approved right now. Add more owners only after a new structural gap survives the suitability gate.

The current `unity` agent remains in place as a narrow cross-boundary fallback after sample-only ownership moved to `sample-integrity`.