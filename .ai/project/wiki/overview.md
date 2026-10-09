---
owns: "What this repository is: the package model, the package-first rule, development projects, directory map, commit scopes, human-facing doc layout, file encoding, and local-only verification"
volatility: evolving
reviewed: 2026-10-09
---

# Overview

Mu3Library is a set of reusable Unity UPM packages that external projects install through Git URLs. Package quality, stable public APIs, and clear assembly boundaries come first.

## When

- starting work here, or unsure which package or folder a change belongs to,
- choosing a commit scope or area label,
- editing README or CHANGELOG files.

## Route Away When

- package rules (surfaces, assemblies, optional integrations, C# conventions): [package-architecture.md](package-architecture.md),
- compiling and testing: [unity-verification.md](unity-verification.md),
- versions, tags, and release notes: [package-release.md](package-release.md).

## Package Model

| Package | Folder | Depends on | Scope and area label |
|---|---|---|---|
| Base | `Mu3Library_Base/` | — | `base` |
| URP | `Mu3Library_URP/` | Base | `urp` |
| Watermelon Game | `Mu3Library_Game_WatermelonGame/` | Base, URP | `watermelon`, never `game` |

## Package-First Rule

- Land behavior in the packages. The development projects consume the packages to exercise them; a feature never lives in a development project.
- Work inside a development project takes the area of the package it exercises.

## Development Projects

| Project | Exercises | Compile target |
|---|---|---|
| `UnityProject_BuiltIn` | Base on the Built-In pipeline; the default development context | `built-in` |
| `UnityProject_URP` | URP layered on Base | `urp` |
| `UnityProject_Game_WatermelonGame` | Watermelon Game | `watermelon` |

- Shared Base files compile in more than one project with different define sets, so one Base file can have more than one valid context. URP adds to the Built-In baseline and does not replace it for shared maintenance.
- Each project's `Library/` holds 1.3–2.3 GB (measured 2026-10-09), and a new worktree re-imports its own and lacks the sample junctions. This is why `policy.worktrees` is off. When a worktree is used anyway, never place it inside a `Library/` and never share a `Library/` between worktrees.

## Directory Map

| Path | Holds |
|---|---|
| `Mu3Library_*/Runtime/` | Package runtime: scripts, plus materials and prefabs in Base and shaders in URP |
| `Mu3Library_*/Editor/` | Editor tooling (Base, URP) |
| `Mu3Library_*/Samples~/` | Importable samples, registered under `samples` in each `package.json` |
| `Mu3Library_*/Tests/` | EditMode tests (Base, Watermelon Game) |
| `UnityProject_*/` | Development projects; `Assets/Mu3LibrarySamples*` is a Git-ignored junction to the package's `Samples~`, so each sample has one source |
| `tools/`, `compile-unity.sh`, `unity-cli-packages.tsv` | Repository support tooling, cataloged in [tools/README.md](../../../tools/README.md) |
| `docs/readme/`, `docs/changelog/` | Localized README and CHANGELOG |
| `docs/repository/CHANGELOG.md` | Repository workflow and tooling changes, kept out of package release notes |

## Commit Scopes

- A scope names a package (`base`, `urp`, `watermelon`), a surface inside one (`di`, `object-pool`, `audio`, `mvp`, `localization`, and similar), or repository-level work (`release`, `agents`, `workflow`, `tooling`, `project`).
- Area labels are `hosting.area_labels`: the three packages, `agents` for `.ai/` and the generated agent files, and `tooling` for `tools/` and `compile-unity.sh`.
- Commits written before Korean became the commit language keep their original language; never rewrite them.

## Human-Facing Docs

- `README.md` and `CHANGELOG.md` are English. Their `docs.locales` copies are `docs/readme/README.<locale>.md` and `docs/changelog/CHANGELOG.<locale>.md`; in `docs.locale_pattern`, `{stem}` is the primary file name without its extension.
- When `CHANGELOG.md` changes, check whether `README.md` needs the same change: new features, changed signatures, new define symbols, usage examples.
- `CHANGELOG.md` tracks package releases only, under per-package headers ([package-release.md](package-release.md)). Repository workflow and tooling changes go to `docs/repository/CHANGELOG.md`.
- `README.md` keeps its links to both localized READMEs and to `CHANGELOG.md`; `mu3-cli repo check` verifies them.

## File Encoding

- Markdown is UTF-8 with BOM and LF per `.editorconfig`, except agentkit files (`AGENTS.md`, `CLAUDE.md`, `.ai/`, and the generated tool folders), which stay UTF-8 without BOM because their frontmatter must start at the first byte.
- `.gitattributes` pins `*.md`, `*.sh`, and `*.tsv` to LF; bash reads a trailing CR as part of a value.

## Local-Only Verification

- The repository runs no GitHub Actions and no branch protection. Verification runs locally, and its commands and results go in the pull request body; a wrong pull request base is a stop condition, not a failing check.
- Local guards: `python .ai/kit/tools/agentkit.py check` for agent docs and `mu3-cli repo check` for repository hygiene.
