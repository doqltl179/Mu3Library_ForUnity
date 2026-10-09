# Changelog

All notable changes to the kit. Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/); versions: [Semantic Versioning](https://semver.org/). Entry rules and migration notes are owned by [versioning.md](core/wiki/evolution/versioning.md).

## [Unreleased]

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
