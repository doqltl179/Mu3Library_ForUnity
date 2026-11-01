# Repository Workflow Changelog

This document tracks repository-level development workflow and tooling changes that do not belong in package release notes.

Package release notes remain in:
- `CHANGELOG.md`
- `docs/changelog/CHANGELOG.ko.md`
- `docs/changelog/CHANGELOG.ja.md`

## 2026-11-01

### Added
- `test-unity.sh` and `mu3-cli unity test` take `--filter <pattern>`, which they pass to the Unity Test Framework as `-testFilter`, so one test, one fixture, or an `[Explicit]` benchmark such as `MultiplayerPerformanceTests` runs through the script with its lock check and isolated mirror instead of a hand-written Unity command that fails while an Editor has the project open. A target where no test matches the pattern fails, and a pattern that starts with `-` or `/` is refused. `tools/README.md` and `.ai/project/wiki/unity-verification.md` run the benchmark this way.
- `test-unity.sh` stops Unity and fails the target when Unity's log has not grown for `--stall-timeout <seconds>`, 600 by default, which `mu3-cli unity test` also takes, so a test that never returns no longer holds the run and the automation behind it. One `urp` run had stopped writing after `InputSystemManagerTests` and spun on a CPU core for about 20 minutes until it was killed by hand. The script starts Unity with `-automated`, so the Unity Test Framework logs a line as each test begins and ends, and keeps Unity's log beside the results as `log/unity-tests/<target>-<platform>.log`; a stalled run, or one that ends without results, names the test that began and did not end.

## 2026-10-31 (agentkit 0.6.0)

### Changed
- Updated agentkit from 0.5.0 to 0.6.0. The `shell` stack pack now reads a file's line endings with `git ls-files --eol` or `file` instead of counting `\r` with `grep`, which is the lesson `.ai/project/lessons.md` held, so that lesson is gone. `release-cut` tags with `--cleanup=verbatim`, so release notes keep their Markdown headings in the tag message.
- `.gitattributes` ends with the agentkit section that `agentkit.py install` and `update` now maintain, checking the kit, the overlay, and the generated agent files out with LF. It replaces the block added on 2026-10-30 for `.ai/`, `.agents/`, `.claude/`, and `.codex/`, and adds `AGENTS.md`, `CLAUDE.md`, `GEMINI.md`, `.cursor/`, and the agent files under `.github/`. Every tracked file under the added paths is Markdown, which `*.md` already checked out with LF, so none of them needs a new checkout.

## 2026-10-31

### Added
- One command enters the repository's development environment: `. .\tools\dev\activate.ps1` in PowerShell, `source tools/dev/activate.sh` in bash, or the root `.envrc` under direnv. The scripts create `.venv` at the repository root on first use and install `mu3-cli` into it, and all three activate it and set the variables listed in `.env.local`, a new personal and Git-ignored file that `tools/dev/env.local.example` documents. In PowerShell on Windows the script also makes `bash` run the bash that `mu3-cli` uses, normally that of Git for Windows. A variable that a bash example sets for one command, such as `NAME=value bash ./test-unity.sh`, a form PowerShell rejects, now goes in `.env.local` once and holds in either shell; `deactivate` restores everything. `tools/README.md` describes it, and `tools/cli/README.md` now sets up the CLI through it instead of a `.venv` inside `tools/cli`. `.gitattributes` checks `.envrc` out with LF, as it does the `.sh` scripts, since direnv runs it in bash.

