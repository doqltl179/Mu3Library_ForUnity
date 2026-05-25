# Workflow Assets

## Purpose

This document maps the reusable workflow entrypoints that support the Mu3Library agent framework without adding new domain owners.

## Current Assets

| Type | Artifact | Purpose |
|---|---|---|
| Prompt | `.github/prompts/compile-unity.prompt.md` | Chat entrypoint for synchronous compile-only verification |
| Prompt | `.github/prompts/development-idea-bank.prompt.md` | Chat entrypoint for repository-shaped Mu3Library idea-bank generation and whitespace discovery when package direction is unclear |
| Prompt | `.github/prompts/framework-next-unit.prompt.md` | Chat entrypoint for the bounded work -> review -> continue or rework loop |
| Skill | `.github/skills/development-idea-bank/SKILL.md` | Pre-unit idea-bank workflow that maps current capability, surfaces whitespace, ranks distinct directions, and preserves option space until a later deepening pass |

## Compile-Only Verification

- Compile-only verification should use editor-safe `dotnet build` on the affected generated Unity `.csproj` files.
- For VS Code and C# Dev Kit-driven verification, start from the tracked `UnityProject_BuiltIn/Mu3Library_ForUnity.code-workspace` file by default, and switch to `UnityProject_URP/Mu3Library_ForUnity.code-workspace` only when the URP context is the primary concern.
- Use `mu3-cli csdevkit` helpers to inspect the active context, run load diagnostics, choose curated compile-only build profiles, and generate repo-local support bundles.
- Compile verification remains evidence for `reviewer`, not a substitute for reviewer approval.

## Design Rule

- Prefer workflow assets when the repository needs a repeatable flow but not a new long-lived execution owner.
- Promote workflow assets before adding a new agent when the change is procedural rather than ownership-driven.

## Ideation Boundary

- Repository-shaped ideation remains in the workflow-asset plane unless it proves a durable ownership gap that cannot be held by existing agents.
- By default, the ideation asset should map current capability and whitespace before ranking ideas.
- Unless the user explicitly asks for refinement work, the ideation asset should bias toward net-new package surfaces, workflow multipliers, ecosystem bridges, or adoption wedges, keep the top 3 outside incremental polish, and allow at most one incremental baseline idea.
- The ideation asset may suggest handoff candidates, but routing still returns to the main agent or `orchestrator`.
- The ideation asset should return an idea bank or shortlist by default. A single concept brief is an explicit follow-up only when the user asks for it.

## Idea-Bank Contract

- `development-idea-bank.md` defines the role, output contract, anti-patterns, and refinement escape hatch for this workflow asset.
- When prompt, skill, or routing changes, confirm that the asset still preserves multiple new directions instead of collapsing into existing-feature enhancement.