---
owns: "Human-facing project documentation policy: doc sync with behavior, changelog entries, specifications, and decision records"
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

- A changelog is kept when `docs.changelog`, or the `changelog` of a package in `release.packages`, names a file that exists. Every changelog rule in the kit, here and on the pages that link here, applies only to such a file. Never create a changelog file the project does not have. Why: a key that names a missing file, such as a default nobody changed, is no decision to keep a changelog.
- Every user-visible change adds an entry under `## [Unreleased]` in the same unit, in the changelog's existing format (Keep a Changelog style when the file follows none yet).
- Breaking changes say how to migrate.
- `release-manager` moves Unreleased entries into a version at release time; nobody else edits released sections.

## Specifications

- With `docs.specs_dir` empty, the project keeps no spec files: write the requirements, with the content and status this section asks of a spec, in the unit's issue body. Without an issue, keep them in the unit's plan while the work runs, then move them into the pull request body when it opens, or into the merge commit body with `hosting.platform = "none"` («Scaling The Order» in [request-lifecycle.md](../operating-model/request-lifecycle.md)).
- Specs live in `docs.specs_dir` as `<kebab-slug>.md` and start with a `Status:` line: `draft` until the user approves, then `approved`, later `superseded by <spec>`.
- A spec states problem, users, scope and out-of-scope, testable acceptance criteria, non-functional requirements, and open questions.
- A material change to an approved spec returns it to `draft` until the user approves it again; a changed direction gets a new spec that supersedes the old one.

## Decision Records

- With `docs.adr_dir` empty, the project keeps no decision records: state the decision with its context, options with trade-offs, and consequences in the unit's issue or pull request body, or in its merge commit body with `hosting.platform = "none"`.
- Architecture decisions live in `docs.adr_dir` as `NNNN-<kebab-title>.md`, numbered sequentially.
- Each record states context, options with trade-offs, decision, consequences, and status: `proposed`, `accepted`, `superseded by NNNN`, or `deprecated`.
- Never rewrite an accepted record; write a new one that supersedes it and update the old record's status line only.
