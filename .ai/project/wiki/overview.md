---
owns: "What this repository is: the package model, the package-first rule, the project configs and where their Unity projects are generated, which checkout a Claude app session's unit uses, where units running at the same time put their worktrees, where each machine sets the work-folder root and how short `<unit>` is, removing a worktree, directory map, commit scopes, human-facing doc layout, file encoding, and local-only verification"
volatility: evolving
reviewed: 2026-11-17
---

# Overview

Mu3Library is a set of reusable Unity UPM packages that external projects install through Git URLs. Package quality, stable public APIs, and clear assembly boundaries come first.

## When

- starting work here, or unsure which package or folder a change belongs to (`project.guardrails` holds the package-first rule),
- choosing a commit scope or area label,
- starting or cleaning up a unit in a Claude app worktree, running units at the same time, removing a worktree, or making a folder outside the checkout,
- editing README or CHANGELOG files.

## Route Away When

- package rules (surfaces, assemblies, optional integrations, C# conventions): [package-architecture.md](rules/package-architecture.md),
- compiling and testing: [unity-verification.md](unity-verification.md),
- versions, tags, and release notes: [package-release.md](package-release.md),
- splitting a feature out of Base into a package of its own: [package-split.md](package-split.md).

## Package Model

| Package | Folder | Depends on | Scope and area label |
|---|---|---|---|
| Addressables | `packages/addressables/` | Base | `addressables` |
| Base | `packages/base/` | — | `base` |
| DI | `packages/di/` | Base | `di` |
| Input System | `packages/inputsystem/` | Base | `inputsystem` |
| Localization | `packages/localization/` | Base | `localization` |
| Notifications | `packages/notifications/` | — | `notifications` |
| Steam | `packages/steam/` | Base | `steam` |
| URP | `packages/urp/` | Base | `urp` |
| Watermelon Game | `packages/game/watermelon/` | Base, URP | `watermelon`, never `game` |

## Development Projects

| Config | Exercises | Compile target |
|---|---|---|
| `tools/unity-projects/full` | Addressables, Base, DI, Input System, Localization, Notifications, and Steam on the Built-In pipeline with every optional SDK, and Base's samples; the default development context | `full` |
| `tools/unity-projects/minimal` | Base and DI without any optional SDK | `minimal` |
| `tools/unity-projects/urp` | URP layered on Base | `urp` |
| `tools/unity-projects/watermelon` | Watermelon Game | `watermelon` |

- The repository commits no Unity project. The tools generate each config's project in the unit's work folder, `${MU3_WORK_ROOT}/Mu3Library_ForUnity/<unit>/<config>`, refresh it on every run, and keep its `Library/` there for the next run ([tools/README.md](../../../tools/README.md#unity-project-configs)). Shared Base files compile in every config with a different define set, so one Base file can have more than one valid context. URP adds to the Built-In baseline and does not replace it for shared maintenance.
- A project's `Library/` holds 1.3–2.3 GB (measured 2026-10-09 in the former development projects), and each unit's work folder holds its own, so a unit's first run imports each project it runs from scratch, in a worktree or not. Units that run at the same time get one worktree each, per «Worktrees» in [git-workflow.md](../../kit/core/wiki/workflows/git-workflow.md).
- A Claude app worktree (`.claude/worktrees/<name>`) is the checkout of the session that runs in it. Units of other sessions, each in its own app worktree, never work in it, and what they run in the main checkout either only reads or stops while it is off the integration branch (step 4 of `git-branch-start`, step 2 of `git-worktree-cleanup`), so they are not "another unit" for «Worktrees» or «Stop Conditions» in [git-workflow.md](../../kit/core/wiki/workflows/git-workflow.md): a unit that runs alone in a session cuts its branch in place in the session's checkout, once step 5 of [git-branch-start](../../kit/core/skills/git-branch-start/SKILL.md) finds no worktree under that checkout's `.worktrees/`. When that checkout is an app worktree, `git-worktree-cleanup` finds it in step 2 as the unit's worktree, but the app owns it: skip steps 4 to 6 for it, run step 7 with `git -C <main checkout>`, and before step 8 switch it to the session's own branch, fast-forwarded to `origin/<integration_branch>` (or detach it at `origin/<integration_branch>` when the session has no branch of its own), so that `git branch -d` can delete the task branch.
- Put the worktree of each unit that runs at the same time as another at `.worktrees/<short-name>` of the checkout the session runs in, such as `.worktrees/n430` for issue #430, instead of the branch-named path in step 4 of [git-branch-start](../../kit/core/skills/git-branch-start/SKILL.md). A session in a Claude app worktree (`.claude/worktrees/<name>`) uses that worktree, never the main checkout's `.worktrees/`: the app's edit guard rejects Edit and Write on every path of the main checkout outside the session's own worktree, that folder included (seen 2026-11-04). Keep the name short to keep the path shallow; the Unity projects lie in the work folder, not in the worktree, and the packages' own files stay below Windows' 260 characters there (step 5 of «Fresh Clone Setup» in [tools/README.md](../../../tools/README.md#fresh-clone-setup)). After the unit's pull request merges, `git-worktree-cleanup` removes the worktree, as the next line says.
- Before every `git worktree remove` here, including one outside `git-worktree-cleanup` such as for an unmerged or abandoned unit, first pass that skill's checks that the worktree has no unsaved changes and holds no other worktree. A worktree holds no samples link any more, but one of a branch from before #523 can still hold one and paths of 260 characters or more; [tools/README.md](../../../tools/README.md#worktree-removal) owns how to remove those and the recovery for a folder already left behind. Run the removal itself as `git -c core.longpaths=true worktree remove <worktree>`, which also covers such a worktree.
- A work folder that must lie outside the checkout («Work Folders» in [git-workflow.md](../../kit/core/wiki/workflows/git-workflow.md)) goes under a root that differs per machine, so `policy.work_root` reads it from the user environment variable `MU3_WORK_ROOT`, which each machine sets, such as to `D:/w`. Set it in the user's environment, never in `.env.local`: that file belongs to one checkout, so an app worktree has none, and only a terminal that enters the [development environment](../../../tools/README.md#development-environment) loads it, which agent shells do not. `<unit>` there is the first 32 characters of the task branch with slashes as dashes, without trailing dots, so two branches whose first 32 characters agree share it; a generated project's marker names its checkout, and a run from the other checkout stops and asks for `--work-dir` ([tools/README.md](../../../tools/README.md#unity-project-configs)). It is that short because Unity cannot read every package file from a project folder of 98 characters or more ([tools/README.md](../../../tools/README.md#fresh-clone-setup)): with it, each generated project, `<root>/Mu3Library_ForUnity/<unit>/<config>`, stays below that with a root of up to 33 characters, the verification build's in `<unit>/verify-build/full` with a root of up to 26, the multiplayer harness's in `<unit>/multiplayer-harness/minimal` with a root of up to 16, or of up to 4 with its `--editor-version`, which adds `-<version>` to that folder (`D:/w` gives 97 of the 98 characters), and a project with an `--editor-version` suffix, such as `watermelon-6000.3.17f1`, with a root of up to 21. `compile-unity.sh` and `test-unity.sh` take this folder by default and stop without `MU3_WORK_ROOT`; give `mu3-verify-build`, the DI stripping harness, and the multiplayer harness a folder of their own inside it, such as `<unit>/verify-build`, since each claims its work folder as a whole.

## Directory Map

| Path | Holds |
|---|---|
| `packages/<name>/Runtime/` | Package runtime, one folder per feature with the assets it needs, such as URP's `ScreenEffect/`, which holds the screen-effect shaders and the `Resources/` asset that keeps them in player builds |
| `packages/<name>/Editor/` | Editor tooling (Addressables, Base, DI, Input System, Localization) |
| `packages/<name>/Samples~/` | Importable samples, registered under `samples` in each `package.json` |
| `packages/<name>/Tests/` | EditMode tests in `Tests/Editor/` (each package that has tests) and PlayMode tests in `Tests/Runtime/` (Base and DI) |
| `tools/unity-projects/` | Project configs, from which the tools generate the Unity projects in the work folder; the `full` project links Base's `Samples~` (a junction on Windows, a symbolic link elsewhere), so each sample has one source. The URP and Watermelon Game packages ship no samples |
| `tools/`, `compile-unity.sh`, `test-unity.sh` | Repository support tooling, cataloged in [tools/README.md](../../../tools/README.md) |
| `docs/readme/`, `docs/changelog/` | Localized README and CHANGELOG |
| `docs/repository/CHANGELOG.md` | Repository workflow and tooling changes, kept out of package release notes |

## Commit Scopes

- A scope names a package (`addressables`, `base`, `di`, `inputsystem`, `localization`, `notifications`, `steam`, `urp`, `watermelon`), a surface inside one (`object-pool`, `audio`, `mvp`, and similar), or repository-level work (`release`, `agents`, `workflow`, `tooling`, `project`).
- Area labels are `hosting.area_labels`: one per package, named as its scope (`addressables`, `base`, `di`, `inputsystem`, `localization`, `notifications`, `steam`, `urp`, `watermelon`), `agents` for `.ai/` and the generated agent files, and `tooling` for `tools/`, `compile-unity.sh`, and `test-unity.sh`. A change to a project config takes `tooling`, as the rest of `tools/` does. The six labels of the split packages exist since 2026-11-16 by the user's decision; a new package's label is a GitHub label, so creating it waits for the user.
- Commits written before Korean became the commit language keep their original language; never rewrite them.

## Human-Facing Docs

- `README.md` and `CHANGELOG.md` are English; their localized copies are listed in `[docs.locale_paths]`.
- When `CHANGELOG.md` changes, check whether `README.md` needs the same change: new features, changed signatures, new define symbols, usage examples.
- `CHANGELOG.md` tracks package releases only, under per-package headers ([package-release.md](package-release.md)). Repository workflow and tooling changes go to `docs/repository/CHANGELOG.md`.
- `README.md` keeps its links to both localized READMEs and to `CHANGELOG.md`; `mu3-repo check` verifies them.

## File Encoding

- Markdown is UTF-8 with BOM and LF per `.editorconfig`; the agentkit section at its end keeps `.ai/` and the generated agent files without BOM.
- `.gitattributes` pins `*.md` and `*.sh` to LF; bash reads a trailing CR as part of a value. The agentkit section at its end, which `agentkit.py update` maintains, pins the kit, the overlay, and the generated agent files to LF («Line Endings» in `.ai/kit/core/wiki/integration/installation.md`).

## Local-Only Verification

- The repository runs no GitHub Actions and no branch protection (`hosting.ci = false`), so a wrong pull request base is a stop condition, not a failing check.
- Local guards: `python .ai/kit/tools/agentkit.py check` for agent docs, and `mu3-repo check`, which runs repository hygiene checks and then that check.