### Changed
- `.gitignore` ignores `.claude/worktrees/`, where Claude Code creates its session worktrees. Only a checkout's own `.git/info/exclude` held it before, so a new clone listed those worktrees as untracked. The commented-out `# **/[Ss]amples~/` line, which ignored nothing, is gone.
- `.gitignore` no longer ignores `Assets/builtin_shaders`, the Unity built-in shader source that a checkout could keep in a development project. Base's materials were the only assets that referred to it, and they now use the built-in Standard shader (see `CHANGELOG.md`), so a copy placed there again shows up in `git status`.
- The three Unity lessons that `.ai/project/lessons.md` kept for a kit proposal moved to `.ai/project/wiki/runtime-design.md` instead, so the file now holds only its header. The upstream `unity` pack takes items that prevent compile errors, data loss, or misread verification first and leaves narrow ones out, and none of the three fits it. `Vector2.Lerp` taking one interpolation value is basic API whose misuse fails to compile, the tiled `SpriteRenderer` sizing applies only to 2D tiled and sliced renderers, and the `LocalizationSettings.HasSettings` check only to projects that use the Localization package. The `LocalizationSettings` checks went to «Managers», and the tiled guide line and `CoordinateBounds.Lerp` to «Watermelon Board And Sample».
- The development projects no longer list Unity template packages that nothing in the repository uses. `UnityProject_URP` and `UnityProject_Game_WatermelonGame` drop `com.unity.collab-proxy`, `com.unity.visualscripting`, `com.unity.ai.navigation`, `com.unity.multiplayer.center`, and `com.unity.timeline`, and `UnityProject_BuiltIn` drops `com.unity.multiplayer.center` and `com.unity.timeline`, from `Packages/manifest.json` and `packages-lock.json`. No other package depended on them, so Unity resolves no other package differently. A cold `compile-unity.sh <target> --isolated` run on one machine, once per target, took 66 s for `built-in` (70 s before), 89 s for `urp` (107 s), and 96 s for `watermelon` (116 s).
- `UnityProject_BuiltIn` no longer registers the OpenUPM scoped registry (`package.openupm.com`) that it kept for the removed IvanMurzak Unity-MCP package. No package resolved from it, so the project resolves the same packages. Unity's advisory for the Brotli decoder vulnerability (CVE-2026-111497) advises against adding scoped registries a project does not control until the Editor is updated.

## 2026-10-30

### Changed
- `.ai/project/lessons.md` keeps only the four of its 25 lessons that hold for every project and wait for a kit proposal. The project rules among the rest moved to the pages that own them. Manager callbacks, initialization, disposal, and object ownership, superseded async operations, the Watermelon board commands, and the Watermelon board and sample rules went to the new `.ai/project/wiki/runtime-design.md`, which `rules/package-architecture.md` routes to. The `ContainerScope.InjectInto` boundary and the layout of an oversized `MonoBehaviour` went to `rules/package-architecture.md`. Opening one development project at a time and proving a Base sample's optional-package guards went to `unity-verification.md`; that rule no longer says every development project carries Addressables, Localization, and the Input System, since only `UnityProject_BuiltIn`, where Base's samples compile, resolves every optional package. Two lessons were dropped because a page already owned them: running Unity from a `subst` drive (`tools/README.md`) and keeping reusable runtime code in the packages (`project.guardrails`).
- `.gitignore` no longer lists `**/[Aa]ssets/[Ss]amples.meta` twice.

### Fixed
- A kit update no longer shows unchanged agentkit files as modified on a `core.autocrlf=true` checkout. `agentkit.py update` writes `.ai/kit/` with LF, while such a checkout held its `.py`, `.json`, `.toml`, and extensionless files with CRLF, so reinstalling the same v0.5.0 listed six files in `git status` with an empty `git diff`. `.gitattributes` now checks `.ai/`, `.agents/`, `.claude/`, and `.codex/` out with LF, as agentkit writes them, with `text=auto` so a binary file stays untouched. Git already stored these files with LF, so no file changed in the repository, but a checkout made before this change keeps its CRLF copies until it checks them out again. On a checkout without uncommitted changes in those folders, run from its root:

  ```bash
  git ls-files -z .ai .agents .claude .codex | xargs -0 rm -f
  git checkout -- .ai .agents .claude .codex
  ```

## 2026-10-30 (agentkit 0.5.0)

### Changed
- Updated agentkit from 0.4.0 to 0.5.0. The release updates the `csharp`, `unity`, and `unity-upm` stack packs that this project's roles load: Unity 6.3–6.6 API and package changes, among them the `DEVELOPMENT_BUILD` and `UNITY_64` deprecation in 6.6, the C# 9 features Unity rejects, the Unity CLI as the live-Editor bridge, and reading a `-runTests` verdict only from the results XML. It also adds the `firebase` and `aws` stack packs, which this project does not use. Its one migration step asks a project whose `[stack_paths]` has a `unity` or `unity-upm` entry to add each default glob of the pack that matches files the entry misses; the entries here already cover every tracked file those defaults match, so `profile.toml` is unchanged.

