# Changelog

All notable changes to the kit. Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/); versions: [Semantic Versioning](https://semver.org/). Entry rules and migration notes are owned by [versioning.md](core/wiki/evolution/versioning.md).

## [Unreleased]

## [0.6.0] - 2026-10-31

### Added
- `install` and `update` append a section to `.gitattributes` that checks the kit, the overlay, and the generated agent files out with LF (`text=auto eol=lf`), writing only the lines the file lacks. In a checkout with `core.autocrlf=true` those files had CRLF, so every `update` listed the kit files as modified in `git status` while `git diff` stayed empty. The rule and the command that converts files a clone already holds with CRLF are in «Line Endings» in `installation.md`. (minor)

### Changed
- Stack pack `shell`: read a file's line endings with `git ls-files --eol` or `file`, not by counting `\r` with `grep`, which on Windows may read each CR-LF pair as a plain newline unless given `-U`. (patch)

### Fixed
- `release-cut`: the tag command passes `--cleanup=verbatim`, because `git tag -F` by default deletes lines starting with `#`, so release notes lost their Markdown headings in the tag message. (patch)

### Migration
1. Run `python .ai/kit/tools/agentkit.py update` once more with the same `--from` and `--ref`: the run that installed this version still used the previous `agentkit.py`, which does not add the `.gitattributes` section of «Line Endings» in `.ai/kit/core/wiki/integration/installation.md`.
2. Run the command in «Line Endings» in `.ai/kit/core/wiki/integration/installation.md`, which checks out again with LF the files this clone still holds with CRLF and changes nothing when there are none. Kit files the update rewrote stay listed in `git status` with an empty `git diff` until they are staged.

## [0.5.0] - 2026-10-30

### Added
- Stack pack `firebase`: CLI and `firebase.json`, Emulator Suite, Firestore and Security Rules, Cloud Functions, Auth, Storage, Crashlytics, and FCM, with FlutterFire and Unity SDK setup. The `infra` kind now also covers cloud platforms and managed backend services (`stack-spec.md`). (minor)
- Stack pack `aws`: credentials, IAM, Regions, tags, and cost; Lambda, API Gateway, DynamoDB, Cognito, S3, and CloudFront; Terraform through the `terraform` pack. It has no default paths: bind it to a role or set `[stack_paths]` in the profile. (minor)

### Changed
- Stack pack `csharp`: in a Unity project, settings come from what Unity compiles with (`Assets/csc.rsp`, `.ruleset` files or `.editorconfig`, analyzer plug-ins) rather than the generated `.csproj`, and nullable reference types follow the project. Async code picks `Awaitable`, `Task`, or `ValueTask` by Unity's criteria, never awaits one `Awaitable` twice, keeps per-frame functions synchronous, and cancels with `Application.exitCancellationToken` or `destroyCancellationToken`. Unity Manual sources use the `docs.unity.com/en-us/engine/current/` addresses. (minor)
- Stack pack `unity-upm`: Unity 6.3–6.6 package changes. Create package writes a `.sample.json` that makes packing ignore the `samples` array, and samples gain `images` (6.5). Packages are signature-checked from 6.3, with the signing methods listed. Unity 6.5 drops bundled copies of four class-library assemblies, and 6.6 turns more packages into core packages. Unity Mathematics (6.5) and Burst (6.6) become built-in modules. Unity 6.6 deprecates `DEVELOPMENT_BUILD` and `UNITY_64`; its guide plans their removal in 6.8, and the Unity 7.0 alpha (`7000.0.0a12`) already leaves them undefined, so `#if` blocks on them compile out there and their `#else` branches always compile. The pack names the replacement defines, and code that must stay out of release Players checks `Debug.isDebugBuild`. Packages that also support older Editors pick between the new defines and the older fallbacks with `UNITY_6000_6_OR_NEWER`. New conventions cover `license` and `LICENSE.md`, the 50-character name limit, and `scopedRegistries` scopes. (minor)
- Stack pack `unity-upm`: Export adds the tilde to `Samples` only, so authors rename `Documentation` to `Documentation~` in the Project window before sharing. Git LFS pointer files can arrive even with the LFS client installed, because the Package Manager clones shallowly. The Samples note follows the Unity 6000.6 manual, and unversioned Unity Manual sources use the `docs.unity.com/en-us/engine/current/` addresses they redirect to. (patch)
- Stack pack `unity`: an open Editor that does not answer (Safe Mode after compile errors, a sandbox that hides it, a modal dialog) no longer counts as "no Editor", and `com.unity.pipeline` in the manifest marks the Unity CLI as the live-Editor bridge. A raw `-runTests` verdict comes only from the results XML, Unity CLI exit codes are read by their documented meaning (`unity test` 0, 8, and 6; `unity recompile` 7), and a change made through an Editor needs a clean console, with advancing frames before Play Mode counts as evidence. The CLI's dialog codes come with the Editor versions that report them. Batch runs with asynchronous work omit `-quit`; CLI worktrees can be seeded, with one Editor each. (minor)
- Stack pack `unity`: Unity 6.3–6.6 changes. `[SerializeField]` applies only to fields (6.3). `InstanceID` APIs give way to the 8-byte `EntityId`, and the obsolete shortcut properties and `AddComponent(string)` are gone (6.5). Dictionary fields serialize from 6.6 and migrate only by copying their data. New 6.6 projects skip the domain reload on entering Play Mode, and `[AutoStaticsCleanup]` resets marked statics from 6.5. The Editor log moves into the project (6.5), the Built-in Render Pipeline is deprecated, and 6.6 YAML word-wrapping changes stay out of feature diffs. The class-library copies and deprecated defines link to the `unity-upm` pack, the `6000.x` version note names the current release streams, and unversioned Unity Manual and Scripting API sources use the `docs.unity.com/en-us/engine/current/` addresses. (minor)

### Fixed
- Stack pack `unity`: the Unity CLI version note names the latest beta, 1.0.0-beta.14 (2026-10-21); there is still no stable release. (patch)
- Stack pack `unity`: Unity 6.3, not 6.4, removes URP Compatibility Mode (6.4 removes the define used to convert a project); render pipeline detection reads a Quality level's asset before the Graphics default; scripts under the top-level `Assets/Plugins` folder compile into the `-firstpass` assemblies, and asmdefs can reference none of the predefined assemblies; the Unity 6.2 malformed-YAML note names general parse errors instead of invalid IDs and says earlier versions often, not always, discard malformed data. (patch)
- Stack packs `unity` and `unity-upm`: binding a package folder outside `Assets/` and `Packages/` with `stack_paths` keeps the pack's default globs the project still needs, because `stack_paths` replaces a pack's defaults; the profile template's `[stack_paths]` note now states the replacement. (patch)
- Stack pack `csharp`: Unity projects compile C# 9 at the .NET Standard 2.1 or .NET Framework 4.8 API level, so the pack names the C# 9 features Unity rejects and forbids `record` in serialized types. There it also drops `record` for immutable data and `TimeProvider` (unless the project includes `Microsoft.Bcl.TimeProvider`), and the `unity` pack's Verify replaces the `dotnet` commands. (patch)

### Reviewed
- Stack pack `unity`, ahead of its due date because an intake record named it. (patch)
- Stack pack `unity-upm`, against the Unity 6000.6 manual, ahead of its due date because an intake record named it. (patch)

### Migration
1. If `[stack_paths]` in `.ai/project/profile.toml` has a `unity` or `unity-upm` entry, add to that entry each of the pack's default globs listed in `.ai/kit/core/stacks/CATALOG.md` that matches project files the entry does not already cover. From v0.2.0 to v0.4.0 those packs advised binding a package folder there, and an entry replaces the pack's defaults, so such an entry dropped globs like `**/Assets/**/*.cs`.

## [0.4.0] - 2026-10-24

### Added
- Idea proposals before adoption, in three scopes: skills `new-content-propose` (new content across the product), `content-expansion-propose` (a new element inside one content, starting from a survey of comparable products), and `content-revision-propose` (one value or rule of a unit, starting from a status table of the content). They hold only the steps; `requirements-analyst` owns the proposals, and `requirements-spec-write` routes ideas not yet adopted to them. (minor)
- Page `idea-evaluation.md`: scopes, the project values each scope reads from `.ai/project/wiki/product-planning.md`, the verdicts `reject`, `defer`, and `pass` (passing is not adoption), the eight criteria and their order, hand-overs to another scope or owner, choosing when the user delegates the choice, and boundaries. (minor)
- Page `product-evidence.md`: evidence kinds and their fields, time windows, rules for every read of production data, reading existing metric sources by cost, stopping on blocked sign-in, and attributing an effect. `data-analyst` and `requirements-spec-write` cite it; new analytical queries stay with `data-analyst`. (minor)
- Page `idea-records.md`: idea records in the main checkout's ignored `.ai/tasks/ideas/`, file names, the domain survey and status table, proposal sections per scope, the defer record, judging again, and the report. `memory-policy.md`, `planning.md`, `git-workflow.md`, and `installation.md` route to it, and `request-lifecycle.md` counts judging an idea as work without tracked file changes. (minor)
- Page `change-scope.md`: how much a change may touch: assumptions named before editing, the smallest sufficient change (no unrequested features, options, or single-use abstractions), edit boundaries inside touched files, and cleanup of only what the change left unused. Routed from a new `START.md` route and «Stop First» line (rendered into every `AGENTS.md`), `request-lifecycle.md`, `code-changes.md`, and the code review checklist. (minor)
- Page `domain-terms.md`: one canonical term per concept, its definition, the synonyms to avoid, and using the terms in code, docs, and conversation. The glossary template gains an `Avoid` column; existing glossaries may add it. `localization-specialist` now owns only renderings, keep-as-is terms, and style notes in the glossary; the unit that settles a term writes its entry. (minor)
- Skill `decision-interview`: settles several user-owned decisions in rounds ordered by their prerequisites, looks facts up instead of asking for them, and gives a recommended answer per question; listed on `requirements-analyst` and `software-architect`, and referenced from the Confirm step of `request-lifecycle.md` and from `requirements-spec-write`. (minor)

### Changed
- Writing rules, all additive: routes name one situation per distinct case, material only some cases need moves behind a link, sentences that change no behavior are deleted, established terms win over coined ones, and prohibitions stay only as hard limits (`agent-first-writing.md`); skill steps end on a checkable condition (`skill-spec.md`); repository files own the facts they state (`ssot.md`), so `codebase-onboard` links commands and file lists instead of copying them into the overview; existing `overview.md` files may replace copied commands with links. (minor)
- `bug-diagnose`: the reproduction must show the reported symptom, intermittent failures are raised to a usable rate, ranked candidate causes are tested one variable at a time, and temporary logging carries a marker that is searched away. A bug no test level can reproduce is verified by its original reproduction and gets a follow-up for the missing test seam (`verification.md`). (minor)
- `test-add`: expected values come from a source independent of the code (characterization tests excepted), and only what the test cannot control is substituted. `code-review-perform` adds a scope check and cites the project's documented conventions. (minor)
- `adr-write` applies only to decisions that are costly to reverse, chosen among real alternatives, and surprising without their reason. (minor)
- `code-changes.md`: an interface includes defaults, error behavior, ordering, side effects, and performance characteristics; a change to one is breaking when a caller relying on it within the documented guarantees would fail. (minor)
- `issues-and-prs.md`: issues describe behavior and interfaces rather than paths and may name what is out of scope; pull request risks state who could be affected and whether a revert fully undoes the change; "already implemented" is a close reason (also in `github-issue-triage`); work the unit's issue or acceptance criteria already require is fixed, not registered as a leftover. (minor)
- Handoff packets state the objective and criteria and point to everything else (`handoff-contract.md`); compacting or handing off happens only between steps, after the running summary records decisions with their reasons (`context-budget.md`, plan template). (minor)
- A rule a tool can check mechanically becomes an automated check instead of a lesson (`memory-policy.md`, `kit-lesson-capture`). `work-decompose` sizes units to one session and splits out preparatory restructuring per `change-scope.md`. (minor)

## [0.3.0] - 2026-10-09

### Added
- `check` warns about template placeholders left unfilled in agents, skills, stack packs, and pages, and about rules pages without `applies_to`; `new page rules/<name>` scaffolds an empty `applies_to`. (minor)

### Fixed
- The duplicate-sentence check ignores frontmatter, so unfilled scaffolds no longer report each other as duplicates. (patch)
- `git-worktree-cleanup` updates the main checkout before deleting the task branch; `git branch -d` refused a just-merged branch while the local integration branch was behind. (patch)

## [0.2.0] - 2026-10-09

### Added
- `sync` and `check` report unmanaged instruction files: files in the locations AI tools read instructions from (listed in `tool-adapters.md`) that agentkit did not generate; profile key `tools.keep_unmanaged` acknowledges files kept on purpose. (minor)
- `check` warns when the profile repeats kit defaults and when `.editorconfig` forces a byte-order mark without the agentkit section; `update` reports profile keys that still hold a default the update changed. (minor)
- `project.guardrails`: one-line project rules for every task, rendered under «Project Rules» in `AGENTS.md`; the `AGENTS.md` budget rises to 120 lines. (minor)
- Path-scoped project rules: pages in `.ai/project/wiki/rules/` with `applies_to` render as path-scoped rule files for Claude Code, Copilot, and Cursor (budget 80 lines). (minor)
- Named commands bound to roles: `commands` in `[bindings.<agent>]` lists keys under `[commands]` (validated) and is rendered into that role's «Project Binding». (minor)
- `hosting.ci`: projects without CI record verification in pull requests, and promotion requires the full verification on a fresh checkout of the integration head. (minor)
- Worktree setup cost: `commands.worktree_setup` runs in each fresh worktree; `capacity` reports free disk; `git-workflow.md` says when to turn worktrees off. `git-worktree-cleanup` also handles projects with worktrees off. (minor)
- Localized doc paths: `docs.locale_pattern` takes `{stem}` and `{ext}`, and `[docs.locale_paths]` sets a path per document; unknown placeholders are errors. (minor)
- Release scope for repositories with separately versioned packages: `[release] tag_pattern` and `[[release.packages]]` (name, version file, changelog, tag pattern), applied by `release.md` and `release-cut`. (minor)
- Stack pack `unity-upm` for authoring Unity Package Manager packages: package layout and naming, manifest fields, assembly definitions and `versionDefines`, samples, Git URL installs (`?path=` before `#revision`), local and embedded packages, package tests, and per-package tags. (minor)

### Changed
- Stack pack `unity`: scenes, prefabs, and serialized assets change by a decision order (live Editor → batch-mode Editor script → direct YAML only as a declared exception that never touches prefab instances or overrides) with mandatory verification after any direct edit; packages outside `Assets/` and `Packages/` are bound with `stack_paths`; fresh worktrees lack `Library/` and untracked links; mass reserialization stays out of feature changes. (minor)
- Agents with `extends` render the project card first and the base role one heading level down under «Base Role», with a «Scope» note; the project card's «Owns» replaces the base «Owns». (minor)
- `install` writes a minimal profile (only the project's own keys), appends a BOM-free section to an `.editorconfig` that forces `utf-8-bom`, and exits with code 2 when it installed but hand-written instruction files need adoption. (minor)
- `kit-install` runs the installation as one tracked unit (issue, task branch, pull request into the current default branch), adopts every file `install` and `check` report, and after the merge proposes repository settings and a label review. (minor)
- `check` no longer flags link-only list items as duplicates, and warns when an active role lists skills the project disabled. (patch)

### Fixed
- Files with a UTF-8 byte-order mark are read correctly; editors saving under `charset = utf-8-bom` no longer break frontmatter or cause generated-file drift. (patch)

### Migration
1. Run `agentkit.py check` and adopt every reported unmanaged instruction file per «Adopt Existing Instructions» in `installation.md`, or list it in `tools.keep_unmanaged`.
2. Remove the profile keys `check` reports as repeating kit defaults, so future default changes apply.
3. If `.editorconfig` sets `charset = utf-8-bom`, append the agentkit section shown in «Editor Settings» in `installation.md`.

## [0.1.0] - 2026-10-09

First release.

### Added
- Core wiki with one owner per rule: principles (single source of truth, agent-first writing, integrity), operating model (architecture, request lifecycle, routing, delegation, concurrency, handoff contract, context budget, memory), workflows (planning, Git, issues and pull requests, code changes, verification, review, documentation, translation, release), evolution (freshness, change intake, tech radar, model calibration, versioning, external guidance), integration (installation, project overlay, tool adapters), and authoring specs.
- 36 agent roles defined by surface across 11 departments (governance, product, client, server, data-ai, platform, specialty, documentation, quality, release, evolution), each composed with stack packs and project bindings when rendered, with boundaries audited for overlap.
- 40 skills: Git and worktrees, GitHub issues and pull requests, refactoring, code, dependency, and schema migration, bug diagnosis, tests, performance, code, security, and accessibility review, documentation and decision records, translation, planning and decomposition, requirements, release, CI triage, and kit maintenance and evolution.
- 33 stack packs for languages, frameworks, and infrastructure, each verified against official sources as of 2026-10.
- Issue-first workflow: every unit gets an issue and its own worktree from `develop`, and returns through a pull request into `develop`; `main` changes only through promotion at release. Repository settings policy: `develop` as the default branch and automatic deletion of merged branches.
- Concurrency rules: read and write scope analysis per unit, ordering rules (a unit that reads what another writes waits for its merge), machine capacity per cost class, node budget, and fan-in.
- Translation policy, the `translate` skill, the `localization-specialist` role, and a project glossary: every target translated directly from one source language, localized by genre and tone, with consistent terminology.
- `tools/agentkit.py` (Python 3.11+, standard library only): `install`, `update` (latest release tag by default), `sync` (renders files for Claude Code, Codex, GitHub Copilot, Cursor, and Gemini CLI), `check` (schemas, links, line budgets, generated drift, kit integrity, duplicated sentences), `freshness`, `capacity`, and `new`.
- Keeping the kit current: `volatility` and `reviewed` metadata on every source file with review cadences, a tech radar, change intake, model calibration with `[scaffold]` rules, versioned releases with migration notes, and a weekly CI freshness check.
- README in English, Korean, and Japanese, including a guide to using the kit in another project.
