# Workflow Assets

## Purpose

This document maps the reusable workflow entrypoints that support the Mu3Library agent framework without adding new domain owners.

## Current Assets

| Type | Artifact | Purpose |
|---|---|---|
| Prompt | `.github/prompts/compile-unity.prompt.md` | Chat entrypoint for synchronous compile-only verification |
| Prompt | `.github/prompts/framework-next-unit.prompt.md` | Chat entrypoint for the bounded work -> review -> continue or rework loop |

## Compile-Only Verification

- Compile-only verification should use editor-safe `dotnet build` on the affected generated Unity `.csproj` files.
- Compile verification remains evidence for `reviewer`, not a substitute for reviewer approval.

## Design Rule

- Prefer workflow assets when the repository needs a repeatable flow but not a new long-lived execution owner.
- Promote workflow assets before adding a new agent when the change is procedural rather than ownership-driven.