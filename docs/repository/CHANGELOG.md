# Repository Workflow Changelog

This document tracks repository-level development workflow and tooling changes that do not belong in package release notes.

Package release notes remain in:
- `CHANGELOG.md`
- `docs/changelog/CHANGELOG.ko.md`
- `docs/changelog/CHANGELOG.ja.md`

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
