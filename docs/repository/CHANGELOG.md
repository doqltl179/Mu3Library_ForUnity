# Repository Workflow Changelog

This document tracks repository-level development workflow and tooling changes that do not belong in package release notes.

Package release notes remain in:
- `CHANGELOG.md`
- `docs/changelog/CHANGELOG.ko.md`
- `docs/changelog/CHANGELOG.ja.md`

## 2026-10-24

### Changed
- `UnityProject_URP` lists the URP package under `testables`, so `test-unity.sh urp`, and `all` or `changed` with a URP change, runs the URP package's EditMode tests, new in `Mu3Library_URP/Tests/Editor`, where the target used to be reported as skipped. `tools/README.md` and the project's verification page say so.

## 2026-10-21

### Added
- `test-unity.sh` and `mu3-cli unity test` run the EditMode tests through the Unity Test Framework CLI, and `commands.test` points at them. They take the targets and options of `compile-unity.sh`, which the new script sources for target selection, the Editor lookup, the lock check, and the isolated mirror, so a project open in an Editor is tested in a mirror. A project runs the packages its manifest lists under `testables`, so `urp` is skipped. A failed test, a failed Unity run, a missing result file, or a run without tests exits non-zero, such a failure does not stop the remaining targets, and NUnit XML lands in `log/unity-tests/`. On 6000.6.7f1 `watermelon` passes 54 of 54 tests and `built-in` 50 of 50.
- `mu3-cli unity link-samples` creates the links from the development projects' `Assets/` to the packages' `Samples~` folders, which a fresh clone or worktree lacked and no command created, and `commands.worktree_setup` points at it. The link paths are a new optional fourth column of `unity-cli-packages.tsv`. The command leaves a correct link alone, replaces a link that points elsewhere, and stops without touching a real folder of the same name. Because Unity compiles the samples only through these links, `compile-unity.sh` and `test-unity.sh` stop before Unity when a selected project lacks its link, unless `--without-samples` is given, and `mu3-cli unity doctor` fails its `Sample link` check. `tools/README.md` gives the setup order for a fresh clone.

### Changed
- All three development projects run on Unity 6000.6.7f1: `UnityProject_BuiltIn` from 6000.3.10f1, `UnityProject_URP` from 6000.3.10f1, and `UnityProject_Game_WatermelonGame` from 6000.3.17f1. The upgrade raised their Unity packages (URP 17.6.0, Input System 1.20.1, Localization 1.5.13, Mobile Notifications 2.5.0, Timeline 6.6.0, uGUI 2.6.0, and others), added the `physicscore2d`, `tetgen`, and `timelinefoundation` modules, and dropped the `vr` module. UniTask is locked to 2.5.11 (`2e993ff`) in the two projects that install it, the first release whose editor tracker window compiles on 6.6. No development project stays on an older editor, so the packages' `"unity": "6000.0"` floor is no longer compiled locally.

### Fixed
- The URP project's `packages-lock.json` records the Base version `Mu3Library_URP/package.json` asks for, so compiling the `urp` target no longer rewrites it.
- `compile-unity.sh` and `mu3-cli unity doctor` no longer take a `Temp/UnityLockfile` left by a crashed Editor for an open project. A project counts as open only while a process holds the file, which PowerShell checks on Windows and `lsof` elsewhere; a stale file is reported as such and the compile runs in place instead of re-importing a mirror from scratch or failing under `--in-place`. `--dry-run` prints where each target would run.
- A mirror that `compile-unity.sh` cannot remove no longer ends the script before it reports Unity's result. Removal retries briefly, then warns with the leftover path; the exit status follows Unity, and the remaining targets still run.
- `mu3-cli unity changes` and `unity compile` run from PowerShell. They found bash only on `PATH`, where Git for Windows does not put it; they now also look next to `git` and in the default install locations, and skip the WSL launcher in `System32`. A missing bash is a usage error that says what to install instead of a traceback from `unity changes`, and `unity doctor` fails its new `Bash` check. `compile-unity.sh` reads `UNITY_HUB_EDITOR_ROOT` as the doctor already did, so both accept the same Editor; `tools/README.md` lists the variables both read.
- `mu3-cli repo info` lists all three packages from `unity-cli-packages.tsv`, the tooling docs and `.gitignore` describe three development projects and the `repo` and `unity` command groups, `tools/cli/README.md` no longer says an in-place compile leaves `Library/` untouched, and the compile selection and repository check no longer special-case the removed root `tasks/` folder.
- `commands.test_tools` passes again: a test pinned the editor versions the 6000.6.7f1 upgrade replaced, and now checks that each project names one.

## 2026-10-11

### Fixed
- `compile-unity.sh changed` selects every development project that consumes a changed package. A package used to select only the project `unity-cli-packages.tsv` pairs it with, so a `Mu3Library_Base` change compiled in `UnityProject_BuiltIn` alone. That project installs every optional package, so code outside its define guard, or an API break against URP or Watermelon Game, passed the compile gate. A project consumes a package when its `Packages/manifest.json` references it: a Base change now selects `built-in`, `urp`, and `watermelon`, and a URP change selects `urp` and `watermelon`. `tools/cli/tests/test_compile_selection.py` runs the script against a throwaway repository to keep the rule in place.

## 2026-10-09 (agentkit 0.2.0)

### Changed
- Updated agentkit to 0.2.0. `profile.toml` now keeps only the values this project sets: three `project.guardrails`, `hosting.ci = false`, per-document localized paths, the three packages under `[[release.packages]]`, the `unity-upm` stack pack, and a `test_tools` command bound to the tooling role.
- The package rules moved to `.ai/project/wiki/rules/package-architecture.md`, which tools load whenever files under `Mu3Library_*/` are touched.
- The Unity YAML guide follows the kit's route order for serialized assets: live Editor, then a batch-mode Editor script, then a declared direct-YAML edit. Samples are edited in place through the development projects' junctions, and the ScreenEffect anchors were re-verified; the `ToonPanel` template the guide named no longer exists.
- `.editorconfig` carries the kit's own BOM-free section for agent files instead of the hand-written one.

