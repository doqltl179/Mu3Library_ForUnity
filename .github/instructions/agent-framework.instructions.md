---
applyTo: '**'
description: "Compact multi-agent framework rules for Mu3Library routing and boundary control"
---

# Agent Framework Instructions

## Core Rules

- Work in bounded units; do not bundle unrelated framework changes.
- Keep one owner per concern. If two agents appear to own the same concern, stop and re-scope.
- Keep governance roles separate from execution roles.
- Keep shared rules in one owning wiki page and link to it from nearby docs.
- Do not create workaround-style alternate procedures for the same concern.

## Token Budget Rules

- For the detailed procedure, use `docs/ai-agents/workflow/token-budget.md`.
- Open `docs/ai-agents/README.md` only when the next page is not obvious.
- Use folder indexes as routers: `routing/`, `contracts/`, `workflow/`, `guides/`, then the smallest matching leaf page.
- Do not read every agent spec during framework discovery. Use `routing/agent-catalog.md` for inventory and open a spec only after that owner is selected.
- Prefer links over repeated summaries in agent specs, instructions, and wiki indexes.
- Summarize large command output instead of copying it into handoff or review text.

## Suitability Gate

After any non-trivial agent-framework change, check:

- role overlap,
- missing ownership,
- routing ambiguity,
- repository-boundary violations,
- required catalog or router updates.

`role-governor` owns the structural continue-or-rework disposition. `orchestrator` routes to the gate but does not self-approve structural expansion.

## Required Artifact Updates

- Update `docs/ai-agents/routing/agent-catalog.md` when owner inventory changes.
- Update `docs/ai-agents/architecture.md` when the stable control model changes.
- Update `.github/copilot-instructions.md` when startup routing or discovery changes.
- Add or revise a skill only when a reusable workflow gains a stable input/output contract.

## Boundaries

- CLI and Python tooling stay in auxiliary tooling scope unless product integration is explicitly requested.
- Unity runtime/editor/package boundaries take precedence over framework convenience.
- Do not replace the narrowed `unity` agent unless the split passes suitability review.
- `.github/instructions/memory-policy.instructions.md` is the operative memory policy; `docs/ai-agents/contracts/handoff-contract.md` is the human-facing packet reference.
