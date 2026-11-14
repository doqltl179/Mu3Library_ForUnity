# Repository Tools

The Python tools in `tools/cli` automate repository work. Each tool is a package of its own under `src/` with its own command, and the code the tools share lives once, in `mu3_common`.

They are intentionally scoped to tooling-safe roots and should not modify Unity runtime or editor package surfaces unless that is explicitly requested.

## Tools

| Package | Command | Covers |
|---|---|---|
| `mu3_repo` | `mu3-repo` | Repository checks |
| `mu3_unity` | `mu3-unity` | Unity Editor diagnostics, and compiles and test runs through `compile-unity.sh` and `test-unity.sh` |
| `mu3_samples` | `mu3-samples` | The development projects' links to their packages' `Samples~` folders |
| `mu3_strip_harness` | `mu3-di-strip-harness` | The [DI stripping harness](../README.md#di-stripping-harness) |
| `mu3_fixed_load` | none | The helper that `compile-unity.sh` and `test-unity.sh` run beside each Unity run, by its path, `src/mu3_fixed_load/fixed_load.py`, with nothing but the standard library (see [../README.md](../README.md#fixed-load-and-repeated-runs)) |
| `mu3_common` | none | The code the tools share: the repository root and the refusal to change it from outside the checkout (`checkout`), the package mapping in `unity-cli-packages.tsv` and the Editor each project needs (`editor`), the bash that runs the shell entrypoints (`bash`), and the state of each samples link (`sample_links`) |

Each command also runs as `python -m <package>`, such as `python -m mu3_unity doctor`, and so from the repository root without installing the tools, with `PYTHONPATH=tools/cli/src`. A tool imports `mu3_common` and nothing of another tool, so code that two tools need goes into `mu3_common` rather than into a copy.

- `mu3-repo info`: print key repository roots and the agent kit and overlay locations.
- `mu3-repo check`: validate repository document layout, README links, stale routing references, invisible and private-use characters and the replacement character U+FFFD in package `.cs` files, and tracked tooling artifacts, then run `python .ai/kit/tools/agentkit.py check` on the agent docs. A package `.cs` file that is not UTF-8 is reported with the line where its bytes stop being UTF-8. When those bytes start like an unpaired surrogate written out as bytes, `ED` and then a byte from `A0` to `BF`, the report says they may be one; otherwise it asks to save the file as UTF-8, as for Korean saved as CP949.
- `mu3-unity doctor`: diagnose bash, package mappings, sample links, Unity projects, required Editors, installed modules, project locks, and log readiness.
- `mu3-unity changes`: explain which development projects the current Git changes or a `--base` ref select, including every project that consumes a changed package.
- `mu3-unity compile`: invoke the repository's selective Unity batchmode compiler through the shared shell entrypoint; `--stall-timeout <seconds>` sets how long Unity may write nothing before the target fails (see [../README.md](../README.md#unity-compile-entrypoint)).
- `mu3-unity test`: run the EditMode tests of the selected development projects through `test-unity.sh`, with the same targets and options as `mu3-unity compile`, or with `--platform playmode` their PlayMode tests; `--filter <pattern>` runs only the tests whose full name matches it, `--stall-timeout <seconds>` sets how long Unity may write nothing before the target fails, `--load-spinners <n>` spins *n* CPU cores from the first test of each run to its last, and `--repeat <n>` runs each target *n* times and totals the runs (see [../README.md](../README.md#fixed-load-and-repeated-runs)).
- `mu3-samples link`: link each development project's `Assets/` to its package's `Samples~` folder, the setup step a fresh clone needs (see [../README.md](../README.md#fresh-clone-setup)).
- `mu3-samples unlink`: remove only those links, never the `Samples~` folders they point at, so that `git worktree remove` can delete the worktree on Windows (see [../README.md](../README.md#worktree-removal)).
- `mu3-di-strip-harness`: build the DI stripping harness as a Windows player at a chosen scripting backend and Managed Stripping Level, with the Editor the URP project names or another installed one, run it, and report each check (see [../README.md](../README.md#di-stripping-harness)).

## Repository Root

The tools act on the checkout they were imported from. An installed command, such as one of the editable install in a checkout's `.venv`, keeps acting on the checkout it was installed from even when it runs in another checkout's folder. A working directory inside that same checkout only supplies the spelling of its root, such as the `subst` drive it was run from.

- Every command of `mu3-unity` and `mu3-samples`, and `mu3-di-strip-harness`, prints `Repository root: <root>` as its first line, before it acts, so the output names the checkout it changes or runs. `mu3-unity doctor --format json` gives the root as the `repository_root` field of its JSON object instead.
- `mu3-samples link` and `mu3-samples unlink` change nothing and exit with 2 when the working directory is outside that checkout. The error names both folders, and the checkout the working directory is in when there is one. A worktree inside the checkout's folder, such as one under `.claude/worktrees/`, counts as outside it, and a `subst` drive or a link that names the checkout counts as inside it. A folder in the checkout's worktree folders, `.claude/worktrees/` and `.worktrees/`, that is no checkout of its own is refused too, and the error names that folder. A failed `git worktree remove` leaves such a folder, which has lost the `AGENTS.md` and `.github` that mark a checkout; [../README.md](../README.md#worktree-removal) says how to remove its links (#436). To change the checkout you are in, run its own tools from its root with `PYTHONPATH=tools/cli/src python -m mu3_samples`.

## Compile Verification

Compile verification runs the Unity Editor, never `dotnet build` on a generated `.csproj`. Unity builds its assemblies from `.asmdef` and the package sources, so a generated project file is not part of what ships and drifts from the code it claims to build.

```powershell
mu3-unity doctor --target built-in
mu3-unity changes --base origin/develop
mu3-unity compile
```

`mu3-unity compile` wraps `compile-unity.sh`, which compiles in place when the Unity project is closed and mirrors it into a temporary project when an Editor holds the lock. An in-place run opens the repository project itself, so Unity updates its `Library/`. A mirror run leaves the open Editor, the repository packages, and the repository `Library/` untouched.

`mu3-unity compile --editor-version <version>` and `mu3-unity test --editor-version <version>` run with another installed Editor, such as the packages' lowest supported one, always in a mirror retargeted to it; see [../README.md](../README.md#lowest-supported-editor).

- Built-In is the default development context for this repository.
- URP is an additional context layered on top of the Base package and should not replace the Built-In baseline for shared maintenance.
- Shared Base files can still have more than one valid project context because all three development projects (Built-In, URP, and Watermelon Game) include them with different define sets.

## Environment Bootstrap

Enter the repository's development environment from PowerShell. The first run creates `.venv` at the repository root, with `uv` when it is installed and the standard `venv` module otherwise, and installs the tools into it in editable mode:

```powershell
. .\tools\dev\activate.ps1
mu3-unity --help
```

From bash, `source tools/dev/activate.sh` does the same. [../README.md](../README.md#development-environment) describes what else entering it sets and how to leave it. A `.venv` made before the tools were split into one command each (#485) holds the former single command and none of the four above, since entering only installs into a new `.venv`; delete the folder and enter again.

## Verification

Run the dependency-light tool unit tests from the repository root:

```powershell
$env:PYTHONPATH = "tools/cli/src"
python -m unittest discover -s tools/cli/tests -v
python -m mu3_repo check
```

The same suite runs from Git Bash:

```bash
PYTHONPATH=tools/cli/src python -m unittest discover -s tools/cli/tests -v
```

The tests that run `compile-unity.sh` find bash the way `mu3-unity` does, so they run from PowerShell too. They run the scripts against a fake Unity with `UNITY_LOG_POLL_SECONDS=0.02` in the environment, so the stall watch looks at the fake's log every 0.02 s instead of every second, the interval a real run keeps. They are skipped, and the summary shows a skip count, on a host where `mu3-unity doctor` reports a failing `Bash` check. The tests that check who holds a lock file are also skipped on a host other than Windows without `lsof`, which the scripts and the doctor use for that check.

## Bash on Windows

`mu3-unity changes`, `mu3-unity compile`, `mu3-unity test`, and the tool tests run the shell entry points through the bash of Git for Windows. The Git for Windows installer puts `git` on `PATH` but not `bash`, so `mu3_common` looks in this order:

1. `bash` on `PATH`, unless it is the WSL launcher (`C:\Windows\System32\bash.exe` or its `WindowsApps` alias), which runs the script inside Linux where the Windows Unity Editor is out of reach.
2. `bin\bash.exe` of the Git installation whose `git` is on `PATH`.
3. `Git\bin\bash.exe` under `Program Files`, `Program Files (x86)`, and `%LOCALAPPDATA%\Programs`.

When none of them exists, the commands stop with a usage error that says what to install or add to `PATH`, and `mu3-unity doctor` fails its `Bash` check. `tools/dev/activate.ps1` asks `mu3_common` for this bash and puts it first on the PowerShell session's `PATH`.

Git Bash also rewrites an argument that starts with `/` as a Windows path before it starts a Windows program such as `python`, so `mu3-unity test --filter /x`, typed in Git Bash, passes `C:/Program Files/Git/x` on; no test matches it and the target fails with `no test matched --filter`. `test-unity.sh` itself refuses a filter that starts with `/`, and PowerShell passes it unchanged for `mu3-unity` to refuse.

## Notes

- These packages live under `tools/` so they stay outside Unity package delivery surfaces.
- Each tool keeps its commands few. Add a tool, as a package of its own with a command of its own, or a command to an existing tool, only when a workflow becomes stable and reusable.
- The tools remain tooling-safe: they can write local support artifacts under `log/`, but they do not edit shipped Unity runtime or editor package surfaces.
- Agent rules, roles, and skills come from agentkit (`.ai/kit/`) and the project overlay (`.ai/project/`); `mu3-repo check` only runs the kit's own check.

## Planned Unity Automation Surface

Keep `compile-unity.sh` and `test-unity.sh` as the dependency-light repository entrypoints while the workflow stabilizes. `mu3-unity` orchestrates them, while the shell scripts remain usable on their own.

Available now:

- `mu3-unity doctor`: bash, Editor versions, modules, project locks, package mappings, sample links, and writable log paths.
- `mu3-unity changes`: changed-file classification, owning packages, and selected projects, expanded to every project that consumes a changed package.
- `mu3-unity compile`: change-aware batchmode compilation with configured target keys, `all`, and `--base` controls.
- `mu3-unity test`: EditMode execution with the same selection, NUnit XML results under `log/unity-tests/`, and a non-zero exit on any failure.
- `mu3-samples link`: the sample links a fresh clone needs.
- `mu3-samples unlink`: those links removed again before `git worktree remove`.

```powershell
mu3-unity doctor --target built-in
mu3-unity changes --base origin/develop
mu3-unity compile --dry-run
mu3-unity test watermelon
```

Still planned:

- `mu3-unity test`: retries.
- `mu3-unity build`: named Build Profiles, platform modules, output directories, and artifact manifests.
- `mu3-unity logs`: per-run log directories, warning/error summaries, durations, and reproducible invocation metadata.
- `mu3-unity cache`: cache size/status inspection and opt-in cleanup; never purge a Unity `Library` implicitly.
- License and authentication state in `mu3-unity doctor`.

The current `unity-cli-packages.tsv` is intentionally minimal: target key, package root, representative Unity project, and the optional samples link. Replace it with a richer versioned TOML model only when dependencies, test matrices, or player-build profiles need structured fields. At that point, keep package ownership, dependency edges, Unity version source, test suites, and build profiles in one configuration rather than spreading them across shell conditionals and CI YAML.