## 2026-10-25

### Fixed
- The first Unity run in a fresh clone or worktree no longer rewrites `UnityProject_BuiltIn/ProjectSettings/EditorBuildSettings.asset`. That tracked file points the Addressables configuration at `Assets/AddressableAssetsData/DefaultObject.asset`, but `.gitignore` excluded the whole folder, so the settings existed only in the main checkout, and a checkout without them got new settings under another GUID and lost the `m_UseUCBPForAssetBundles` line. The BuiltIn project's Addressables settings are now tracked, taken from the main checkout with their GUIDs: the default object, the settings, the profile data source, the group sort settings, the data builders, the group template, and the groups whose entries all point at tracked assets of the Base Template sample (the Localization locales and string tables, `TestPack`, `TestPack02`, `TestScene`, and the default group with `Sample_IS/InputSystemActions`). `AddressableAssetSettings.asset` also carries the two fields that Addressables 2.11.2 adds when it saves the file, `m_ExtractTypeTreeData` and `m_DisableWriteTypeTree`, at their default of 0, because an EditMode run saved it. The per-platform content state (`*/*.bin*`) stays ignored, and so does `AddressableAssetsData` in the other development projects, whose tracked files do not point at it. Updating the main checkout replaces its ignored copies with the tracked files, which hold the same settings, since Git overwrites an ignored file that a merge brings in as tracked. In a new worktree, `commands.worktree_setup` followed by an in-place compile and EditMode test of `built-in` leaves `git status --short` empty.
- A PlayMode run in place no longer leaves `ProjectSettings/ProjectSettings.asset` modified in `UnityProject_URP` and `UnityProject_Game_WatermelonGame`. Both files were still in an older Editor's format (`serializedVersion: 28`), and the Unity Test Framework saves the player settings before it enters Play Mode, so 6000.6.7f1 rewrote them in its own format (`30`) on every run. Each project now holds the file as that run rewrote it, committed on its own. The rewrite does not change either project's behavior in 6000.6.7f1, the Editor both projects pin: a dump of every serialized player setting that 6000.6.7f1 holds after loading the file (2,518 property paths in `urp` and 2,542 in `watermelon`, array elements counted) is identical for the old and the rewritten file, the Android and iOS settings among them. The minimum Android API level already read as 26, the lowest that 6000.6.7f1 offers, where the old file said 25; `AndroidAllowedArchitectures` and the five other removed fields are not part of the new format, and the target architecture stays ARM64; the new fields hold the defaults that already applied, the values `UnityProject_BuiltIn` saved too; and the `i` that now ends each byte-array value, such as `m_StackTraceTypes` and `m_ColorGamuts`, changes only the notation. `tools/README.md` and the project's verification page no longer say to restore the file after a PlayMode run.
- The `TestPack` Addressables group of `UnityProject_BuiltIn` has its own `BundledAssetGroupSchema` and `ContentUpdateGroupSchema`. Its schema set pointed at the two schemas of `TestPack02`, so a bundle or content update setting changed for one group in the Groups window changed the other as well, and now that the folder is tracked, the change would be committed for both. A batch-mode Editor script made the copies through the Addressables editor API (`AddressableAssetGroup.AddSchema`), so `TestPack_BundledAssetGroupSchema.asset` and `TestPack_ContentUpdateGroupSchema.asset` hold the settings of `TestPack02`'s schemas and differ from them only in their name and group, and `TestPack02`'s schemas still point at `TestPack02` alone. In the editor's Use Asset Database mode, which the project plays in, the locations of the Template sample's Addressables labels, their download size and dependency download, and the loads of `TestImage03` and the `SceneThumbnails` entries are the same before and after.

## 2026-10-24 (agentkit 0.4.0)

