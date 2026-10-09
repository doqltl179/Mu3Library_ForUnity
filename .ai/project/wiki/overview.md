---
owns: "What this repository is: the package model, the package-first rule, development projects, directory map, commit scopes, human-facing doc layout, file encoding, and local-only verification"
volatility: evolving
reviewed: 2026-10-09
---

# Overview

Mu3Library is a set of reusable Unity UPM packages that external projects install through Git URLs. Package quality, stable public APIs, and clear assembly boundaries come first.

## When

- starting work here, or unsure which package or folder a change belongs to (`project.guardrails` holds the package-first rule),
- choosing a commit scope or area label,
- editing README or CHANGELOG files.

## Route Away When

- package rules (surfaces, assemblies, optional integrations, C# conventions): [package-architecture.md](rules/package-architecture.md),
- compiling and testing: [unity-verification.md](unity-verification.md),
- versions, tags, and release notes: [package-release.md](package-release.md).

## Package Model

| Package | Folder | Depends on | Scope and area label |
|---|---|---|---|
| Base | `Mu3Library_Base/` | — | `base` |
| URP | `Mu3Library_URP/` | Base | `urp` |
| Watermelon Game | `Mu3Library_Game_WatermelonGame/` | Base, URP | `watermelon`, never `game` |

## Development Projects

| Project | Exercises | Compile target |
|---|---|---|
| `UnityProject_BuiltIn` | Base on the Built-In pipeline; the default development context | `built-in` |
| `UnityProject_URP` | URP layered on Base | `urp` |
| `UnityProject_Game_WatermelonGame` | Watermelon Game | `watermelon` |

- Shared Base files compile in more than one project with different define sets, so one Base file can have more than one valid context. URP adds to the Built-In baseline and does not replace it for shared maintenance.
- Each project's `Library/` holds 1.3–2.3 GB (measured 2026-10-09), which a fresh worktree re-imports, and a fresh worktree lacks the sample junctions below. This is why `policy.worktrees` is off.

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
- Area labels are `hosting.area_labels`: the three packages, `agents` for `.ai/` and the generated agent files, and `tooling` for `tools/` and `compile-unity.sh`. Work inside a development project takes the area of the package it exercises.
- Commits written before Korean became the commit language keep their original language; never rewrite them.

## Human-Facing Docs

- `README.md` and `CHANGELOG.md` are English; their localized copies are listed in `[docs.locale_paths]`.
- When `CHANGELOG.md` changes, check whether `README.md` needs the same change: new features, changed signatures, new define symbols, usage examples.
- `CHANGELOG.md` tracks package releases only, under per-package headers ([package-release.md](package-release.md)). Repository workflow and tooling changes go to `docs/repository/CHANGELOG.md`.
- `README.md` keeps its links to both localized READMEs and to `CHANGELOG.md`; `mu3-cli repo check` verifies them.

## File Encoding

- Markdown is UTF-8 with BOM and LF per `.editorconfig`; the agentkit section at its end keeps `.ai/` and the generated agent files without BOM.
- `.gitattributes` pins `*.md`, `*.sh`, and `*.tsv` to LF; bash reads a trailing CR as part of a value.

## Local-Only Verification

- The repository runs no GitHub Actions and no branch protection (`hosting.ci = false`), so a wrong pull request base is a stop condition, not a failing check.
- Local guards: `python .ai/kit/tools/agentkit.py check` for agent docs, and `mu3-cli repo check`, which runs repository hygiene checks and then that check.
