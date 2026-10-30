---
name: kit-update
description: "Update the kit installed in a project: record the current version, clear local kit edits, run the updater against the upstream or a local path, read the migration notes between versions, apply the required overlay changes, then render and validate. Use when a newer kit release exists, a migration note or fix is needed, or the user asks to update the kit."
category: kit
volatility: evolving
reviewed: 2026-10-09
---

# Update Kit

## Use When
- A newer kit release exists, or a fix the project needs has shipped upstream.
- The user asks to update the kit.

## Do Not Use When
- The project has no `.ai/kit/` → `kit-install`.
- You are in the kit repository itself → `release-cut` for versioning.
- The project needs a kit change that has not shipped → `kit-upstream-propose`.

## Inputs
- The source: `evolution.upstream` in the profile, or a local kit checkout path.
- The target ref: the latest release by default, or a tag or branch the user names, per «Consumer Updates» in [versioning.md](../../wiki/evolution/versioning.md).

## Steps
1. Read `.ai/kit/VERSION` and record it as the old version.
2. Start a task branch with `git-branch-start` from a clean working tree (`git status --short`), so the update lands as one reviewable change.
3. Run `agentkit.py check`. When it reports edits inside `.ai/kit/`, stop: carry each edit into `.ai/project/` or to `kit-upstream-propose`, and restore the original kit file only after the user confirms.
4. Run `agentkit.py update`, adding `--from <source>` only for a source other than `evolution.upstream` and `--ref <tag-or-branch>` only when the user chose a version other than the latest release, per «Update» in [installation.md](../../wiki/integration/installation.md). Record the version it reports installing.
5. Read every CHANGELOG section the command prints and list each migration note per «Migration Notes» in versioning.md. For every `profile:` line it prints (a key that still holds a default the update changed), ask the user whether to remove the key and adopt the new default or keep it on purpose.
6. Apply each migration step in order, editing `.ai/project/` only. Then `rg` the overlay for every renamed or removed name the notes list and fix any reference the steps missed.
7. Run `agentkit.py sync` to regenerate tool files for the new version, then `agentkit.py check`; fix every error in the overlay, never in `.ai/kit/`.
8. Run any verification the migration notes call for; when a note names a `commands.*` key that is empty, report the gap.
9. Commit with `git-commit` and open a pull request with `github-pr-create` when the user asks.

## Output
- Old and new version.
- Each migration note with the action taken, or the reason it was skipped.
- The `check` result and any kit defect routed to `kit-upstream-propose`.
