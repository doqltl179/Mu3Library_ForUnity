---
owns: "What a unit that splits a feature out of Base into a package of its own changes: the new package's files, its dependency versions, moving the code with what its assembly keeps or gets and dropping its SDK gate, the configs, tools, and profile, and the docs up to the package's first release"
volatility: evolving
reviewed: 2026-11-16
---

# Package Split

How a feature leaves Base for a package of its own under [ADR 0001](../../../docs/adr/0001-package-split-and-repository-restructure.md), as unit 7 (#533) split Notifications into `packages/notifications`. Units 8 to 12 follow it and read their own decisions in the ADR first: the D7 table (package IDs, assemblies, namespaces, and the three new assemblies), the D5 exceptions, the D12 config table, D8 and D9 (di and the editor tools), D11 (tests), and D19 and D22 (steam).

## When

- moving a feature of Base into a package of its own.

## Route Away When

- surfaces, assembly direction, gates, and the `package.json` dependency rules: [package-architecture.md](rules/package-architecture.md),
- the first release, its tag, its install URL, and its dependency versions: [package-release.md](package-release.md),
- compiling and testing the split: [unity-verification.md](unity-verification.md),
- encoding and line endings of the new files: «File Encoding» in [overview.md](overview.md).

## New Package Files

- `packages/<name>/` takes the package ID, display name, assemblies, and namespaces of the D7 table. `Runtime/` holds the feature directly, without a `Scripts/` level (D20); `Editor/` and `Tests/` exist only when the package has editor code (D9) or tests (D11).
- `package.json` takes Base's form and fields: four-space indent and no BOM (Git stores LF; the working tree follows `core.autocrlf`), Base's `unity`, `license`, `author`, and `changelogUrl`, a `documentationUrl` at the package's own README on `main`, and `version` `0.1.0`, which stays until the first release.
- `README.md`, `CHANGELOG.md` (the header of the other package changelogs and an empty `## [Unreleased]`), and `LICENSE.md` (a copy of Base's) follow «File Encoding» in overview.md.
- Each new file at the package root has a `.meta` in the form of URP's, `PackageManifestImporter` for `package.json` and `TextScriptImporter` for the Markdown files, with a new GUID of 32 lowercase hex characters that `git grep` finds nowhere else.

## Dependencies

- Declare the Unity-registry SDK the code uses at the version the `unity` floor bundles, per «Assemblies And Optional Integrations» in package-architecture.md, never at the newer version a config pins. Read it from the retarget lines that `compile-unity.sh <config> --editor-version <installed 6000.3 release>` prints, `<package> <config version> -> <floor version>`; for an SDK the config brings in only through another package, such as Addressables through Localization, read it from the package list that the same run's Unity log prints after `[Package Manager] Registered`. Name the 6000.3 release it came from in the commit body: Mobile Notifications 2.4.3 came from 6000.3.10f1.
- A new dependency makes that lower-bound compile required («Lowest Supported Editor» in [tools/README.md](../../../tools/README.md#lowest-supported-editor)), besides the compile on the configs' own Editor. After it, check in the generated project's `Packages/packages-lock.json` or in its log that the declared version is the one that resolved: on 2026-11-15 a project that pinned Mobile Notifications 2.4.3 directly still resolved 2.5.0 while the package declared 2.5.0.
- Name a Mu3Library package only when the code references one of its assemblies: a Git URL install does not resolve that dependency, so the README then tells the user to install it first. A split package that references Base names the Base release that removes the moved code, the next Base version after 0.28.0 (0.29.0, the minor bump that a breaking change gets before 1.0.0 in the kit's release.md), never 0.28.0 or earlier, which still holds the moved code, so the same types would sit in two assemblies. While that Base is unreleased, name the planned version; the first release confirms it.

## Moving The Code

- `git mv` the feature with every `.meta` file; a feature folder that becomes `Runtime/` itself takes its `.meta` along as `Runtime.meta`, with its GUID. What the assembly keeps follows D5 and D7:
  - notifications and di: the `.asmdef` moves with its GUID, assembly name, and namespace, so code and assembly references by name or GUID keep working;
  - steam: the `.asmdef` keeps its GUID, but its assembly and namespace become `Mu3Library.Steam.Multiplayer` (D19), so a reference by name and each `using` change;
  - addressables, localization, and inputsystem: `Addressable/`, `Localization/`, and `IS/` have no `.asmdef` today and compile into `Mu3Library`, so each package gets a new assembly (`Mu3Library.Addressables`, `Mu3Library.Localization`, `Mu3Library.InputSystem`) with a new GUID, and the namespaces (`Mu3Library.Addressable`, `Mu3Library.Localization`, `Mu3Library.IS`) stay; an assembly definition that references `Mu3Library` for them adds the new one.
- Drop the SDK's gate from the moved code: its `defineConstraints` entry, its `versionDefines` entry, and each `#if` on its symbol (D21). Keep the UniTask and Steamworks.NET gates, which guard Git-distributed SDKs, and the `includePlatforms`.
- Remove from Base what only the feature used. Delete the symbol's row from the gate table in package-architecture.md only once no package uses the symbol: `MU3LIBRARY_ADDRESSABLES_SUPPORT` stays for Base's `Scene/*SceneLoader.Addressables*.cs`, and `MU3LIBRARY_INPUTSYSTEM_SUPPORT` for Base's `UI/MVP/MVPManager.cs` and Watermelon Game's `Helpers/InputHandler*.cs`, the extensions that D1 keeps.

## Configs, Tools, And Profile

- Name the package as `"checkout"` in the manifest of each config that the D12 table lists for it, which is not every config that installs its SDK: `urp` and `watermelon` install the Input System without the inputsystem package, and di also goes into `minimal`. List it under `testables` only when it has tests. Check with `compile-unity.sh changed --dry-run` that a file changed under the package folder alone selects those configs.
- Update the configs' package sets in `tools/cli/tests/test_unity_projects.py`, the package count and list in `tools/cli/tests/test_repository.py`, and the config table and selection examples in [tools/README.md](../../../tools/README.md#unity-project-configs).
- In the profile, add a `[[release.packages]]` entry with the tag pattern `<name>/v{version}`, name the package in `project.summary`, and bind the moved paths: code the split left without a gate goes to its runtime or tools owner, and gated split files stay with `optional-integration-engineer` through its globs.
- Add the package to «Package Model» and «Commit Scopes» in overview.md, the compile selection in unity-verification.md, the release table in package-release.md, and the package names in the [glossary](glossary.md); its area label follows «Commit Scopes» in overview.md.

## Docs Until The First Release

- Move the feature's documentation from Base's README and the root README into the package README, and leave a pointer to it in each.
- Under Unreleased in the root CHANGELOG and its copies: a Base entry saying that the feature moved, that a project using it installs the new package from its first release, and what D5 says stays or changes for that feature, such as an assembly reference to add (D7) or a renamed assembly and namespace (D19); and an entry for the new package. The package-local CHANGELOG gets its first version entry at the release.
- List the package in the root README and its copies, in the local-disk install list and the package list at the end, and say that it ships with the next release: no install URL names a tag that does not exist yet. «Install URLs» in package-release.md covers adding the URL.
- Add a dated entry to `docs/repository/CHANGELOG.md` for the config change.
