# AI Agent Framework Docs

This directory is the human-facing map for the Mu3Library multi-agent framework.

## Goals

- Build the framework through small, reviewable iterations instead of one large rewrite.
- Keep agent responsibilities narrow enough that ownership is obvious.
- Separate control-plane concerns from execution-plane concerns.
- Preserve repository-specific guardrails such as Unity assembly boundaries, define symbols, multilingual docs, and release flow.

## Documents In This Folder

- `architecture.md`: control-plane, execution-plane, quality-plane, and memory-plane design.
- `iteration-process.md`: the required feature-unit -> suitability-review -> continue-or-rework loop.
- `agent-catalog.md`: current and planned agent inventory, including boundary notes.
- `handoff-contract.md`: structured handoff packet format, memory routing, and review-aware state transitions.
- `workflow-assets.md`: reusable skills, prompts, hooks, and scripts that support framework execution without adding new owners.

Related tooling:

- `tools/mu3_cli/README.md`: bootstrap and usage guide for the repository-local Python CLI.

## Foundation Scope

Iteration 1 established the foundation needed before broader agent expansion:

- A central orchestration model.
- A governance model for detecting role overlap.
- A constrained CLI/platform manager for auxiliary tooling.
- Reusable skills for CLI bootstrap and role audits.

## Current Rollout State

Approved in the current rollout:

- memory and handoff protocol for the control plane,
- split Unity specialists for runtime, editor, and optional-package ownership,
- dedicated docs-sync ownership for multilingual README and CHANGELOG synchronization,
- dedicated release-manager ownership for versioning, release-scoped manifest metadata, branch and tag execution, release packaging, and GitHub Release execution,
- dedicated sample-integrity ownership for sample manifests, package samples, imported sample footprints, and sample smoke checks,
- a compile-only workflow gate that waits for compile completion before the next unit proceeds,
- a tooling-local Python CLI bootstrap under `tools/mu3_cli`.

No deferred ownership candidates are currently approved in the rollout backlog.

## Design Notes

This framework intentionally combines:

- flow-style execution control for predictable sequencing,
- role-specialized agents for bounded autonomy,
- explicit state and memory handling,
- CLI conventions that start small and scale through subcommands.

Those patterns were chosen because they map well to Mu3Library's package-first Unity workflow.