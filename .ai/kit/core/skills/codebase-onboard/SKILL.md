---
name: codebase-onboard
description: "Map an unfamiliar codebase quickly while reading narrowly: entry points, build and test commands, directory map, architecture, and conventions; propose values for .ai/project/profile.toml (commands, bindings, stacks) and draft .ai/project/wiki/overview.md. Use when the kit is newly installed in a project, or work starts in a codebase or area that no project doc describes."
category: planning
volatility: evolving
reviewed: 2026-10-09
---

# Onboard To A Codebase

## Use When
- The kit was just installed and `.ai/project/profile.toml` still has empty `commands.*`, no bindings, or no active stacks.
- `.ai/project/wiki/overview.md` is missing or contradicts the code.
- Work starts in a repository area no project doc describes.

## Do Not Use When
- Installing or updating the kit files → `kit-install` or `kit-update`.
- Diagnosing one specific failure → `bug-diagnose`.
- A detected language or framework has no stack pack → `kit-stack-add`.

## Inputs
- Repository access and the request that triggered onboarding.
- The current `.ai/project/profile.toml`, if any, and the [profile template](../../templates/project/profile.toml) for what each key means.

## Steps
1. Read narrowly per [context-budget.md](../../wiki/operating-model/context-budget.md): list before opening, read manifests and docs before source, and stop reading a file once the needed fact is found.
2. Inventory the tree by weight: `git ls-files | cut -d/ -f1-2 | sort | uniq -c | sort -rn | head -40`. Read `docs.readme` and any contributing guide.
3. Identify stacks from manifests and lockfiles (for example `package.json`, `pyproject.toml`, `go.mod`, `Cargo.toml`, `*.csproj`, `build.gradle`, `Package.swift`) and from the CI configuration.
4. Derive each `commands.*` candidate from evidence only: manifest scripts, task-runner targets, CI job steps, and README instructions. Run each candidate that only builds, tests, or checks the working copy, and record the result per «Evidence Format» in [verification.md](../../wiki/workflows/verification.md). Never run deploy, publish, or data-changing commands. Leave a key empty when no evidence exists.
5. Find the entry points: main functions, server bootstraps, app manifests, route registrations, CLI definitions, and package export roots.
6. Sketch the architecture: top-level components, how they communicate (calls, HTTP, queues, shared database), where data persists, and which external services they use. Back each edge with a file reference.
7. Record observed conventions: layering, naming, error handling, test layout, and branch and commit style from `git log --oneline -30`.
8. Map each major path to an owner per «Selection Procedure» in [routing.md](../../wiki/operating-model/routing.md), and derive `bindings.<agent>` paths and stacks, `stacks.active`, and `agents.enabled`.
9. Write the draft `.ai/project/wiki/overview.md`: purpose, directory map, architecture, entry points, commands, conventions, and unknowns, citing a file path for each claim.
10. Present the proposed profile values with their evidence and wait for the user's confirmation before writing them. After they are applied, run `agentkit.py sync`, then `agentkit.py check`, and fix every error in `.ai/project/`.

## Output
- Proposed profile values (`project.*`, `commands.*`, `bindings.*`, `stacks.active`, `agents.enabled`), each with its evidence.
- The draft `.ai/project/wiki/overview.md`.
- Unknowns, and commands left empty or unverified.
