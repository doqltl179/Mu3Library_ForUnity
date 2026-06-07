---
name: development-idea-bank
description: "Use when the user needs a Mu3Library idea bank of genuinely new package directions and wants to avoid collapsing back into refinement of current features."
---

# Development Idea Bank

## Purpose

Generate a Mu3Library package idea bank before a bounded implementation unit exists. Default to whitespace discovery, adjacent-new package surfaces, workflow multipliers, ecosystem bridges, and adoption wedges.

Detailed contract: `../../../docs/ai-agents/workflow/development-idea-bank.md`.

## Use This Skill When

- the next package direction is unclear,
- prior ideas feel repetitive,
- the user wants a repository-shaped shortlist before planning,
- a pain point may indicate adjacent whitespace rather than a direct refinement task.

## Default Rules

- Start from repository evidence and current package intent.
- Build a capability map and whitespace map before proposing solutions.
- Unless the user explicitly asks for refinement, keep the top ideas in `net-new` or `adjacent-new` territory.
- Keep at most one low-priority incremental baseline idea.
- Preserve multiple viable directions; do not collapse into one concept brief unless the user asks after seeing the bank.

## Required Output

- package-intent summary,
- capability map,
- whitespace map,
- six ranked ideas with bucket, novelty class, why-new evidence, likely package surfaces, and primary risk,
- short hooks for which idea to deepen next.

## Guardrails

- Repository constraints beat web research.
- Use web research only after the whitespace map exists and only to widen adjacent patterns.
- Do not produce execution sequencing, final owner selection, or verification ownership.
- If the user asks for incremental refinement, state that mode explicitly and keep the work inside the named surfaces.