## 2026-10-09

### Changed
- The AI-agent docs now come from the shared [agentkit](https://github.com/doqltl179/ai-agents) v0.1.0, installed as a read-only copy in `.ai/kit/`. Project facts moved to the overlay in `.ai/project/`: parameters, active roles, and path bindings in `profile.toml`; package architecture, Unity verification, package release formats, and the Unity YAML guide in `wiki/`; two local roles (`optional-integration-engineer`, `sample-integrity-engineer`), three local skills (`work-issues`, `asmdef-triage`, `development-idea-bank`), and `lessons.md`. `AGENTS.md`, `CLAUDE.md`, and the `.claude/`, `.codex/`, `.agents/`, and `.github/` agent files are generated by `python .ai/kit/tools/agentkit.py sync` and are not edited by hand.
- Units cut their task branch in the main checkout (`policy.worktrees = false`), because each development project's `Library/` takes 1.3–2.3 GB and a new worktree re-imports it.
- `mu3-cli repo check` runs `agentkit.py check` after its hygiene checks. `.editorconfig` keeps agentkit files UTF-8 without BOM, since the kit reads frontmatter from the first byte.

### Removed
- Removed the hand-written agent framework: `docs/ai-agents/`, `.github/agents`, `.github/instructions`, `.github/prompts`, `.github/skills`, `.claude/commands/work-issues.md`, `tasks/`, and the hand-written `AGENTS.md`, `CLAUDE.md`, and `.github/copilot-instructions.md`. Rules the kit already owns were dropped rather than moved; the `mu3-cli agents` command group, which validated that framework's shape, went with it.

## 2026-08-26

### Removed
- Removed the last GitHub Actions workflow, `.github/workflows/branch-strategy.yml`, and with it the `.github/workflows/` directory. Its `validate-branch-flow` job compared only a pull request's base and head branch names against the branch policy. Neither `develop` nor `main` carries branch protection or a ruleset, so the job never gated a merge and a red result would have blocked nothing. The branch policy it echoed is unchanged and stays owned by `docs/ai-agents/workflow/git-workflow.md`, where a wrong destination is a stop condition instead of a failing check. This repository now runs no GitHub Actions at all: verification is local, and the commands and their results belong in the pull request body.

## 2026-08-15

### Changed
- Compile-only verification runs the Unity Editor CLI through `compile-unity.sh` instead of `dotnet build` against the generated Unity `.csproj` files. The isolated mirror that arrived with `mu3-cli unity` removed the reason the batch path was dropped on 2026-05-02, because a compile no longer needs the target Editor closed. A generated `.csproj` also drifts from the sources it claims to build, which silently invalidates the verification it was trusted for.

### Removed
- Removed the `mu3-cli csdevkit` command group. Its `build-profile run` compiled the generated Unity `.slnx` and `.csproj` with `dotnet build` and presented that as compile-only verification, which is the path this repository no longer verifies through. Package-identity and package-version drift validation went with the group and is not covered anywhere else at the moment.

### Fixed
- `compile-unity.sh` runs on Windows. The Unity Hub lookup covers macOS, Windows, and Linux instead of hardcoding the macOS path, the isolated mirror falls back to `cp -RL` wherever `rsync` is absent as it is in Git Bash, and a staging path long enough to push a git-URL package clone past the Windows path limit is reported before Unity fails on it.
- `*.sh` and `*.tsv` are pinned to LF in `.gitattributes`. Under `core.autocrlf=true` a Windows checkout gave `unity-cli-packages.tsv` CRLF endings, bash read the trailing CR as part of the configured project path, and every target failed with a missing-directory error.

## 2026-08-13

### Changed
- Moved repository document layout, README link, stale agent-routing reference, tracked tooling artifact, and agent-framework shape validation into `mu3-cli repo check`.

### Removed
- Removed the post-push repository-hygiene GitHub Actions workflow; repository hygiene is now an explicit local tooling check.

## 2026-08-11

### Changed
- Renamed the outer Python tooling directory from `tools/mu3_cli` to `tools/cli` while retaining the `mu3_cli` import namespace and `mu3-cli` command name.
- Added change-aware Unity compile orchestration and environment diagnostics under `mu3-cli unity`.
- Moved package-identity metadata validation into `mu3-cli csdevkit drift check` and removed the redundant standalone `tools/csdevkit_tests` xUnit project.

## 2026-06-21

### Removed
- Removed package-local `AGENTS.md` documents and matching `.meta` files from the importable `Mu3Library_Base` and `Mu3Library_URP` package surfaces so Unity package import no longer depends on agent-doc assets that are now owned only by the repository root and wiki routing docs.

## 2026-05-20

### Added
- Added tracked `Mu3Library_ForUnity.code-workspace` files for the Built-In and URP development workspaces.
- Added the `mu3-cli csdevkit` workflow for context switching, load diagnostics, curated compile-only build profiles, support bundles, and drift checks.
- Added `tools/csdevkit_tests`, a standalone xUnit project targeting `net10.0` so C# Dev Kit can discover a narrow pure C# metadata test surface without loading Unity package assemblies.

## 2026-05-02

### Removed
- Removed the repository Unity batch compile-gate workflow, scripts, hooks, and editor batch entrypoints because that path required the target Unity editor to be closed.
- Compile-only verification now relies on editor-safe `dotnet build` against the generated Unity `.csproj` files, and the batch SceneLoader smoke entrypoint was removed with that workflow.
