---
applyTo: ".github/agents/**,.github/instructions/**,.github/prompts/**,.github/skills/**,.github/workflows/repo-hygiene.yml,docs/ai-agents/**,tasks/**"
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

- Keep interactive answers, searches, and command summaries short.
- Use `docs/ai-agents/workflow/token-budget.md` for detailed low-token procedure.
- Open `docs/ai-agents/README.md` only when the next page is not obvious.
- Route through section indexes, then stop at the smallest owning page.
- Do not read every agent spec during framework discovery; use `routing/README.md` until an owner is selected.
- Prefer links over repeated summaries in agent specs, instructions, and wiki indexes.

## Suitability Gate

After any non-trivial agent-framework change, check:

- role overlap,
- missing ownership,
- routing ambiguity,
- repository-boundary violations,
- required catalog or router updates.

`role-governor` owns the structural continue-or-rework disposition. `orchestrator` routes to the gate but does not self-approve structural expansion.

## Required Artifact Updates

- Update `docs/ai-agents/routing/README.md` when owner inventory changes.
- Update `docs/ai-agents/architecture.md` when the stable control model changes.
- Update `.github/copilot-instructions.md` when startup routing or discovery changes.
- Add or revise a skill only when a reusable workflow gains a stable input/output contract.

## AI-Agent Doc Change Rules

- When adding or editing agent-framework docs, keep one canonical owner per rule and replace nearby duplicates with links.
- Prefer extending an existing router, leaf rule page, contract, or workflow page before creating a new document.
- Add a new document only when it owns a distinct question shape, boundary, or reusable contract that does not already have an owner.
- If a new owning page is introduced, update the smallest relevant router instead of adding broad cross-links or parallel indexes.
- Remove or shrink stale pointer files and duplicated summaries as soon as the owning page fully absorbs their role. At task closeout, follow the [Task Record Policy](../../docs/ai-agents/plans/task-record-policy.md): completed plans are removed, not retained as repository history.

## Boundaries

- CLI and Python tooling stay in auxiliary tooling scope unless product integration is explicitly requested.
- Unity runtime/editor/package boundaries take precedence over framework convenience.
- Do not replace the narrowed `unity` agent unless the split passes suitability review.
- `.github/instructions/memory-policy.instructions.md` is the operative memory policy; `docs/ai-agents/contracts/handoff-contract.md` is the human-facing packet reference.
