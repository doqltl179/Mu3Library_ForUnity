# Changelog

All notable changes to the kit. Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/); versions: [Semantic Versioning](https://semver.org/). Entry rules and migration notes are owned by [versioning.md](core/wiki/evolution/versioning.md).

## [Unreleased]

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
