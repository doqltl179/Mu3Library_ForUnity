# Changelog

All notable changes to the kit. Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/); versions: [Semantic Versioning](https://semver.org/). Entry rules and migration notes are owned by [versioning.md](core/wiki/evolution/versioning.md).

## [Unreleased]

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
