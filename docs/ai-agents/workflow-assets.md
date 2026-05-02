# Workflow Assets

## Purpose

This document maps the reusable workflow entrypoints that support the Mu3Library agent framework without adding new domain owners.

## Current Assets

| Type | Artifact | Purpose |
|---|---|---|
| Prompt | `.github/prompts/compile-unity.prompt.md` | Chat entrypoint for synchronous compile-only verification |
| Prompt | `.github/prompts/development-idea-creator.prompt.md` | Chat entrypoint for repository-shaped Mu3Library ideation when package direction is unclear |
| Prompt | `.github/prompts/framework-next-unit.prompt.md` | Chat entrypoint for the bounded work -> review -> continue or rework loop |
| Skill | `.github/skills/development-idea-creator/SKILL.md` | Pre-unit concept shaping that summarizes package intent, ranks candidate ideas, and emits one concept brief |

## Compile-Only Verification

- Compile-only verification should use editor-safe `dotnet build` on the affected generated Unity `.csproj` files.
- Compile verification remains evidence for `reviewer`, not a substitute for reviewer approval.

## Design Rule

- Prefer workflow assets when the repository needs a repeatable flow but not a new long-lived execution owner.
- Promote workflow assets before adding a new agent when the change is procedural rather than ownership-driven.

## Ideation Boundary

- Repository-shaped ideation remains in the workflow-asset plane unless it proves a durable ownership gap that cannot be held by existing agents.
- The ideation asset may suggest likely downstream owners, but routing still returns to the main agent or `orchestrator`.
- The ideation asset stops at a concept brief and never emits task sequencing, verification ownership, or final owner selection.