### Changed
- Updated agentkit from 0.2.0 to 0.4.0. Agents now read `change-scope.md` (how much a change may touch) from `AGENTS.md`, and the kit adds the `decision-interview` skill, the idea-proposal skills `new-content-propose`, `content-expansion-propose`, and `content-revision-propose` with their evaluation, evidence, and record pages, and the `domain-terms.md` page. Existing skills and pages tighten their rules, among them `bug-diagnose`, `test-add`, `code-review-perform`, `issues-and-prs.md`, and `handoff-contract.md`. Neither release carries a migration step. The project skill `development-idea-bank` now only generates a bank of directions to choose from, and sends judging one idea to `new-content-propose` and an adopted idea to `requirements-spec-write`, so a request for an idea no longer matches both skills.

## 2026-10-24

### Added
- `compile-unity.sh`, `test-unity.sh`, and `mu3-cli unity compile` and `unity test` take `--editor-version <version>`, which runs the selected targets with that installed Editor instead of the one `ProjectVersion.txt` names, so the packages' lowest supported Editor (`"unity": "6000.3"`) has a compile path again. The run always uses an isolated mirror and refuses `--in-place`. The mirror's `ProjectVersion.txt` names the requested version, its `packages-lock.json` is removed, and each `com.unity.*` dependency pinned to a version takes the version that Editor bundles; a built-in module it does not ship is dropped. The script prints each change. With 6000.3.17f1, `built-in`, `urp`, and `watermelon` compile and pass their EditMode tests. `tools/README.md` and the project's verification page describe when to run it.
- `test-unity.sh` and `mu3-cli unity test` take `--platform editmode|playmode`, and `commands.test_playmode` runs `bash ./test-unity.sh changed --platform playmode`. `playmode` runs the Unity Test Framework's PlayMode tests, for behavior that only the player loop shows, and writes `log/unity-tests/<target>-playmode.xml`; the default stays `editmode` with its `<target>-editmode.xml`. Messages and the summary name the platform that ran. The first PlayMode tests are Base's, in `Mu3Library_Base/Tests/Runtime` (`Mu3Library.Tests.Runtime`): a core's preparation when its component is disabled, its object deactivated, both in turn, its routine disables it, or it is destroyed while preparing; how the coroutine path steps a routine that yields Unity's wait instructions and nested routines, or that throws or is null; and the `CoreRoot` accessor once the application quits. On 6000.6.7f1 `urp` passes 12 of 12 PlayMode tests, `built-in` 6 of 6, and `watermelon` 6 of 6, where UniTask leaves the `CoreRoot` tests and two UniTask preparation tests; on 6000.3.17f1 `urp` passes 12 of 12. A PlayMode run in place re-saves `ProjectSettings/ProjectSettings.asset` in `urp` and `watermelon`, which still hold an older Editor's format; restore it after the run until #116 settles it. `tools/README.md` and the project's verification page describe the option.

### Changed
- `UnityProject_URP` lists the URP package under `testables`, so `test-unity.sh urp`, and `all` or `changed` with a URP change, runs the URP package's EditMode tests, new in `Mu3Library_URP/Tests/Editor`, where the target used to be reported as skipped. `tools/README.md` and the project's verification page say so.
- `UnityProject_URP` also lists the Base package under `testables`. It is the only development project without UniTask, so it is the only one where Base's code paths without UniTask, such as `CoreBase`'s coroutine preparation, run under test; before, they were only compiled. `test-unity.sh urp` therefore runs the Base EditMode tests as well: 250 tests where it ran 13, 248 passing and 2 skipped, the two that ignore themselves where preparation is a coroutine, which edit mode cannot run. `built-in` and `watermelon` keep their counts, 258 and 296.
- The READMEs no longer repeat package versions in "Recent Updates" and "Package Info", which had fallen three to four releases behind; each version stays in its `package.json` and its notes in `CHANGELOG.md`. The only version strings left are the tags in the Git install URLs, and the release procedure in `.ai/project/wiki/package-release.md` now names updating them in all three README locales in the release commit. The same page fixes one package-local changelog format, which `Mu3Library_Game_WatermelonGame/CHANGELOG.md` now follows like Base and URP.

