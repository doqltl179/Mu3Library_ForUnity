# Workflow Assets

## Purpose

This document maps the reusable workflow entrypoints that support the Mu3Library agent framework without adding new domain owners.

## Current Assets

| Type | Artifact | Purpose |
|---|---|---|
| Prompt | `.github/prompts/compile-unity.prompt.md` | Chat entrypoint for synchronous compile-only verification |
| Prompt | `.github/prompts/development-idea-creator.prompt.md` | Chat entrypoint for repository-shaped Mu3Library ideation and whitespace discovery when package direction is unclear |
| Prompt | `.github/prompts/framework-next-unit.prompt.md` | Chat entrypoint for the bounded work -> review -> continue or rework loop |
| Skill | `.github/skills/development-idea-creator/SKILL.md` | Pre-unit concept shaping that maps current capability, surfaces whitespace, ranks distinct ideas, and emits one concept brief |

## Compile-Only Verification

- Compile-only verification should use editor-safe `dotnet build` on the affected generated Unity `.csproj` files.
- Compile verification remains evidence for `reviewer`, not a substitute for reviewer approval.

## Design Rule

- Prefer workflow assets when the repository needs a repeatable flow but not a new long-lived execution owner.
- Promote workflow assets before adding a new agent when the change is procedural rather than ownership-driven.

## Ideation Boundary

- Repository-shaped ideation remains in the workflow-asset plane unless it proves a durable ownership gap that cannot be held by existing agents.
- By default, the ideation asset should map current capability and whitespace before ranking ideas.
- Unless the user explicitly asks for refinement work, the ideation asset should bias toward net-new package surfaces, workflow multipliers, or ecosystem bridges and allow at most one incremental baseline idea.
- The ideation asset may suggest handoff candidates, but routing still returns to the main agent or `orchestrator`.
- The ideation asset stops at a concept brief and never emits task sequencing, verification ownership, or final owner selection.

## Ideation Evaluation

- Use `development-idea-creator-evaluation.md` when prompt, skill, or routing changes could affect ideation behavior.
- The evaluation set must check both broad whitespace discovery and the explicit incremental-refinement escape hatch.