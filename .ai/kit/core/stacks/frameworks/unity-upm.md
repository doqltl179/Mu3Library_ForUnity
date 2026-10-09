---
id: unity-upm
title: Unity Package Manager packages
kind: framework
applies_to: ["**/package.json", "**/Samples~/**", "**/Documentation~/**"]
related: [unity, csharp]
volatility: volatile
reviewed: 2026-10-09
sources: ["https://docs.unity3d.com/Manual/CustomPackages.html", "https://docs.unity3d.com/Manual/cus-layout.html", "https://docs.unity3d.com/Manual/cus-naming.html", "https://docs.unity3d.com/Manual/upm-manifestPkg.html", "https://docs.unity3d.com/6000.3/Documentation/Manual/cus-samples.html", "https://docs.unity3d.com/6000.0/Documentation/Manual/cus-samples.html", "https://docs.unity3d.com/Manual/cus-asmdef.html", "https://docs.unity3d.com/Manual/assembly-definition-file-format.html", "https://docs.unity3d.com/Manual/assembly-definition-includes.html", "https://docs.unity3d.com/Manual/cus-tests.html", "https://docs.unity3d.com/Manual/upm-git.html", "https://docs.unity3d.com/Manual/upm-localpath.html", "https://docs.unity3d.com/Manual/upm-concepts.html", "https://docs.unity3d.com/Manual/SpecialFolders.html"]
---

# Unity Package Manager packages

## Detect
- A UPM package folder has a `package.json` whose `name` is reverse-domain (for example `com.example.tool`), next to some of `Runtime/`, `Editor/`, `Tests/`, `Samples~/` or `Samples/`, `Documentation~/`, and `CHANGELOG.md`. A `package.json` without that `name` shape belongs to another stack (for example Node); ignore this pack there.
- Mutability: an embedded package (`Packages/<name>/`) and a local folder referenced as `"<name>": "file:<path>"` in a project's `Packages/manifest.json` (relative paths start from `Packages/`) can be changed; registry, Git URL, and tarball installs are read-only.
- Packages outside `Assets/` and `Packages/` (for example at the repository root): their C# files match neither pack's default globs. Bind the package folder (for example `com.example.tool/**`) to both `unity` and `unity-upm` with `stack_paths` in the profile and activate both.
- Host project: Unity compiles and tests a package only inside a project. Find it through `commands.build`/`commands.test` or a `Packages/manifest.json` that references the package with `file:` and lists it in `testables`.
- Samples convention: the folder name (`Samples~` or `Samples`), the prefix of each `samples[].path`, and any `.sample.json` files under the samples folder (see «Version Notes»).
- Release tags: `git tag --list` shows whether releases are tagged per package or per repository.

## Conventions
- `package.json`: `name` (required) uses reverse-domain notation with only lowercase letters, digits, `-`, `_`, and `.`; never start it with `com.unity` or put `unity` anywhere in it. `version` (required) is SemVer `major.minor.patch`. Set `unity` to the lowest supported Editor as `major.minor` (`"6000.0"` for Unity 6; optional `unityRelease` such as `"0b4"`), plus `displayName` and `description`. Optional fields, when present, must hold valid values.
- `dependencies` maps package names to exact SemVer versions: no ranges, and no Git URLs (those are allowed only in a project's `Packages/manifest.json`).
- One asmdef per code folder: `Runtime/<org>.<package>.asmdef`, `Editor/<org>.<package>.Editor.asmdef` (Editor platform only, references Runtime), `Tests/Runtime/<org>.<package>.Tests.asmdef`, `Tests/Editor/<org>.<package>.Editor.Tests.asmdef`. The Editor ignores package scripts outside an asmdef; Runtime never references Editor.
- Optional dependency on another package: leave it out of `dependencies`. In the asmdef that uses it, add a `versionDefines` entry `{ "name": "<package>", "expression": "<version or range>", "define": "<SYMBOL>" }`, then guard the code with `#if <SYMBOL>`, or move it to its own asmdef that references the package and lists `<SYMBOL>` in `defineConstraints`, so it compiles only when the package is installed. The symbol is visible only inside that assembly.
- `versionDefines` expressions: `[1.3,3.4.1]` inclusive, `(1.3.0,3.4)` exclusive, `[2.4.5]` exact, a bare `2.1.0` means that version or later; no spaces or wildcards.
- `samples`: one entry per sample subfolder with `displayName`, `path`, and optional `description`; keep the path prefix the package already uses. Keep sample metadata in either the `samples` array or one `.sample.json` per sample, never both: a single `.sample.json` makes packing ignore the array.
- Keep a `CHANGELOG.md` at each package root and add an entry with every `version` bump.
- A repository holding several packages: version each package independently and tag each release with the package name and version (for example `com.example.tool-v1.2.0`), so a consumer can pin `<repo>.git?path=/<folder>#<tag>`; one repository-wide tag cannot pin packages separately.

## Verify
- Prefer `commands.build` and `commands.test`; otherwise run the `unity` pack's batch compile and test commands on the host project.
- Package tests run in a host project only when the package is embedded or listed in that project's `testables` (for example `"testables": ["com.example.tool"]` in `Packages/manifest.json`).
- `package.json` parses as JSON, and its `version` matches the newest `CHANGELOG.md` entry.
- `git ls-files <package folder>` lists a `.meta` beside every file and folder outside `~` folders.
- After changing `samples`, import each changed sample into the host project from the Package Manager window and check the console for errors.

## Pitfalls
- Unity skips files and folders whose names end in `~` or start with `.` during import, so `Samples~/` and `Documentation~/` get no Unity-generated `.meta`, no compilation, and no Editor API access. Edit a sample through the host project's imported copy under `Assets/` or a link that exposes it, then copy the result back with the same file set the sample already tracks. With neither, editing its scenes or prefabs falls under the direct-YAML exception in the `unity` pack.
- Editing only the imported copy of a sample: it is a copy, not the package; the change stays in the host project until copied back.
- Missing `.meta` files in the package: GUID references need them, and registry and Git installs are read-only for consumers. Commit each package asset's `.meta` with the asset.
- Git URL order: `?path=` precedes `#revision` (`https://example.com/repo.git?path=/com.example.tool#com.example.tool-v1.2.0`); the reverse order fails. A revision is a branch, a tag, or a full commit hash; short hashes are unsupported. The project's lock file pins the resolved commit, so a branch URL stays on that commit until the dependency is requested again.
- Without the Git LFS client, the Package Manager checks out LFS pointer files and reports no error.

## Version Notes
- Samples folder: the Unity 6.3 manual has authors create `Samples/` with `samples[].path` starting `Samples`, and a Unity process that packs the package (for example Export) renames the folder to `Samples~`. The Unity 6.0 manual has authors create `Samples~/` directly, with `Samples~/<name>` paths, and the 6.3 manifest reference still shows `Samples~/` paths. Keep the convention the package already uses unless asked to migrate (as of 2026-10, per Unity Manual 6000.0 and 6000.3 "Create samples for your package" and the package manifest reference).
- The Package Manager's Create package names `Samples` and `Documentation` without the tilde; registry packages use the tilde to hide them from the Project window (as of 2026-10, per Unity Manual 6000.3 package layout).
- Unity 6 values of `unity` use the `6000.x` form, for example `"6000.0"` (as of 2026-10, per the Unity Manual package manifest reference).
- Git dependencies need a Git client 2.14.0 or later on `PATH`, plus the Git LFS client for repositories that use LFS (as of 2026-10, per Unity Manual "Introduction to Git dependencies").
