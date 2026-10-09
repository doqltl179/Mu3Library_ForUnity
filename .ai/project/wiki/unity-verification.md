---
owns: "How package changes are verified in this repository: the Unity compile entry point and its targets, what each kind of package change must show, where the tests live, and what the report states"
volatility: evolving
reviewed: 2026-10-09
---

# Unity Verification

The kit's [verification.md](../../kit/core/wiki/workflows/verification.md) owns what verification is and how evidence is reported; this page records this repository's commands and the Unity-specific checks.

## When

- verifying a change to a package, a development project, or the compile tooling.

## Route Away When

- package rules being verified: [package-architecture.md](rules/package-architecture.md),
- changing the compile tooling itself: [tools/README.md](../../../tools/README.md).

## Compile

- `commands.build` compiles with the Unity Editor CLI only the development projects whose packages changed, from staged, unstaged, and untracked files. Package Markdown, licenses, `Documentation~`, and repository tooling select no target.
- Explicit targets: `bash ./compile-unity.sh built-in|urp|watermelon|all`. Add `--base origin/develop` to include committed changes and `--dry-run` to print the selection without Unity. Targets map to projects in `unity-cli-packages.tsv`.
- `mu3-cli unity compile` wraps the same script; `mu3-cli unity doctor` diagnoses Editor installs, project locks, and log readiness.
- The script compiles in place when the project is closed and in a temporary source-only mirror when an Editor holds the lock, so an open Editor, the packages, and the repository `Library/` stay untouched.
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
- No repository command runs them, so `commands.test` is empty. When a request needs them, run the Unity Test Framework CLI from the `unity` stack pack against the development project that includes the package, and otherwise report that they did not run.

## Report

- The compile targets run, Unity's exit status, and the error and warning counts from its log.
- Compile runs take minutes per target, so report the targets actually run rather than all targets.
