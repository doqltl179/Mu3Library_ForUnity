# Tools

Repository-local support tooling lives here. These tools can help agents inspect or verify the repository, but they do not own Unity package behavior.

| Path | Owns |
|---|---|
| [cli/](cli/README.md) | Python CLI (`mu3-cli`) with the `repo` command group for repository checks and the `unity` group for Unity automation |
| [dev/](dev/) | The scripts that enter the [development environment](#development-environment), which the root `.envrc` mirrors for direnv, and `env.local.example`, the template for personal variables |
| [../compile-unity.sh](../compile-unity.sh) | Git-change-aware Unity batchmode compilation configured by `unity-cli-packages.tsv` |
| [../test-unity.sh](../test-unity.sh) | EditMode and PlayMode test runs through the Unity Test Framework, built on `compile-unity.sh`'s selection, Editor lookup, and mirror |

Generated files under `bin/`, `obj/`, `.venv/`, and `*.egg-info/` are local artifacts and should not be tracked.

Run `mu3-cli repo check` before sharing repository-level documentation or agent-framework changes. It replaces the former remote repository-hygiene workflow with its useful checks at the local tooling boundary.

## Fresh Clone Setup

A new clone or worktree holds only tracked files. Set it up in this order before the first Unity run:

1. Install the Unity Editor version that each `UnityProject_*/ProjectSettings/ProjectVersion.txt` names through Unity Hub, and on Windows Git for Windows, whose bash runs the shell entrypoint.
2. Install Python 3.10 or later, then enter the [development environment](#development-environment), which installs the CLI and its one dependency, `typer`, into `.venv` at the repository root on first use. `python -m pip install -e tools/cli` installs both without it. Every command below also runs without installing the CLI, from the repository root with `PYTHONPATH=tools/cli/src python -m mu3_cli` in place of `mu3-cli`.
3. Link the package samples into the development projects. This is the project's `commands.worktree_setup`:

   ```bash
   PYTHONPATH=tools/cli/src python -m mu3_cli unity link-samples
   ```

   Each development project sees its package's samples only through a Git-ignored link from `Assets/` to the package's `Samples~` folder, which Unity skips on its own; the fourth column of `unity-cli-packages.tsv` names the link. The command creates a junction on Windows and a relative symbolic link elsewhere, leaves a correct link alone, replaces a link that points elsewhere, and stops without touching a real folder of the same name. `compile-unity.sh` refuses to run a project whose link is missing unless `--without-samples` is given, so a compile without the samples never passes silently.
4. Check the environment; every line should pass:

   ```bash
   PYTHONPATH=tools/cli/src python -m mu3_cli unity doctor
   ```

5. Compile and test: `./compile-unity.sh all`, then `./test-unity.sh all` and `./test-unity.sh all --platform playmode`, restoring the settings file the PlayMode run re-saves (see [Unity Test Entrypoint](#unity-test-entrypoint)). The first run of each project imports it from scratch and takes minutes. On Windows, run Unity from a short path: a deep checkout, such as one under `.claude/worktrees/`, pushes Unity's `Library/PackageCache` past the 260-character limit, so map it to a drive letter with `subst` first and run the commands from that drive. The shell entrypoints and `mu3-cli` both open the project on the drive they run from.

## Development Environment

One command, run from the repository root, enters the repository's development environment in a terminal:

| Shell | Enter | Leave |
|---|---|---|
| PowerShell | `. .\tools\dev\activate.ps1` | `deactivate` |
| bash, including Git Bash | `source tools/dev/activate.sh` | `deactivate` |
| direnv | `direnv allow` once, then enter the repository folder | leave the folder |

Entering it:

- creates `.venv` at the repository root on first use, with `uv` when it is installed and `python -m venv` otherwise, and installs `mu3-cli` into it in editable mode;
- activates that virtual environment, so its `python` and `mu3-cli` come first on `PATH`;
- sets every variable that `.env.local` at the repository root lists, such as those in [Environment](#environment), and `MU3_DEV_ROOT` to the repository root;
- in PowerShell on Windows, makes `bash` run the bash that `mu3-cli` uses, normally that of Git for Windows, putting its folder first on `PATH` when `bash` does not already find it, so `bash ./test-unity.sh` runs the bash the scripts are written for.

`deactivate` restores every variable and `PATH` as they were before. Entering it while another virtual environment is active leaves that one first. The `.envrc` does not create `.venv`; run one of the scripts once first. The PowerShell script needs an execution policy that runs local scripts, such as `RemoteSigned`.

`.env.local` is personal and Git-ignored; copy [dev/env.local.example](dev/env.local.example) to start one. It holds one `KEY=VALUE` per line. The three ways of entering parse it separately, so keep to the subset all of them read alike, which the example file lists. PowerShell has no `NAME=value command` form, so a variable that a bash example sets for one command belongs in `.env.local`, or in `$env:NAME = 'value'` for the current session.

## Unity Compile Entrypoint

The default command compiles the development projects selected from staged, unstaged, and untracked Git changes:

```bash
./compile-unity.sh --dry-run
./compile-unity.sh
```

A changed package selects every configured project whose `Packages/manifest.json` references it, not only the project `unity-cli-packages.tsv` pairs it with. Each project installs different optional packages, so it compiles the shared sources with a different define set: a `Mu3Library_Base` change selects `built-in`, `urp`, and `watermelon`, and a `Mu3Library_URP` change selects `urp` and `watermelon`.

Package Markdown, licenses, `Documentation~`, and repository tooling changes do not trigger Unity compilation.

A target compiles in place while its project is closed, and in a temporary source-only mirror while an Editor has it open. A project counts as open while a process holds its `Temp/UnityLockfile`. An Editor that crashed leaves that file behind with no holder; the script reports it as stale and compiles in place, and `mu3-cli unity doctor` reports it apart from an open project. PowerShell checks the holder on Windows and `lsof` does elsewhere; on a host with neither, an existing lock file counts as open. `--dry-run` prints where each selected target would run.

The script removes a mirror as soon as Unity exits, retrying for a few seconds while a Unity child process still holds its files. A mirror it still cannot remove is reported with its path, and Unity's exit status and the remaining targets carry on regardless.

Use a Git base when selecting committed changes in CI, or keep the explicit targets for manual verification:

```bash
./compile-unity.sh changed --base origin/develop --dry-run
./compile-unity.sh built-in
./compile-unity.sh urp
./compile-unity.sh watermelon
./compile-unity.sh all
```

Add future package-to-project mappings to `unity-cli-packages.tsv`; the shell entrypoint should not need a new selection branch for each package.

## Unity Test Entrypoint

`test-unity.sh` runs the EditMode tests, or with `--platform playmode` the PlayMode tests, through the Unity Test Framework CLI and exits non-zero when a test fails, Unity fails, or a run reports no result:

```bash
./test-unity.sh --dry-run
./test-unity.sh            # projects selected by Git changes, as compile-unity.sh selects them
./test-unity.sh built-in   # Base tests with the Built-In project's define set
./test-unity.sh urp        # Base and URP tests with the URP project's define set, which lacks UniTask
./test-unity.sh watermelon # Base and Watermelon Game tests with that project's define set
./test-unity.sh all
./test-unity.sh changed --platform playmode
./test-unity.sh urp --platform playmode
./test-unity.sh built-in --filter Mu3Library.Tests.MultiplayerPerformanceTests
```

It takes the same targets and options as `compile-unity.sh` and sources it for target selection, the Editor lookup, the lock check, the sample-link check, and the isolated mirror, so a project an Editor has open is tested in a mirror and `--in-place` refuses it. A project runs only the tests of the packages its `Packages/manifest.json` lists under `testables`: `built-in` runs the Base tests, `urp` the Base and URP tests, and `watermelon` the Base and Watermelon Game tests; a project that lists none is reported as skipped. `urp` is the only project without UniTask, so it is where the Base tests reach the code paths that replace UniTask with coroutines; `--platform playmode` runs the tests that need the player loop, among them a core's coroutine preparation. A failed test or Unity run does not stop the remaining targets, and a summary closes the run; a target that cannot start, such as an open project under `--in-place` or a mirror that cannot be copied, stops the script at once. Each run writes NUnit XML to `log/unity-tests/<target>-<platform>.xml`, such as `urp-playmode.xml`, which is Git-ignored and removed before the run so an old result never stands in for a new one.

`--filter <pattern>` runs only the tests whose full name matches the pattern, which the script passes to the Unity Test Framework as `-testFilter`: a regular expression, such as the full name of a test, a fixture, or a namespace, or several separated by `;`. It also runs the `[Explicit]` tests it matches, which a run without it reports as skipped; the benchmark `Mu3Library.Tests.MultiplayerPerformanceTests` runs this way, as in the last example above. A filtered run takes the same lock check and mirror as a full one and writes the same result file, and a target where no test matches the pattern fails. A pattern may not start with `-`, which would read as an option, or `/`, which Git Bash rewrites as a file path; the script keeps Git Bash from rewriting the rest of it.

Beside each result file the script keeps Unity's log, such as `log/unity-tests/urp-editmode.log`, while it prints the log as Unity writes it. It starts Unity with `-automated`, which makes the Unity Test Framework log a `##utp:` line as each test begins and ends. When the log has not grown for `--stall-timeout <seconds>`, 600 by default, the script stops Unity and its child processes and fails the target with the name of the test that began and did not end; `--stall-timeout 0` waits as long as Unity runs. A run that ends without results names that test too. The NUnit `[Timeout]` attribute cannot take this role: the Unity Test Framework checks it only after a test method returns, so a test that never returns runs past it.

## Lowest Supported Editor

Each package declares the lowest Editor it supports in the `unity` field of its `package.json`, `6000.3` for all three, while the development projects run a newer Editor. `--editor-version <version>` compiles or tests the selected targets with another Editor installed through Unity Hub, such as a 6000.3 release, without letting it open a repository project:

```bash
./compile-unity.sh all --editor-version 6000.3.17f1
./test-unity.sh all --editor-version 6000.3.17f1
```

Every target then runs in an isolated mirror, and `--in-place` is refused. Before Unity starts, the script retargets the mirror and prints each version it changed:

- `ProjectSettings/ProjectVersion.txt` names the requested version.
- `Packages/packages-lock.json` is removed, so Unity resolves the packages again.
- Each `com.unity.*` dependency that `Packages/manifest.json` pins to a version takes the version that Editor bundles: the one its own package manifest recommends, or else the version of its built-in copy. A built-in module the Editor does not ship, such as `com.unity.modules.tetgen` on 6000.3, is dropped. A package it does not bundle keeps its version, with a warning.

The check therefore compiles against the Unity packages a project on that Editor gets by default. Run it when a change touches code behind a `UNITY_6000_x_OR_NEWER` branch or an API that differs between Editor versions, changes a package's dependencies, or moves the declared floor. Each mirror imports from scratch, so a run takes minutes per target. `UNITY_EDITOR` still overrides the executable, with a warning when it is not the requested version.

## Environment

The shell entrypoints and `mu3-cli unity doctor` read the same variables, so the Editor the doctor accepts is the one the script runs. Listed in `.env.local`, they are set in every terminal that enters the [development environment](#development-environment).

| Variable | Effect |
|---|---|
| `UNITY_EDITOR` or `UNITY` | Path to a Unity executable (or `Unity.app`) used for every target instead of the version in `ProjectVersion.txt` or the one `--editor-version` names. |
| `UNITY_HUB_EDITOR_ROOT` | Folder that holds one folder per installed Editor version, for a Unity Hub set to install Editors outside its default location (`C:\Program Files\Unity\Hub\Editor`, `/Applications/Unity/Hub/Editor`, or `~/Unity/Hub/Editor`). |
| `TMPDIR` | Where the script creates isolated mirrors. Keep it short on Windows, where a long path pushes Unity's package cache past the path limit. |

On Windows the script runs in the bash of Git for Windows. `mu3-cli` finds it even when only `git` is on `PATH`; see [cli/README.md](cli/README.md#bash-on-windows).
