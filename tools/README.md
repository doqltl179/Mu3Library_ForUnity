# Tools

Repository-local support tooling lives here. These tools can help agents inspect or verify the repository, but they do not own Unity package behavior.

| Path | Owns |
|---|---|
| [cli/](cli/README.md) | Python CLI (`mu3-cli`) with the `repo` command group for repository checks and the `unity` group for Unity automation |
| [di-strip-harness/](di-strip-harness/) | The assets of the [DI stripping harness](#di-stripping-harness), which `mu3-cli unity di-strip-harness` copies into a mirror project, builds as a player, and runs |
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

   Unity's asset import cannot read a file whose path has 260 characters or more, even with `LongPathsEnabled=1`, and the run still passes. A first import of `built-in`, `urp`, and `watermelon` from `.claude/worktrees/<session>/.worktrees/<unit>` on 2026-11-08, with Unity 6000.6.7f1, could not read 49, 64, and 190 package files whose paths had 260 to 317 characters, among them `.uxml`, `.uss`, `.device`, shader, and template files and, in `urp` and `watermelon`, `Unity.Collections.LowLevel.ILSupport.dll`, which the API updater reads; 32, 38, and 124 asset imports failed on them. No C# file was among them, and the compile and the EditMode tests passed.

   On Windows both shell entrypoints therefore warn twice. Before Unity starts, they warn when the folder Unity opens as the project, in place or in a mirror, has 98 characters or more: the longest path below a project folder that Unity could not read, `Library\PackageCache\com.unity.render-pipelines.core@13460d8ea9e5\Editor-PrivateShared\Tools\Converter\Window\RenderPipelineConverterVisualElementListFilter.uxml`, has 161 characters, so it reaches 260 there. A mirror is told to take a shorter `TMPDIR`. After Unity exits, they warn with the number of files its log says it could not find at paths of 260 characters or more, the asset imports that failed on them, and each DLL among them. `test-unity.sh` gives both warnings again under the target in its summary, and `compile-unity.sh` repeats them at the end of a run of several targets. Neither warning fails the run, since the measured failures broke no compile and no test; a plugin whose failed import does break the compile fails the target through Unity's exit status, as any compile error does. Unity does not import the failed files again: four `built-in` test runs that followed the first import in their worktree, three filtered ones in one worktree and a full EditMode run in another, imported none of them, the full run only the 110 assets its tests create, and each log named one file Unity could not read, a Terrain Tools template it reads as it loads, so the path warning before Unity starts is the one that names the cause on every run. A package with a longer path than these reaches the limit from a shorter folder; the log warning still reports its files.

   On 2026-11-08 a first `./compile-unity.sh all` from `.claude/worktrees/<session>/.worktrees/<unit>`, whose project folders had 139 to 155 characters, gave both warnings for every target with the counts above and passed. The same command from a worktree in the main checkout's `.worktrees/s382` gave no warning for `built-in` (93 characters) or `urp` (89), whose logs named no file Unity could not read. `watermelon`, whose folder there has 105 characters, was warned and could not read 3 files of `com.unity.render-pipelines.core`, of 260 to 267 characters, so 2 asset imports failed; the compile passed. A worktree under the main checkout's `.worktrees/` is therefore short enough for `built-in` and `urp` but not for `watermelon`, whose folder name alone takes 33 characters.

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

The script prints Unity's log as Unity writes it and keeps it in `log/unity-compile/<target>.log`, which is Git-ignored. When the log has not grown for `--stall-timeout <seconds>`, 300 by default, the script stops Unity and its child processes and fails the target, so a compile that waits on something batch mode never answers, such as a dialog, cannot hold the run; `--stall-timeout 0` waits as long as Unity runs. The script looks at the log every second and times a silence to the microsecond with bash 5, the bash of Git for Windows; an older bash, such as the 3.2 that macOS ships, counts only whole seconds, so there the script waits up to a second longer. Either way it never stops Unity before the log has stood still for the whole timeout. Like any failed compile, a stalled one ends the run before the remaining targets. The default is about 12 times the longest silence measured in a first import. On 2026-11-01, cold `--isolated` runs of `built-in`, `urp`, and `watermelon` and a `built-in --editor-version 6000.3.17f1` run, while up to six other Unity batch processes ran, each took 80 to 128 s; their logs stood still longest while Unity resolved the packages (up to 14 s) and compiled the scripts (up to 25 s, in the run beside six other Unity processes). A slower machine, a larger code base, or a package download can stay silent longer, which the margin leaves room for.

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

`test-unity.sh` runs the EditMode tests, or with `--platform playmode` the PlayMode tests, through the Unity Test Framework CLI and exits non-zero when a test fails or is inconclusive, Unity fails, or a run reports no result:

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
./test-unity.sh built-in --filter Mu3Library.Tests.MultiplayerPerformanceTests --load-spinners 16 --repeat 3
```

It takes the same targets and options as `compile-unity.sh` and sources it for target selection, the Editor lookup, the lock check, the sample-link check, and the isolated mirror, so a project an Editor has open is tested in a mirror and `--in-place` refuses it. A project runs only the tests of the packages its `Packages/manifest.json` lists under `testables`: `built-in` runs the Base tests, `urp` the Base and URP tests, and `watermelon` the Base and Watermelon Game tests; a project that lists none is reported as skipped. `urp` is the only project without UniTask, so it is where the Base tests reach the code paths that replace UniTask with coroutines; `--platform playmode` runs the tests that need the player loop, among them a core's coroutine preparation. A failed test or Unity run does not stop the remaining targets, and a summary closes the run; a target that cannot start, such as an open project under `--in-place` or a mirror that cannot be copied, stops the script at once. Each run writes NUnit XML to `log/unity-tests/<target>-<platform>.xml`, such as `urp-playmode.xml`, which is Git-ignored and removed before the run so an old result never stands in for a new one.

The summary gives each target the counts of the `<test-run>` element of its result file, as `<target>: <total> tests, <passed> passed, <failed> failed, <skipped> skipped, <inconclusive> inconclusive`, or `FAILED` and the reason. A test is inconclusive when an `Assume.That` in it does not hold or it calls `Assert.Inconclusive`. It reached no verdict, so it fails the target as a failed test does, and the summary names each one under its target as `inconclusive: <full name>`; the target's `error:` line is followed by the failed and the inconclusive tests. The Unity Test Framework itself exits with 2 for an inconclusive test, as for a failed one. A warning instead would let a run pass with a test that checked nothing: `Mu3Library.Tests.MultiplayerLocalMachineTests.Directory_ReadWhoseThreadIsHeldUpPastTheWait_FindsAReplacementThatFinishedInTime_AndLooksOnceAtTheOthers` gives up its verdict when its thread is held up past the read's wait, so under load its target fails and is run again with less. A test that cannot run on a machine, such as one that needs a signed-in Steam client or a network address besides loopback, calls `Assert.Ignore`, which NUnit counts as skipped. The `Assume` calls in `MultiplayerSteamTests` hold unless Steam still runs in the Unity process; Steam starts there only through a Steam backend, which stops it when disposed, so in a command-line run they fail only after an earlier test left one running.

`--filter <pattern>` runs only the tests whose full name matches the pattern, which the script passes to the Unity Test Framework as `-testFilter`: a regular expression, such as the full name of a test, a fixture, or a namespace, or several separated by `;`. It also runs the `[Explicit]` tests it matches, which a run without it reports as skipped; the benchmark `Mu3Library.Tests.MultiplayerPerformanceTests` runs this way, as in the last example above. A filtered run takes the same lock check and mirror as a full one and writes the same result file, and a target where no test matches the pattern fails. A pattern may not start with `-`, which would read as an option, or `/`, which Git Bash rewrites as a file path; the script keeps Git Bash from rewriting the rest of it.

Beside each result file the script keeps Unity's log, such as `log/unity-tests/urp-editmode.log`, while it prints the log as Unity writes it. It starts Unity with `-automated`, which makes the Unity Test Framework log a `##utp:` line as each test begins and ends. When the log has not grown for `--stall-timeout <seconds>`, 600 by default, the script stops Unity and its child processes and fails the target with the name of the test that began and did not end; `--stall-timeout 0` waits as long as Unity runs. It times the silence as a [compile](#unity-compile-entrypoint) does. A run that ends without results names that test too. The NUnit `[Timeout]` attribute cannot take this role: the Unity Test Framework checks it only after a test method returns, so a test that never returns runs past it.

### Fixed Load and Repeated Runs

`--load-spinners <n>` puts each run under a fixed load, so that two versions of a timing-sensitive test can be compared under the same one: *n* Python processes that each spin a CPU core, from the first test until that run's Unity exits, so that they do not slow the compile and the project load before it. A helper, [cli/src/mu3_cli/fixed_load.py](cli/src/mu3_cli/fixed_load.py), starts them waiting before Unity starts, reads Unity's log every 0.05 s, and lets them spin once the log holds the `##utp:` line of the first test's start. A test shorter than that look can therefore end before the load starts, and the summary then says after how many tests it started. A run under which no test ran with the load fails, since none of its timings were measured under it: the run ended before the helper saw its first test begin, or every test had ended by then. The script stops the helper as a run ends, as it fails or is interrupted too, and the helper stops the spinners; a spinner also ends by itself once the helper ends, however it ends, since its input then closes, and the helper ends once the script does, so no spinner outlives its run.

Every run, with or without the load, also counts the other Unity processes, those that do not descend from the script, such as another worktree's Editor or batch run and its import workers, since timings depend on them: once as the run starts, every 5 s, and as it ends. Under each run the summary gives the count at the start and the fewest and most while it ran, and with the load how many spinners ran on how many logical CPUs and for how long. The helper runs with `python` or `python3` of version 3.10 or later from `PATH`; without one, the script warns and leaves the processes uncounted, and refuses `--load-spinners` before Unity starts. Each run keeps what the helper recorded as `key=value` lines beside its results, such as `log/unity-tests/built-in-editmode.load`: the spinners and their process IDs, when the load started and how long it lasted, and the counts.

`--repeat <n>` runs each selected target *n* times, each run in a Unity of its own, and keeps each run's results as `<target>-<platform>-run<k>.xml`, `.log`, and `.load`, with *k* as wide as *n*, after removing those an earlier repeat left. A failed run does not stop the remaining ones. Each run has its summary entry, and the target's runs close with their totals: how many failed, the sums of the counts of the runs that wrote results, the number of runs in which each failed or inconclusive test had that result, and the fewest and most other Unity processes over all runs.

The script starts Unity anew for each run instead of repeating the tests in one Unity. In the Unity Test Framework that Unity 6000.6.7f1 bundles (1.8.0), both the `-repeat` command-line option and NUnit's `[Repeat]` attribute end a test's repeats at its first result that is not a success and report one result for them, so neither counts how many runs fail, which a timing measurement needs. A loop inside one Unity must be written into the test: #367 and #377 measured with an uncommitted `[Explicit]` loop that called `TearDown` and `SetUp` itself and logged each round. A run of its own gives every test its own verdict in its own result file, with the framework's own fixture setup and the load's start the same in every run, for any filter, a whole fixture included. Each run pays for Unity's start and the project load, so `--repeat` suits up to some tens of runs; a failure rate over hundreds of runs of a test that lasts a fraction of a second still needs a loop in the test, which the load and the counts serve as well, since the loop is one test.

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

The [DI stripping harness](#di-stripping-harness) takes the same `--editor-version` and retargets its own mirror by the same rule, so a player built with the floor Editor shows whether stripping keeps what the container needs there.

## DI Stripping Harness

The DI container reaches the constructors it calls and the `[Inject]` members it fills only through reflection, which the Unity linker cannot see, so a player built with Managed Stripping Level Medium or High keeps them only through the link.xml that Base's editor `DILinkerProcessor` writes for each build. The Editor and the EditMode and PlayMode tests never strip, so only a player shows a regression. `mu3-cli unity di-strip-harness` builds a Windows x64 player of a small mirror project, runs it, and prints one line per check:

```bash
PYTHONPATH=tools/cli/src python -m mu3_cli unity di-strip-harness --stripping High
PYTHONPATH=tools/cli/src python -m mu3_cli unity di-strip-harness --stripping Medium --backend il2cpp
PYTHONPATH=tools/cli/src python -m mu3_cli unity di-strip-harness --stripping High --with-unitask --work-dir C:/mu3-strip
PYTHONPATH=tools/cli/src python -m mu3_cli unity di-strip-harness --stripping Medium --editor-version 6000.3.17f1 --work-dir C:/mu3h
```

`--stripping` takes `Disabled`, `Minimal`, `Low`, `Medium`, or `High` (default `High`), and `--backend` takes `mono` (default) or `il2cpp`, which needs the Editor's Windows IL2CPP module and Visual Studio's C++ workload. Without `--with-unitask` cores prepare through the coroutine path, as in the URP project; with it, through UniTask. The command runs only on Windows.

`--editor-version <version>` builds with another Editor installed through Unity Hub instead of the one the URP project names, such as a 6000.3 release for the [lowest supported Editor](#lowest-supported-editor), where `DILinkerProcessor` finds the loaded assemblies through `AppDomain` instead of its `UNITY_6000_5_OR_NEWER` branch. That Editor gets its own mirror, `project-<version>/`, so each Editor keeps its own `Library/`, and the harness retargets it as `compile-unity.sh --editor-version` retargets its mirrors: `ProjectVersion.txt` names the version, each `com.unity.*` package pinned to a version takes the version that Editor bundles, a built-in module it does not ship is dropped, and any other package it does not bundle keeps its version with a warning. The command prints each change. `UNITY_EDITOR` still overrides the executable, with a warning when it is not where Unity Hub installs the requested version, since the mirror and the labels then name a version that does not build them. The Editor needs Windows Build Support (Mono), and Windows Build Support (IL2CPP) for `--backend il2cpp`.

[di-strip-harness/](di-strip-harness/) holds the mirror's assets. No game code in them creates a service with `new`; only the container does:

- `Assets/Harness/StripHarnessServices.cs`: the services, among them a class with a parameterless and a longer constructor, one whose only constructor takes parameters, one that takes the `ContainerScope` and registers the rest through it, a nested class, a class that only the player compiles, a class nothing uses, and a consumer with every kind of `[Inject]` member: private and base-class fields, auto and explicit properties, keys, another core, an optional missing one, `IEnumerable`, `Func`, `Lazy`, and `IObjectInjector`. Three more classes each declare one required `static` `[Inject]` member that no code reads or writes: a field, a property, and a field on a base class. A test of the tooling checks that their names appear nowhere else but in the messages the boot expects, since any reference would keep the member in the player without the link.xml. One more declares an instance `[Inject]` field that no code reads or writes, for a transient service whose constructor counts the injections; a test of the tooling checks that the field's name appears only in its declaration, since the linker keeps the `[Inject]` attribute of a field that code reads. Another declares an instance `[Inject]` auto-property with a private setter that no code reads or writes, for a transient service counted the same way; a test of the tooling checks that the property's name appears only in its declaration, since a call to its getter or setter would keep it in the player without the link.xml. For each of these five classes, and the base class, the tests also check that the class is named nowhere else but in its declaration, where the boot creates it, the message the boot expects, and, for the base class, the derived class's declaration, since reflection on the class, such as `typeof(...).GetField`, can keep its member too. These checks read every file under `Assets/` and `Plugin/`, the code a harness player is built from, and skip whole-line comments.
- `Assets/Harness/StripHarnessBoot.cs`: creates itself in an empty scene, creates two cores, runs the checks after ten frames, destroys the core to check `Dispose`, and quits with 0 only when every check passed and nothing logged an error or exception.
- `Assets/Domain/`: an assembly that does not reference `Mu3Library.DI`, whose classes `Assembly-CSharp` registers.
- `Plugin/HarnessPlugin.cs`: compiled with the Editor's own Mono compiler into `Assets/Plugins/HarnessPlugin.dll`, a precompiled assembly that does not reference `Mu3Library.DI` either.
- `Assets/Editor/StripHarnessBuild.cs`: the `-executeMethod` entry that sets the backend and the stripping level and builds the player.

The checks cover the core (its initialized and prepared events, an `[Inject]` field and a private-set property), registrations and constructors (the default constructor, the longest constructor, a `ContainerScope` parameter, a keyed registration without the `new()` constraint, a class with only a parameterized constructor, a factory with field injection, an instance with property injection), where a constructed class lives (the DI-free assembly, the precompiled DLL, a nested class, a player-only class), member injection (each kind of member above, and a player-only `[Preserve, Inject]` property), the required `static` members (injecting each of the three classes through `IObjectInjector` throws the exception that names its member, which the container finds only when the player kept the member), the instance `[Inject]` field that no code reads (injecting its class through `IObjectInjector` creates one service, which the container does only when the player kept the field's `[Inject]` attribute), the instance `[Inject]` auto-property that no code reads (injecting its class through `IObjectInjector` creates one service, which the container does only when the player kept the property, its setter, and its `[Inject]` attribute), and the lifecycle (`Initialize`, at least five `Update` and `LateUpdate` calls, and `Dispose`): 36 in all.

`--work-dir`, `log/di-strip-harness` by default, holds the mirror in `project/` (`project-<version>/` with `--editor-version`), the player in `players/<label>/`, and Unity's logs as `<label>-build.log` and `<label>-player.log`. The label is `<backend>-<level>`, followed by `-unitask` with `--with-unitask` and by `-<version>` with `--editor-version`, such as `mono-high-unitask` or `mono-medium-6000.3.17f1`. Each run brings the mirror up to date and keeps its `Library/`, so only the first run imports it from scratch. The mirror's `Packages/manifest.json` holds the URP project's built-in modules and uGUI, Base from this checkout as a `file:` path, and with `--with-unitask` the UniTask commit that the Built-In project's lock file pins; without `--editor-version` its `ProjectVersion.txt` is the URP project's, so the harness runs the Editor that project names, or `UNITY_EDITOR`. No tracked project is opened or changed. Keep `--work-dir` short on Windows: the command warns when the mirror path passes 100 characters, which a deep worktree reaches, since Unity's `Library/` and the IL2CPP build add their own long paths below it.

The harness changes and deletes files only in a work folder it made. It writes the marker file `.mu3-di-strip-harness` into a `--work-dir` that does not exist or is empty, and refuses any other folder without that file before it touches anything, so a wrong `--work-dir` cannot cost a project its assets. A work folder made before the marker existed, such as an older `log/di-strip-harness`, is refused too: delete it yourself or pass another `--work-dir`, and the next run imports a new mirror.

The command exits 0 only when the player built, exited 0 within `--player-timeout` (120 seconds by default), logged every check as passed and as many checks as it counted, and logged no error or exception; 1 only when the player ran and a check failed or it did not report; and 3 when the run stopped around the player instead: the work folder was refused, the mirror or the previous player and logs could not be prepared or removed (a file another process holds, a missing permission, a malformed or incomplete package manifest or lock file, or a malformed package file of the Editor `--editor-version` names), the build failed or passed `--build-timeout` (3600 seconds by default), or the player could not be started. Exit 3 prints one `error:` line with the cause rather than a traceback, so automation does not read a setup problem as a DI regression. Once the player ran, its checks decide the exit code: nothing after them turns it into 3. It also prints the total size of the managed DLLs for Mono and of `GameAssembly.dll` for IL2CPP, and only warns when it cannot read them.

### Measured Runs

Every run was on the measurement PC (AMD Ryzen 7 5800X3D, Windows 11 Pro 26300), which has had Visual Studio 2022 Build Tools (MSVC 14.44) for IL2CPP since 2026-11-01. A run on a new mirror imports it first, so it takes longest; later runs on that mirror keep its `Library/`. Exit is the command's exit code, which passes only at 0. A check count is out of the checks the harness had on that date. Time is the whole command, with the build's share where it was recorded. Load is how many other Unity processes ran beside it. Size is the total of the managed DLLs for Mono and `GameAssembly.dll` for IL2CPP.

| Date | Editor | Backend | Level | UniTask | Exit | Checks | Mirror | Time | Load | Size (bytes) | Source |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 2026-11-01 | 6000.6.7f1 | Mono | High | no | not recorded | 31 of 31 | new | about 80 s | none | not recorded | #232 |
| 2026-11-01 | 6000.6.7f1 | Mono | High | no | not recorded | 31 of 31 | imported | about 34 s | none | not recorded | #232 |
| 2026-11-01 | 6000.6.7f1 | IL2CPP | Medium | no | not recorded | 31 of 31 | imported | 76 s | two other Unity batch runs | 9,581,056 | #231 |
| 2026-11-01 | 6000.6.7f1 | IL2CPP | High | no | not recorded | 31 of 31 | imported | 52 s | two other Unity batch runs | 8,512,000 | #231 |
| 2026-11-01 | 6000.6.7f1 | Mono | Medium | no | not recorded | 31 of 31 | imported | not recorded | two other Unity batch runs | not recorded | #231 |
| 2026-11-01 | 6000.6.7f1 | Mono | High | no | not recorded | 31 of 31 | imported | not recorded | two other Unity batch runs | not recorded | #231 |
| 2026-11-01 | 6000.6.7f1 | Mono | Medium | no | not recorded | 31 of 31 | not recorded | not recorded | not recorded | not recorded | #281 |
| 2026-11-01 | 6000.3.17f1 | Mono | Medium | no | not recorded | 31 of 31 | new | about 72 s | no other Unity batch run | not recorded | #281 |
| 2026-11-01 | 6000.3.17f1 | Mono | High | no | not recorded | 31 of 31 | imported | about 24 s | no other Unity batch run | not recorded | #281 |
| 2026-11-04 | 6000.3.17f1 | IL2CPP | Medium | no | 0 | 31 of 31 | new | about 106 s, 78 in the build | none before, between, or after the two runs | 22,208,000 | #299 |
| 2026-11-04 | 6000.3.17f1 | IL2CPP | High | no | 0 | 31 of 31 | imported | about 69 s, 55 in the build | none before, between, or after the two runs | 21,022,720 | #299 |
| 2026-11-04 | 6000.6.7f1 | Mono | Medium | no | 0 | 34 of 34 | imported | 25 s | none at the start | 7,717,376 | PR #357 |
| 2026-11-04 | 6000.6.7f1 | Mono | High | no | 0 | 34 of 34 | imported | 28 s | none at the start | 7,428,608 | PR #357 |
| 2026-11-04 | 6000.3.17f1 | Mono | Medium | no | 0 | 34 of 34 | new | 70 s | one at the start | 9,566,720 | PR #357 |
| 2026-11-04 | 6000.3.17f1 | Mono | High | no | 0 | 34 of 34 | imported | 27 s | none at the start | 9,274,880 | PR #357 |
| 2026-11-04 | 6000.3.17f1 | IL2CPP | Medium | yes | 0 | 34 of 34 | new | 103 s, 72 in the build | none, sampled every 10 s | 22,826,496 | #335 |
| 2026-11-04 | 6000.3.17f1 | IL2CPP | High | yes | 0 | 34 of 34 | imported | 65 s, 53 in the build | none, sampled every 10 s | 21,649,408 | #335 |
| 2026-11-04 | 6000.6.7f1 | Mono | Medium | no | 0 | 35 of 35 | imported | 24 s | two at the start | 7,717,888 | #358 |
| 2026-11-04 | 6000.6.7f1 | Mono | High | no | 0 | 35 of 35 | imported | 31 s | one at the start | 7,429,632 | #358 |
| 2026-11-04 | 6000.3.17f1 | Mono | Medium | no | 0 | 35 of 35 | new | 41 s | none at the start | 9,568,256 | #358 |
| 2026-11-04 | 6000.3.17f1 | Mono | High | no | 0 | 35 of 35 | imported | 24 s | none at the start | 9,276,416 | #358 |
| 2026-11-04 | 6000.6.7f1 | Mono | Medium | no | 0 | 36 of 36 | new | 52 s | one at the start | 7,718,912 | #364 |
| 2026-11-04 | 6000.6.7f1 | Mono | High | no | 0 | 36 of 36 | imported | 28 s | two at the start | 7,431,168 | #364 |
| 2026-11-04 | 6000.3.17f1 | Mono | Medium | no | 0 | 36 of 36 | new | 41 s | two at the start | 9,569,280 | #364 |
| 2026-11-04 | 6000.3.17f1 | Mono | High | no | 0 | 36 of 36 | imported | 25 s | none at the start | 9,276,928 | #364 |

## Environment

The shell entrypoints and `mu3-cli unity doctor` read the same variables, so the Editor the doctor accepts is the one the script runs. Listed in `.env.local`, they are set in every terminal that enters the [development environment](#development-environment).

| Variable | Effect |
|---|---|
| `UNITY_EDITOR` or `UNITY` | Path to a Unity executable (or `Unity.app`) used for every target instead of the version in `ProjectVersion.txt` or the one `--editor-version` names. |
| `UNITY_HUB_EDITOR_ROOT` | Folder that holds one folder per installed Editor version, for a Unity Hub set to install Editors outside its default location (`C:\Program Files\Unity\Hub\Editor`, `/Applications/Unity/Hub/Editor`, or `~/Unity/Hub/Editor`). |
| `TMPDIR` | Where the script creates isolated mirrors. Keep it short on Windows, where a long path pushes Unity's package cache past the path limit. |

On Windows the script runs in the bash of Git for Windows. `mu3-cli` finds it even when only `git` is on `PATH`; see [cli/README.md](cli/README.md#bash-on-windows).
