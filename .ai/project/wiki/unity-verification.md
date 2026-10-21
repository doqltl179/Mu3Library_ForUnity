---
owns: "How package changes are verified in this repository: the Unity compile entry point and its targets, what each kind of package change must show, where the tests live, and what the report states"
volatility: evolving
reviewed: 2026-10-21
---

# Unity Verification

The kit's [verification.md](../../kit/core/wiki/workflows/verification.md) owns what verification is and how evidence is reported; this page records this repository's commands and the Unity-specific checks.

## When

- verifying a change to a package, a development project, or the compile tooling.

## Route Away When

- package rules being verified: [package-architecture.md](rules/package-architecture.md),
- changing the compile tooling itself: [tools/README.md](../../../tools/README.md).

## Compile

- `commands.build` compiles with the Unity Editor CLI the development projects that consume a changed package, from staged, unstaged, and untracked files: a Base change selects `built-in`, `urp`, and `watermelon`, a URP change `urp` and `watermelon`, a Watermelon Game change `watermelon`. Package Markdown, licenses, `Documentation~`, and repository tooling select no target.
- Explicit targets: `bash ./compile-unity.sh built-in|urp|watermelon|all`. Add `--base origin/develop` to include committed changes and `--dry-run` to print the selection without Unity. Targets map to projects in `unity-cli-packages.tsv`.
- `mu3-cli unity compile` wraps the same script; `mu3-cli unity doctor` diagnoses bash, Editor installs, sample links, project locks, and log readiness.
- The script compiles in place when the project is closed and in a temporary source-only mirror when an Editor holds the lock, so an open Editor, the packages, and the repository `Library/` stay untouched. A `Temp/UnityLockfile` that no process holds, left by a crashed Editor, is reported as stale and does not count as open.
- A development project compiles its package's samples only through a Git-ignored link from `Assets/` to `Samples~`. The script stops before Unity when a selected project lacks it; `commands.worktree_setup` creates the links in a fresh clone or worktree. `--without-samples` runs without them and says so; a report must then state that the samples were not compiled.
- Never substitute `dotnet build` on the generated `.csproj` files: Unity compiles from the `.asmdef` files and the package sources, and the generated projects drift from both.
- On Windows the script runs through Git Bash.

## By Change Type

| Change | Also show |
|---|---|
| Runtime code | The affected assemblies compile; null safety and define guards on the changed paths |
| Editor code | The editor assembly compiles; no runtime assembly depends on editor code |
| DI or core lifecycle | Initialization and injection order unchanged for the samples that depend on it |
| Optional integration | The code sits entirely inside its define symbol, and every development project that includes the file compiles, since their define sets differ |
| Compile-only request | Compile only; do not add or run tests |

## Tests

- EditMode tests live in `Mu3Library_Base/Tests/Editor` (`Mu3Library.Tests.Editor.asmdef`) and `Mu3Library_Game_WatermelonGame/Tests/Editor`. Add new ones next to the nearest existing pattern.
- `commands.test` runs them through the Unity Test Framework CLI for the projects that consume a changed package, selected as `commands.build` selects them. Explicit targets: `bash ./test-unity.sh built-in|urp|watermelon|all`, with the compile script's options; `mu3-cli unity test` wraps it.
- A project runs only the packages its `Packages/manifest.json` lists under `testables`: `built-in` runs the Base tests, `watermelon` the Base and Watermelon Game tests, and `urp` lists none and is skipped. Base tests therefore run under two define sets.
- The command exits non-zero when a test fails, Unity fails, or a run writes no result. Such a failure does not stop the remaining targets; a target that cannot start, such as an open project under `--in-place`, stops the command at once. NUnit XML lands in `log/unity-tests/<target>-editmode.xml`. A project open in an Editor is tested in an isolated mirror, as in compile.
- Tests take minutes per target on a first import; when a request does not need them, report that they did not run.

## Report

- The compile targets run, Unity's exit status, and the error and warning counts from its log.
- For tests, the targets run and each target's total, passed, failed, and skipped counts from the summary.
- Compile and test runs take minutes per target, so report the targets actually run rather than all targets.