### Fixed
- `mu3-cli unity compile` and `unity test` run from a `subst` drive open the project on that drive. The CLI found the repository root through `Path.resolve()`, which on Windows turns a subst drive back into the long path it maps, and the shell entrypoints take the project path from their own location, so Unity opened the project on the long path whose `Library/PackageCache` exceeds the 260-character limit. The root now keeps the spelling of the path the CLI was loaded from, and an installed `mu3-cli` takes it from a working directory inside the same checkout. Unit tests in `tools/cli/tests/` pin both, and a run from `K:` logs `Successfully changed project path to: K:/UnityProject_BuiltIn`.
- In-place compile and test runs no longer leave files modified whose line endings alone changed. The Unity Editor writes the YAML of scenes, assets, and `.meta` files with LF, while a checkout with `core.autocrlf = true` held them as CRLF, so every run showed eight settings files across the three development projects as modified. `.gitattributes` now checks the Unity YAML file types out with LF, with `text=auto` so a binary `.asset` stays untouched. Git already stored these files with LF, so no file changed in the repository, but a checkout made before this change keeps its CRLF copies until it checks them out again. Checking them out makes Unity import them again, and `DepthOutline.shader.meta` and `GaussianBlur.shader.meta` in `Mu3Library_URP`, which held only their GUID, now carry the importer section Unity fills in on every such import. On a checkout without uncommitted changes, run from its root:

  ```bash
  git ls-files --eol | awk -F'\t' '$1 ~ /text=auto eol=lf/ {print $2}' | while IFS= read -r p; do rm -f -- "$p"; done
  git ls-files --eol | awk -F'\t' '$1 ~ /text=auto eol=lf/ {print $2}' | git checkout-index -f -u --stdin
  ```

## 2026-10-21

### Added
- `test-unity.sh` and `mu3-cli unity test` run the EditMode tests through the Unity Test Framework CLI, and `commands.test` points at them. They take the targets and options of `compile-unity.sh`, which the new script sources for target selection, the Editor lookup, the lock check, and the isolated mirror, so a project open in an Editor is tested in a mirror. A project runs the packages its manifest lists under `testables`, so `urp` is skipped. A failed test, a failed Unity run, a missing result file, or a run without tests exits non-zero, such a failure does not stop the remaining targets, and NUnit XML lands in `log/unity-tests/`. On 6000.6.7f1 `watermelon` passes 54 of 54 tests and `built-in` 50 of 50.
- `mu3-cli unity link-samples` creates the links from the development projects' `Assets/` to the packages' `Samples~` folders, which a fresh clone or worktree lacked and no command created, and `commands.worktree_setup` points at it. The link paths are a new optional fourth column of `unity-cli-packages.tsv`. The command leaves a correct link alone, replaces a link that points elsewhere, and stops without touching a real folder of the same name. Because Unity compiles the samples only through these links, `compile-unity.sh` and `test-unity.sh` stop before Unity when a selected project lacks its link, unless `--without-samples` is given, and `mu3-cli unity doctor` fails its `Sample link` check. `tools/README.md` gives the setup order for a fresh clone.

### Changed
- All three development projects run on Unity 6000.6.7f1: `UnityProject_BuiltIn` from 6000.3.10f1, `UnityProject_URP` from 6000.3.10f1, and `UnityProject_Game_WatermelonGame` from 6000.3.17f1. The upgrade raised their Unity packages (URP 17.6.0, Input System 1.20.1, Localization 1.5.13, Mobile Notifications 2.5.0, Timeline 6.6.0, uGUI 2.6.0, and others), added the `physicscore2d`, `tetgen`, and `timelinefoundation` modules, and dropped the `vr` module. UniTask is locked to 2.5.11 (`2e993ff`) in the two projects that install it, the first release whose editor tracker window compiles on 6.6. No development project stays on an older editor, so the packages' `"unity": "6000.0"` floor is no longer compiled locally.

