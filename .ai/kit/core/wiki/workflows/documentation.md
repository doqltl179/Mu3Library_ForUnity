---
owns: "Human-facing project documentation policy: doc sync with behavior, changelog entries, which localized variants exist and when they are updated, specifications, and decision records"
volatility: evolving
reviewed: 2026-10-09
---

# Documentation

Human-facing project docs: README, guides, API references, changelog, specifications, and decision records. Agent-facing docs (kit and overlay) follow [authoring/README.md](../authoring/README.md) instead. Paths come from `docs.*` in the profile.

## When

- a change alters behavior, a public interface, configuration, or setup steps,
- writing a specification or a decision record.

## Route Away When

- the steps of syncing docs after a change: the `project-docs-sync` skill,
- release-time changelog finalization: [release.md](release.md).

## Doc Sync Rule

- A change that alters documented behavior updates the owning doc in the same unit of work. The change owner does it; `technical-writer` owns docs-only work and wording passes.
- Document only verified behavior. Examples must match real signatures and run as written.
- Each fact has one owning doc; other docs link to it.

## Changelog

- When `docs.changelog` is set, every user-visible change adds an entry under `## [Unreleased]` in the same unit, in the project's existing changelog format (Keep a Changelog style when none exists).
- Breaking changes say how to migrate.
- `release-manager` moves Unreleased entries into a version at release time; nobody else edits released sections.

## Localized Variants

- For every locale in `docs.locales`, the localized copy at `docs.locale_pattern` is updated in the same unit as its primary doc, translated per [translation.md](translation.md). `localization-specialist` owns the localized copies.
- Keep the same section structure across languages, not only the same text, and link each copy to the others.
- When a translation cannot be produced, mark the localized section as outdated with a visible note rather than leaving it silently stale.

## Specifications

- Specs live in `docs.specs_dir` as `<kebab-slug>.md` and start with a `Status:` line: `draft` until the user approves, then `approved`, later `superseded by <spec>`.
- A spec states problem, users, scope and out-of-scope, testable acceptance criteria, non-functional requirements, and open questions.
- A material change to an approved spec returns it to `draft` until the user approves it again; a changed direction gets a new spec that supersedes the old one.

## Decision Records

- Architecture decisions live in `docs.adr_dir` as `NNNN-<kebab-title>.md`, numbered sequentially.
- Each record states context, options with trade-offs, decision, consequences, and status: `proposed`, `accepted`, `superseded by NNNN`, or `deprecated`.
- Never rewrite an accepted record; write a new one that supersedes it and update the old record's status line only.
