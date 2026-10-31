---
owns: "How package changes are verified in this repository: the Unity compile entry point and its targets, what each kind of package change must show, where the tests live, and what the report states"
volatility: evolving
reviewed: 2026-10-31
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
- Explicit targets: `bash ./compile-unity.sh built-in|urp|watermelon|all`; targets map to projects in `unity-cli-packages.tsv`. `bash ./compile-unity.sh changed --base origin/develop` adds the changes committed since that ref to the selection; `--base` works only with `changed`, and the script refuses it with an explicit target. `--dry-run` prints the selection and where each target would run, without Unity.
- `mu3-cli unity compile` wraps the same script; `mu3-cli unity doctor` diagnoses bash, Editor installs, sample links, project locks, and log readiness.
- The script compiles in place when the project is closed and in a temporary source-only mirror when an Editor holds the lock, so an open Editor, the packages, and the repository `Library/` stay untouched. A `Temp/UnityLockfile` that no process holds, left by a crashed Editor, is reported as stale and does not count as open.
- A development project compiles its package's samples only through a Git-ignored link from `Assets/` to `Samples~`. The script stops before Unity when a selected project lacks it; `commands.worktree_setup` creates the links in a fresh clone or worktree. `--without-samples` runs without them and says so; a report must then state that the samples were not compiled.
- Never substitute `dotnet build` on the generated `.csproj` files: Unity compiles from the `.asmdef` files and the package sources, and the generated projects drift from both.
- On Windows the script runs through Git Bash.
- Open one development project in an Editor at a time. The three projects share the packages through `file:` references, so a newer Editor that rewrites a shared `.meta` while it imports breaks another project's import of the same file with `Import Error Code:(4)`.

## Lowest Supported Editor

- The packages declare their floor in the `unity` field of `package.json` (`6000.3`), and no development project runs that Editor. `--editor-version <version>` on either script runs the targets with that installed Editor in an isolated mirror retargeted to it, with the Unity package versions it bundles; [tools/README.md](../../../tools/README.md#lowest-supported-editor) describes the retargeting.
- Never open a development project in an older Editor in place: the downgrade rewrites its tracked settings. The scripts refuse `--in-place` with `--editor-version`.

## By Change Type

| Change | Also show |
|---|---|
| Runtime code | The affected assemblies compile; null safety and define guards on the changed paths |
| Editor code | The editor assembly compiles; no runtime assembly depends on editor code |
| DI or core lifecycle | Initialization and injection order unchanged for the samples that depend on it; `commands.test_playmode` passes, `urp` included, since only that target runs the coroutine path |
| Optional integration | The code sits entirely inside its define symbol, and every development project that includes the file compiles, since their define sets differ |
| Editor-version branch, version-sensitive API, package dependency, or the `unity` floor | The affected targets also compile with `--editor-version` set to an installed release of the floor |
| Compile-only request | Compile only; do not add or run tests |

- Base's samples compile only in `UnityProject_BuiltIn`, which resolves every optional package, so an unguarded optional-package reference in a Base sample compiles there. Prove a sample's guards by compiling a temporary mirror of that project with the optional packages removed from its manifest, and read the assembly's defines from its `Library/Bee` response file. A reading-only audit of the Template sample missed six unguarded files.

## Tests

- EditMode tests live in `Mu3Library_Base/Tests/Editor` (`Mu3Library.Tests.Editor.asmdef`), `Mu3Library_URP/Tests/Editor` (`Mu3Library.URP.Tests.Editor.asmdef`), and `Mu3Library_Game_WatermelonGame/Tests/Editor`. Add new ones next to the nearest existing pattern.
- PlayMode tests live in `Mu3Library_Base/Tests/Runtime` (`Mu3Library.Tests.Runtime.asmdef`) and cover what only the player loop shows: a core's preparation when its component or object is turned off or destroyed, the `CoreRoot` accessor once the application quits, and the Multiplayer Debugger's lookup of a manager registered under `CoreRoot` (`MultiplayerDebuggerLookupTests`). A test belongs there only when edit mode cannot observe the behavior. The assembly also holds the components that EditMode tests save into a prefab, such as `ObservablePrefabTestComponent`, since a prefab keeps a component only through its own script and `AddComponent` refuses a script from an editor assembly; `Mu3Library.Tests.Editor` references it for them.
- `MultiplayerSteamIntegrationTests` run the Steam backend against the Steam client on the machine, on Steam's test app 480, only when the environment variable `MU3LIBRARY_STEAM_TESTS=1` reaches Unity (for example `MU3LIBRARY_STEAM_TESTS=1 bash ./test-unity.sh built-in`) and a signed-in Steam client runs; otherwise they report as ignored. Two players on two accounts remain a manual check (section 13 of `docs/specs/steam-player-hosted-multiplayer.md`).
- `MultiplayerLocalMachineTests` run the machine-wide local network over real loopback sockets and a temporary folder, so they wait in real time, about 15 seconds per run. The test that proves the listener refuses every address but loopback reports as ignored on a machine with no other network address. Separate processes are not part of the run: the pull request that adds such behavior records how it was checked across processes.
- `MultiplayerPerformanceTests` measure NFR-1 and NFR-2 under the full 16-player load on both local networks; they are `[Explicit]`, so `commands.test` reports them as skipped. Run them by name through the Unity CLI, since `test-unity.sh` has no test filter, with no Editor open on the project: `<Unity> -batchmode -projectPath <repo>/UnityProject_BuiltIn -runTests -testPlatform EditMode -testFilter Mu3Library.Tests.MultiplayerPerformanceTests -testResults <repo>/log/perf/results.xml -logFile <repo>/log/perf/run.log`. A run takes about two minutes, asserts only that the steady state allocates nothing and that every message arrives by the next frame, and logs the host's timing in lines that start with `[NFR-1]`.
- `commands.test` runs the EditMode tests through the Unity Test Framework CLI for the projects that consume a changed package, selected as `commands.build` selects them, and `commands.test_playmode` runs the PlayMode tests for the same selection. Explicit targets: `bash ./test-unity.sh built-in|urp|watermelon|all`, with the compile script's options and `--platform editmode|playmode`; `mu3-cli unity test` wraps it.
- A project runs only the packages its `Packages/manifest.json` lists under `testables`: `built-in` runs the Base tests, `urp` the Base and URP tests, and `watermelon` the Base and Watermelon Game tests. Base tests therefore run under three define sets, and `urp` is the only one without UniTask, so only its runs reach the coroutine paths that stand in for UniTask, such as `CoreBase`'s preparation.
- The command exits non-zero when a test fails, Unity fails, or a run writes no result. Such a failure does not stop the remaining targets; a target that cannot start, such as an open project under `--in-place`, stops the command at once. NUnit XML lands in `log/unity-tests/<target>-<platform>.xml`. A project open in an Editor is tested in an isolated mirror, as in compile.
- Tests take minutes per target on a first import; when a request does not need them, report that they did not run.

## Report

- The compile targets run, the Editor version, Unity's exit status, and the error and warning counts from its log.
- For tests, the targets run and each target's total, passed, failed, and skipped counts from the summary.
- Compile and test runs take minutes per target, so report the targets actually run rather than all targets.
