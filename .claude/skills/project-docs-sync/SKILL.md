---
name: project-docs-sync
description: "Update the human-facing docs that own a changed behavior or API in the same change: README sections, guides, API reference, the changelog Unreleased entry, and localized variants listed in docs.locales, documenting only verified behavior. Use when a unit changes user-visible behavior, a public interface, configuration, or setup steps."
---
<!-- agentkit:generated from .ai/kit/core/skills/project-docs-sync/SKILL.md. Do not edit: change the source, then run `python .ai/kit/tools/agentkit.py sync`. Paths are relative to the project root. -->

# Sync Project Docs

## Use When
- A unit changes user-visible behavior, a public API or CLI, configuration keys, environment variables, install or setup steps, or supported platforms.
- A review reports docs out of sync with the code.

## Do Not Use When
- Recording why an architectural choice was made → `adr-write`.
- Specifying requirements for new work → `requirements-spec-write`.
- Editing kit files under `core/` or `.ai/project/` → `kit-page-update`.
- Finalizing the changelog for a release → `release-cut`.

## Inputs
- The commit range or diff of the behavior change, and its verification evidence.
- `docs.readme`, `docs.changelog`, `docs.locales`, `docs.locale_pattern`, and `project.language` from the profile.

## Steps
1. List what changed for a reader of the docs: `git diff --stat <base>...<head>`, then read the changed public surfaces (exports, routes, CLI flags, config keys, environment variables, schemas, error messages).
2. Find every doc that states each changed fact: `rg -n "<old name|flag|key|example value>"` across `docs.readme`, the docs tree, API reference sources (docstrings, schema or OpenAPI files), and examples. Edit the owning section per «Doc Sync Rule» in [documentation.md](.ai/kit/core/wiki/workflows/documentation.md).
3. Check each fact you will write against this change's evidence (a test or a quoted command result). Write only verified behavior; list anything unverified as a gap in the report instead.
4. Update the owning sections: prose, examples, option tables, and migration notes for breaking changes. Generated reference docs change per «Generated Code» in [code-changes.md](.ai/kit/core/wiki/workflows/code-changes.md); when no command regenerates them, report the gap.
5. Run each changed example or command snippet that can run locally, or mark it unverified in the report.
6. Add the Unreleased entry to `docs.changelog` per «Changelog» in documentation.md. Skip this step when `docs.changelog` is empty.
7. Update the copy at `docs.locale_pattern` for each locale in `docs.locales` per «Localized Variants» in documentation.md.
8. Stage the doc edits with the code change per «Staging» in [git-workflow.md](.ai/kit/core/wiki/workflows/git-workflow.md) and commit with `git-commit`.

## Output
- Doc files changed, the changelog entry, localized variants updated or marked stale, and behavior left undocumented because it was unverified.