### Fixed
- The URP project's `packages-lock.json` records the Base version `Mu3Library_URP/package.json` asks for, so compiling the `urp` target no longer rewrites it.
- `compile-unity.sh` and `mu3-cli unity doctor` no longer take a `Temp/UnityLockfile` left by a crashed Editor for an open project. A project counts as open only while a process holds the file, which PowerShell checks on Windows and `lsof` elsewhere; a stale file is reported as such and the compile runs in place instead of re-importing a mirror from scratch or failing under `--in-place`. `--dry-run` prints where each target would run.
- A mirror that `compile-unity.sh` cannot remove no longer ends the script before it reports Unity's result. Removal retries briefly, then warns with the leftover path; the exit status follows Unity, and the remaining targets still run.
- `mu3-cli unity changes` and `unity compile` run from PowerShell. They found bash only on `PATH`, where Git for Windows does not put it; they now also look next to `git` and in the default install locations, and skip the WSL launcher in `System32`. A missing bash is a usage error that says what to install instead of a traceback from `unity changes`, and `unity doctor` fails its new `Bash` check. `compile-unity.sh` reads `UNITY_HUB_EDITOR_ROOT` as the doctor already did, so both accept the same Editor; `tools/README.md` lists the variables both read.
- `mu3-cli repo info` lists all three packages from `unity-cli-packages.tsv`, the tooling docs and `.gitignore` describe three development projects and the `repo` and `unity` command groups, `tools/cli/README.md` no longer says an in-place compile leaves `Library/` untouched, and the compile selection and repository check no longer special-case the removed root `tasks/` folder.
- `commands.test_tools` passes again: a test pinned the editor versions the 6000.6.7f1 upgrade replaced, and now checks that each project names one.

## 2026-10-11

### Fixed
- `compile-unity.sh changed` selects every development project that consumes a changed package. A package used to select only the project `unity-cli-packages.tsv` pairs it with, so a `Mu3Library_Base` change compiled in `UnityProject_BuiltIn` alone. That project installs every optional package, so code outside its define guard, or an API break against URP or Watermelon Game, passed the compile gate. A project consumes a package when its `Packages/manifest.json` references it: a Base change now selects `built-in`, `urp`, and `watermelon`, and a URP change selects `urp` and `watermelon`. `tools/cli/tests/test_compile_selection.py` runs the script against a throwaway repository to keep the rule in place.

## 2026-10-09 (agentkit 0.2.0)

### Changed
- Updated agentkit to 0.2.0. `profile.toml` now keeps only the values this project sets: three `project.guardrails`, `hosting.ci = false`, per-document localized paths, the three packages under `[[release.packages]]`, the `unity-upm` stack pack, and a `test_tools` command bound to the tooling role.
- The package rules moved to `.ai/project/wiki/rules/package-architecture.md`, which tools load whenever files under `Mu3Library_*/` are touched.
- The Unity YAML guide follows the kit's route order for serialized assets: live Editor, then a batch-mode Editor script, then a declared direct-YAML edit. Samples are edited in place through the development projects' junctions, and the ScreenEffect anchors were re-verified; the `ToonPanel` template the guide named no longer exists.
- `.editorconfig` carries the kit's own BOM-free section for agent files instead of the hand-written one.

## 2026-10-09

