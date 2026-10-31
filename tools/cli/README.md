# Mu3 CLI

`mu3-cli` is an auxiliary Python command-line tool for repository automation.

It is intentionally scoped to tooling-safe roots and should not modify Unity runtime or editor package surfaces unless that is explicitly requested.

## Initial Command Surface

- `repo info`: print key repository roots and the agent kit and overlay locations.
- `repo check`: validate repository document layout, README links, stale routing references, and tracked tooling artifacts, then run `python .ai/kit/tools/agentkit.py check` on the agent docs.
- `unity doctor`: diagnose bash, package mappings, sample links, Unity projects, required Editors, installed modules, project locks, and log readiness.
- `unity link-samples`: link each development project's `Assets/` to its package's `Samples~` folder, the setup step a fresh clone needs (see [../README.md](../README.md#fresh-clone-setup)).
- `unity changes`: explain which development projects the current Git changes or a `--base` ref select, including every project that consumes a changed package.
- `unity compile`: invoke the repository's selective Unity batchmode compiler through the shared shell entrypoint.
- `unity test`: run the EditMode tests of the selected development projects through `test-unity.sh`, with the same targets and options as `unity compile`, or with `--platform playmode` their PlayMode tests; `--filter <pattern>` runs only the tests whose full name matches it, and `--stall-timeout <seconds>` sets how long Unity may write nothing before the target fails (see [../README.md](../README.md#unity-test-entrypoint)).

## Compile Verification

Compile verification runs the Unity Editor, never `dotnet build` on a generated `.csproj`. Unity builds its assemblies from `.asmdef` and the package sources, so a generated project file is not part of what ships and drifts from the code it claims to build.

```powershell
mu3-cli unity doctor --target built-in
mu3-cli unity changes --base origin/develop
mu3-cli unity compile
```

`unity compile` wraps `compile-unity.sh`, which compiles in place when the Unity project is closed and mirrors it into a temporary project when an Editor holds the lock. An in-place run opens the repository project itself, so Unity updates its `Library/`. A mirror run leaves the open Editor, the repository packages, and the repository `Library/` untouched.

`unity compile --editor-version <version>` and `unity test --editor-version <version>` run with another installed Editor, such as the packages' lowest supported one, always in a mirror retargeted to it; see [../README.md](../README.md#lowest-supported-editor).

- Built-In is the default development context for this repository.
- URP is an additional context layered on top of the Base package and should not replace the Built-In baseline for shared maintenance.
- Shared Base files can still have more than one valid project context because all three development projects (Built-In, URP, and Watermelon Game) include them with different define sets.

## Environment Bootstrap

Enter the repository's development environment from PowerShell. The first run creates `.venv` at the repository root, with `uv` when it is installed and the standard `venv` module otherwise, and installs the CLI into it in editable mode:

```powershell
. .\tools\dev\activate.ps1
mu3-cli --help
```

From bash, `source tools/dev/activate.sh` does the same. [../README.md](../README.md#development-environment) describes what else entering it sets and how to leave it.

## Verification

Run the dependency-light CLI unit tests from the repository root:

```powershell
$env:PYTHONPATH = "tools/cli/src"
python -m unittest discover -s tools/cli/tests -v
python -m mu3_cli repo check
```

The same suite runs from Git Bash:

```bash
PYTHONPATH=tools/cli/src python -m unittest discover -s tools/cli/tests -v
```

The tests that run `compile-unity.sh` find bash the way the `unity` commands do, so they run from PowerShell too. They are skipped, and the summary shows a skip count, on a host where `mu3-cli unity doctor` reports a failing `Bash` check. The tests that check who holds a lock file are also skipped on a host other than Windows without `lsof`, which the scripts and the doctor use for that check.

## Bash on Windows

`unity changes`, `unity compile`, `unity test`, and the CLI tests run the shell entry points through the bash of Git for Windows. The Git for Windows installer puts `git` on `PATH` but not `bash`, so the CLI looks in this order:

1. `bash` on `PATH`, unless it is the WSL launcher (`C:\Windows\System32\bash.exe` or its `WindowsApps` alias), which runs the script inside Linux where the Windows Unity Editor is out of reach.
2. `bin\bash.exe` of the Git installation whose `git` is on `PATH`.
3. `Git\bin\bash.exe` under `Program Files`, `Program Files (x86)`, and `%LOCALAPPDATA%\Programs`.

When none of them exists, the commands stop with a usage error that says what to install or add to `PATH`, and `unity doctor` fails its `Bash` check. `tools/dev/activate.ps1` asks the CLI for this bash and puts it first on the PowerShell session's `PATH`.

## Notes

- This package lives under `tools/` so it stays outside Unity package delivery surfaces.
- The command tree is intentionally small. Add new groups only when a workflow becomes stable and reusable.
- The CLI remains tooling-safe: it can write local support artifacts under `log/`, but it does not edit shipped Unity runtime or editor package surfaces.
- Agent rules, roles, and skills come from agentkit (`.ai/kit/`) and the project overlay (`.ai/project/`); this CLI only runs the kit's own check.

## Planned Unity Automation Surface

Keep `compile-unity.sh` and `test-unity.sh` as the dependency-light repository entrypoints while the workflow stabilizes. The `mu3-cli unity` group orchestrates them, while the shell scripts remain usable on their own.

Available now:

- `unity doctor`: bash, Editor versions, modules, project locks, package mappings, sample links, and writable log paths.
- `unity changes`: changed-file classification, owning packages, and selected projects, expanded to every project that consumes a changed package.
- `unity compile`: change-aware batchmode compilation with configured target keys, `all`, and `--base` controls.
- `unity test`: EditMode execution with the same selection, NUnit XML results under `log/unity-tests/`, and a non-zero exit on any failure.
- `unity link-samples`: the sample links a fresh clone needs.

```powershell
mu3-cli unity doctor --target built-in
mu3-cli unity changes --base origin/develop
mu3-cli unity compile --dry-run
mu3-cli unity test watermelon
```

Still planned:

- `unity test`: retries.
- `unity build`: named Build Profiles, platform modules, output directories, and artifact manifests.
- `unity logs`: per-run log directories, warning/error summaries, durations, and reproducible invocation metadata.
- `unity cache`: cache size/status inspection and opt-in cleanup; never purge a Unity `Library` implicitly.
- License and authentication state in `unity doctor`.

The current `unity-cli-packages.tsv` is intentionally minimal: target key, package root, representative Unity project, and the optional samples link. Replace it with a richer versioned TOML model only when dependencies, test matrices, or player-build profiles need structured fields. At that point, keep package ownership, dependency edges, Unity version source, test suites, and build profiles in one configuration rather than spreading them across shell conditionals and CI YAML.
