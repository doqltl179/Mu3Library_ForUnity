---
owns: "What this repository is: the package model, the package-first rule, development projects, where units running at the same time put their worktrees, removing a worktree that holds sample links or paths of 260 characters or more, directory map, commit scopes, human-facing doc layout, file encoding, and local-only verification"
volatility: evolving
reviewed: 2026-10-24
---

# Overview

Mu3Library is a set of reusable Unity UPM packages that external projects install through Git URLs. Package quality, stable public APIs, and clear assembly boundaries come first.

## When

- starting work here, or unsure which package or folder a change belongs to (`project.guardrails` holds the package-first rule),
- choosing a commit scope or area label,
- running units at the same time, or removing a worktree,
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
- Each project's `Library/` holds 1.3–2.3 GB (measured 2026-10-09), which a fresh worktree re-imports. This is why `policy.worktrees` is off: units run one at a time in place. Units that run at the same time ([concurrency.md](../../kit/core/wiki/operating-model/concurrency.md)) still get one worktree each: start each with [git-branch-start](../../kit/core/skills/git-branch-start/SKILL.md) as if `policy.worktrees` were on, taking its worktree steps (the target-path check in step 3, then steps 4 and 6) instead of step 5, at the path in the next line. A fresh worktree also lacks the sample links below until `commands.worktree_setup` creates them.
- Put each such worktree at `.worktrees/<short-name>` of the checkout the session runs in, such as `.worktrees/n430` for issue #430, instead of step 4's branch-named path. A session in a Claude app worktree (`.claude/worktrees/<name>`) uses that worktree, never the main checkout's `.worktrees/`: the app's edit guard rejects Edit and Write on every path of the main checkout outside the session's own worktree, that folder included (seen 2026-11-04). Keep the name short to keep the path shallow; even so, a path under a session worktree stays too deep for Unity's asset import to read every package file, and that limit and its remedy (`subst`) are owned by step 5 of «Fresh Clone Setup» in [tools/README.md](../../../tools/README.md#fresh-clone-setup). After the unit's pull request merges, `git-worktree-cleanup` removes the worktree, with the teardown in the next line.
- Before every `git worktree remove` here, including one outside `git-worktree-cleanup` such as for an unmerged or abandoned unit, first pass that skill's checks that the worktree has no unsaved changes and holds no other worktree, then run `commands.worktree_teardown` inside it and stop if it fails, as the skill's teardown step does. Like `commands.worktree_setup`, its value is POSIX shell syntax, so run it in the skill's POSIX form from Git Bash; PowerShell cannot start it, and the skill's PowerShell form then stops. A real folder or file in a link's place is Git-ignored, so `git status` never showed it, and the removal would delete it without warning. [tools/README.md](../../../tools/README.md#worktree-removal) owns the command's behavior and the recovery for a folder already left behind. A worktree under a session worktree also holds tracked paths of 260 characters or more, which Git for Windows deletes only with `core.longpaths` on in the checkout the removal runs from, and a unit worktree's own setting does not reach the main checkout: run the removal there as `git -c core.longpaths=true worktree remove <worktree>`, and take the evidence and the deletion of a folder a removal without it left behind from the same section.

## Directory Map

| Path | Holds |
|---|---|
| `Mu3Library_*/Runtime/` | Package runtime: scripts, plus materials and textures in Base, and in URP shaders and the `Resources/` asset that keeps the screen-effect shaders in player builds |
| `Mu3Library_*/Editor/` | Editor tooling (Base) |
| `Mu3Library_*/Samples~/` | Importable samples, registered under `samples` in each `package.json` |
| `Mu3Library_*/Tests/` | EditMode tests (each package) |
| `UnityProject_*/` | Development projects; `Assets/Mu3LibrarySamples*` is a Git-ignored link to the package's `Samples~` (a junction on Windows, a symbolic link elsewhere), so each sample has one source |
| `tools/`, `compile-unity.sh`, `test-unity.sh`, `unity-cli-packages.tsv` | Repository support tooling, cataloged in [tools/README.md](../../../tools/README.md) |
| `docs/readme/`, `docs/changelog/` | Localized README and CHANGELOG |
| `docs/repository/CHANGELOG.md` | Repository workflow and tooling changes, kept out of package release notes |

## Commit Scopes

- A scope names a package (`base`, `urp`, `watermelon`), a surface inside one (`di`, `object-pool`, `audio`, `mvp`, `localization`, and similar), or repository-level work (`release`, `agents`, `workflow`, `tooling`, `project`).
- Area labels are `hosting.area_labels`: the three packages, `agents` for `.ai/` and the generated agent files, and `tooling` for `tools/`, `compile-unity.sh`, and `test-unity.sh`. Work inside a development project takes the area of the package it exercises.
- Commits written before Korean became the commit language keep their original language; never rewrite them.

## Human-Facing Docs

- `README.md` and `CHANGELOG.md` are English; their localized copies are listed in `[docs.locale_paths]`.
- When `CHANGELOG.md` changes, check whether `README.md` needs the same change: new features, changed signatures, new define symbols, usage examples.
- `CHANGELOG.md` tracks package releases only, under per-package headers ([package-release.md](package-release.md)). Repository workflow and tooling changes go to `docs/repository/CHANGELOG.md`.
- `README.md` keeps its links to both localized READMEs and to `CHANGELOG.md`; `mu3-cli repo check` verifies them.

## File Encoding

- Markdown is UTF-8 with BOM and LF per `.editorconfig`; the agentkit section at its end keeps `.ai/` and the generated agent files without BOM.
- `.gitattributes` pins `*.md`, `*.sh`, and `*.tsv` to LF; bash reads a trailing CR as part of a value. The agentkit section at its end, which `agentkit.py update` maintains, pins the kit, the overlay, and the generated agent files to LF («Line Endings» in `.ai/kit/core/wiki/integration/installation.md`).

## Local-Only Verification

- The repository runs no GitHub Actions and no branch protection (`hosting.ci = false`), so a wrong pull request base is a stop condition, not a failing check.
- Local guards: `python .ai/kit/tools/agentkit.py check` for agent docs, and `mu3-cli repo check`, which runs repository hygiene checks and then that check.