### Changed
- The AI-agent docs now come from the shared [agentkit](https://github.com/doqltl179/ai-agents) v0.1.0, installed as a read-only copy in `.ai/kit/`. Project facts moved to the overlay in `.ai/project/`: parameters, active roles, and path bindings in `profile.toml`; package architecture, Unity verification, package release formats, and the Unity YAML guide in `wiki/`; two local roles (`optional-integration-engineer`, `sample-integrity-engineer`), three local skills (`work-issues`, `asmdef-triage`, `development-idea-bank`), and `lessons.md`. `AGENTS.md`, `CLAUDE.md`, and the `.claude/`, `.codex/`, `.agents/`, and `.github/` agent files are generated by `python .ai/kit/tools/agentkit.py sync` and are not edited by hand.
- Units cut their task branch in the main checkout (`policy.worktrees = false`), because each development project's `Library/` takes 1.3–2.3 GB and a new worktree re-imports it.
- `mu3-cli repo check` runs `agentkit.py check` after its hygiene checks. `.editorconfig` keeps agentkit files UTF-8 without BOM, since the kit reads frontmatter from the first byte.

### Removed
- Removed the hand-written agent framework: `docs/ai-agents/`, `.github/agents`, `.github/instructions`, `.github/prompts`, `.github/skills`, `.claude/commands/work-issues.md`, `tasks/`, and the hand-written `AGENTS.md`, `CLAUDE.md`, and `.github/copilot-instructions.md`. Rules the kit already owns were dropped rather than moved; the `mu3-cli agents` command group, which validated that framework's shape, went with it.

## 2026-08-26

### Removed
- Removed the last GitHub Actions workflow, `.github/workflows/branch-strategy.yml`, and with it the `.github/workflows/` directory. Its `validate-branch-flow` job compared only a pull request's base and head branch names against the branch policy. Neither `develop` nor `main` carries branch protection or a ruleset, so the job never gated a merge and a red result would have blocked nothing. The branch policy it echoed is unchanged and stays owned by `docs/ai-agents/workflow/git-workflow.md`, where a wrong destination is a stop condition instead of a failing check. This repository now runs no GitHub Actions at all: verification is local, and the commands and their results belong in the pull request body.

## 2026-08-15

### Changed
- Compile-only verification runs the Unity Editor CLI through `compile-unity.sh` instead of `dotnet build` against the generated Unity `.csproj` files. The isolated mirror that arrived with `mu3-cli unity` removed the reason the batch path was dropped on 2026-05-02, because a compile no longer needs the target Editor closed. A generated `.csproj` also drifts from the sources it claims to build, which silently invalidates the verification it was trusted for.

### Removed
- Removed the `mu3-cli csdevkit` command group. Its `build-profile run` compiled the generated Unity `.slnx` and `.csproj` with `dotnet build` and presented that as compile-only verification, which is the path this repository no longer verifies through. Package-identity and package-version drift validation went with the group and is not covered anywhere else at the moment.

### Fixed
- `compile-unity.sh` runs on Windows. The Unity Hub lookup covers macOS, Windows, and Linux instead of hardcoding the macOS path, the isolated mirror falls back to `cp -RL` wherever `rsync` is absent as it is in Git Bash, and a staging path long enough to push a git-URL package clone past the Windows path limit is reported before Unity fails on it.
- `*.sh` and `*.tsv` are pinned to LF in `.gitattributes`. Under `core.autocrlf=true` a Windows checkout gave `unity-cli-packages.tsv` CRLF endings, bash read the trailing CR as part of the configured project path, and every target failed with a missing-directory error.

## 2026-08-13

### Changed
- Moved repository document layout, README link, stale agent-routing reference, tracked tooling artifact, and agent-framework shape validation into `mu3-cli repo check`.

### Removed
- Removed the post-push repository-hygiene GitHub Actions workflow; repository hygiene is now an explicit local tooling check.

## 2026-08-11

### Changed
- Renamed the outer Python tooling directory from `tools/mu3_cli` to `tools/cli` while retaining the `mu3_cli` import namespace and `mu3-cli` command name.
- Added change-aware Unity compile orchestration and environment diagnostics under `mu3-cli unity`.
- Moved package-identity metadata validation into `mu3-cli csdevkit drift check` and removed the redundant standalone `tools/csdevkit_tests` xUnit project.

## 2026-06-21

### Removed
- Removed package-local `AGENTS.md` documents and matching `.meta` files from the importable `Mu3Library_Base` and `Mu3Library_URP` package surfaces so Unity package import no longer depends on agent-doc assets that are now owned only by the repository root and wiki routing docs.

## 2026-05-20

### Added
- Added tracked `Mu3Library_ForUnity.code-workspace` files for the Built-In and URP development workspaces.
- Added the `mu3-cli csdevkit` workflow for context switching, load diagnostics, curated compile-only build profiles, support bundles, and drift checks.
- Added `tools/csdevkit_tests`, a standalone xUnit project targeting `net10.0` so C# Dev Kit can discover a narrow pure C# metadata test surface without loading Unity package assemblies.

## 2026-05-02

### Removed
- Removed the repository Unity batch compile-gate workflow, scripts, hooks, and editor batch entrypoints because that path required the target Unity editor to be closed.
- Compile-only verification now relies on editor-safe `dotnet build` against the generated Unity `.csproj` files, and the batch SceneLoader smoke entrypoint was removed with that workflow.
