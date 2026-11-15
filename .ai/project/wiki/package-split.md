---
owns: "What a unit that splits a feature out of Base into a package of its own changes: the new package's files, moving the code with its GUIDs and dropping its SDK gate, the configs, tools, and profile, and the docs up to the package's first release"
volatility: evolving
reviewed: 2026-11-15
---

# Package Split

How a feature leaves Base for a package of its own under ADR 0001 (D1, D5, D7, D11, D20, D21), as unit 7 (#533) split Notifications into `packages/notifications`. Units 8 to 12 follow it, with their own decisions in the ADR (D8, D9, D19, D22) on top.

## When

- moving a feature of Base into a package of its own.

## Route Away When

- surfaces, assembly direction, gates, and the `package.json` dependency rules: [package-architecture.md](rules/package-architecture.md),
- the first release, its tag, and its install URL: [package-release.md](package-release.md),
- compiling and testing the split: [unity-verification.md](unity-verification.md).

## New Package Files

- `packages/<name>/` takes the package ID, display name, assemblies, and namespaces of the D7 table. `Runtime/` holds the feature directly, without a `Scripts/` level (D20); `Editor/` and `Tests/` exist only when the package has editor code (D9) or tests (D11).
- `package.json` takes Base's form and fields: four-space indent, no BOM, CRLF in the working tree (Git stores LF), Base's `unity`, `license`, `author`, and `changelogUrl`, a `documentationUrl` at the package's own README on `main`, and `version` `0.1.0`, which stays until the first release.
- Its `dependencies` name the Unity-registry SDK the code uses, at the version the `full` config installs, besides what «Assemblies And Optional Integrations» in package-architecture.md requires. They name a Mu3Library package only when the code references one of its assemblies: a Git URL install does not resolve that dependency, so the README then tells the user to install it first.
- `README.md`, `CHANGELOG.md` (the header of the other package changelogs and an empty `## [Unreleased]`), and `LICENSE.md` (a copy of Base's) are UTF-8 with BOM and LF.
- Each new file at the package root has a `.meta` in the form of URP's, `PackageManifestImporter` for `package.json` and `TextScriptImporter` for the Markdown files, with a new GUID of 32 lowercase hex characters that `git grep` finds nowhere else.

## Moving The Code

- `git mv` the feature folder with every `.meta` file. When the folder becomes `Runtime/` itself, its `.meta` becomes `Runtime.meta` and keeps its GUID. The `.asmdef` keeps its GUID, its name, and its namespace (D5), so code and assembly references by name or GUID keep working.
- Drop the SDK's gate from the moved code: its `defineConstraints` entry, its `versionDefines` entry, and each `#if` on its symbol (D21). Keep the UniTask and Steamworks.NET gates and the `includePlatforms`.
- Remove from Base what only the feature used, and the symbol's row from the gate table in package-architecture.md.

## Configs, Tools, And Profile

- Name the package as `"checkout"` in the manifest of each config that installs its SDK, `full` for every split (D12), and under `testables` only when it has tests. Check with `compile-unity.sh changed --dry-run` that a file changed under the package folder alone selects those configs.
- Update the configs' package sets in `tools/cli/tests/test_unity_projects.py`, the package count and list in `tools/cli/tests/test_repository.py`, and the config table and selection examples in [tools/README.md](../../../tools/README.md#unity-project-configs).
- In the profile, add a `[[release.packages]]` entry with the tag pattern `<name>/v{version}`, name the package in `project.summary`, and bind the moved paths: code the split left without a gate goes to its runtime or tools owner, and gated split files stay with `optional-integration-engineer` through its globs.
- Add the package to «Package Model» and «Commit Scopes» in [overview.md](overview.md), the compile selection in unity-verification.md, the release table in package-release.md, and the package names in the [glossary](glossary.md); its area label follows «Commit Scopes» in overview.md.

## Docs Until The First Release

- Move the feature's documentation from Base's README and the root README into the package README, and leave a pointer to it in each.
- Under Unreleased in the root CHANGELOG and its copies: a Base entry saying that the feature moved, that a project using it installs the new package from its first release, and that the assembly name, namespace, and GUIDs stay; and an entry for the new package. The package-local CHANGELOG gets its first version entry at the release.
- List the package in the root README and its copies, in the local-disk install list and the package list at the end, and say that it ships with the next release: no install URL names a tag that does not exist yet. «Install URLs» in package-release.md covers adding the URL.
- Add a dated entry to `docs/repository/CHANGELOG.md` for the config change.
