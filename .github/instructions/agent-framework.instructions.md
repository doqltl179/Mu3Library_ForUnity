---
applyTo: '**'
description: "Core multi-agent framework rules for Mu3Library orchestration and boundary control"
---

# Agent Framework Instructions

## Core Rules

- Build the agent framework through bounded iterations, not broad rewrites.
- Apply the development philosophy: "Break large features into multiple small features, and make each small feature an independent, non-overlapping capability. Develop with modularity and maintainability in mind."
- Break large features into smaller features, and define each small feature as an independent, non-overlapping capability.
- Use `docs/ai-agents/README.md` as the wiki index for framework documentation, and open only the smallest linked page that matches the current task.
- For AI-agent doc discovery, prefer the folder index that matches the current question shape: `routing/` for owner selection, `contracts/` for shared formats, `workflow/` for repeatable process, `guides/` for specialized edit procedures, and `architecture.md` for stable rationale.
- Treat routing as wiki navigation to the single owning page for a concern.
- When multiple agent docs share the same contract or sibling-owner routing rules, move that shared content into one wiki page and keep the agent files focused on their deltas.
- Apply the same rule to control-plane owner-selection and gate-order guidance.
- Default loop: one feature unit -> suitability review -> continue or rework.
- Prefer one owner per concern. If two agents appear to own the same concern, stop and re-scope before continuing.
- Keep governance roles separate from execution roles.
- Prefer modular, maintainable structures over bundled implementations.
- When framework docs start duplicating each other, consolidate the rule into one owning page and replace repeats with links.
- Do not create workaround-style alternate documentation paths for the same concern. If a second page starts acting like another procedure for the same surface, consolidate it into the owner page or formally re-scope ownership.

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

- update `docs/ai-agents/routing/agent-catalog.md` if the inventory changes,
- update `docs/ai-agents/architecture.md` if the control model changes,
- update `.github/copilot-instructions.md` if routing or discovery changes,
- add or revise a skill when a reusable workflow gains a stable input/output contract.

## Special Boundary Notes

- CLI and Python tooling must stay in auxiliary tooling scope unless the user explicitly requests product integration.
- Unity runtime/editor/package boundaries still take precedence over framework convenience.
- Do not replace the existing `unity` agent with split agents until the split has passed a suitability review.

## Memory Notes

- `.github/instructions/memory-policy.instructions.md` is the authoritative memory-routing policy for this framework.
- `docs/ai-agents/contracts/handoff-contract.md` defines the human-facing packet format and review-aware handoff flow.
- Until all requested review gates pass, treat current-iteration routing state as session-scoped.