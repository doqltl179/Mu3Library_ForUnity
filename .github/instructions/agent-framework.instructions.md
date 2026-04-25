---
applyTo: '**'
description: "Core multi-agent framework rules for Mu3Library orchestration and boundary control"
---

# Agent Framework Instructions

## Core Rules

- Build the agent framework through bounded iterations, not broad rewrites.
- Default loop: one feature unit -> suitability review -> continue or rework.
- Prefer one owner per concern. If two agents appear to own the same concern, stop and re-scope before continuing.
- Keep governance roles separate from execution roles.

## Required Suitability Gate

After any non-trivial agent-framework change, check:

- role overlap,
- missing ownership,
- routing ambiguity,
- repository-boundary violations,
- catalog and router updates required by the change.

The `role-governor` owns the structural suitability disposition. The `orchestrator` routes work to that gate but does not self-approve structural expansion.

If the fit is unclear, revise the structure before adding more agents.

## Required Artifacts

When adding or changing agent-framework documents:

- update `docs/ai-agents/agent-catalog.md` if the inventory changes,
- update `docs/ai-agents/architecture.md` if the control model changes,
- update `.github/copilot-instructions.md` if routing or discovery changes,
- add or revise a skill when a reusable workflow gains a stable input/output contract.

## Special Boundary Notes

- CLI and Python tooling must stay in auxiliary tooling scope unless the user explicitly requests product integration.
- Unity runtime/editor/package boundaries still take precedence over framework convenience.
- Do not replace the existing `unity` agent with split agents until the split has passed a suitability review.

## Memory Notes

- `.github/instructions/memory-policy.instructions.md` is the authoritative memory-routing policy for this framework.
- `docs/ai-agents/handoff-contract.md` defines the human-facing packet format and review-aware handoff flow.
- Until all requested review gates pass, treat current-iteration routing state as session-scoped.