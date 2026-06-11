---
applyTo: '**'
description: "Compact multi-agent framework rules for Mu3Library routing and boundary control"
---

# Agent Framework Instructions

## Core Rules

- Development philosophy: break large features into smaller features, and break small features into independent, non-overlapping units.
- Prefer modular, structural, systematic clean code over tightly coupled task-local shortcuts.
- Work in bounded units; do not bundle unrelated framework changes.
- Keep one owner per concern. If two agents appear to own the same concern, stop and re-scope.
- Keep governance roles separate from execution roles.
- Keep shared rules in one owning wiki page and link to it from nearby docs.
- Do not create workaround-style alternate procedures for the same concern.

## Token Budget Rules

- In interactive sessions, answer with the smallest clear result that completes the request.
- Do not restate the request, repeat obvious context, or add long preambles unless they change the decision.
- Expand only when the user explicitly asks for detail or the task is high-risk enough that brevity would hide a material risk.
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

## AI-Agent Doc Change Rules

- When adding or editing agent-framework docs, keep one canonical owner per rule and replace nearby duplicates with links.
- Prefer extending an existing router, contract, workflow page, or local `AGENTS.md` before creating a new document.
- Add a new document only when it owns a distinct question shape, boundary, or reusable contract that does not already have an owner.
- If a new owning page is introduced, update the smallest relevant router instead of adding broad cross-links or parallel indexes.
- Remove or shrink stale pointer files, duplicated summaries, and completed plan artifacts as soon as the owning page fully absorbs their role.

## Boundaries

- CLI and Python tooling stay in auxiliary tooling scope unless product integration is explicitly requested.
- Unity runtime/editor/package boundaries take precedence over framework convenience.
- Do not replace the narrowed `unity` agent unless the split passes suitability review.
- `.github/instructions/memory-policy.instructions.md` is the operative memory policy; `docs/ai-agents/contracts/handoff-contract.md` is the human-facing packet reference.
