# Repository Workflow Changelog

This document tracks repository-level development workflow and tooling changes that do not belong in package release notes.

Package release notes remain in:
- `CHANGELOG.md`
- `docs/changelog/CHANGELOG.ko.md`
- `docs/changelog/CHANGELOG.ja.md`

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
