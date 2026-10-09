---
name: release-cut
description: "Cut a requested release: confirm scope, choose the version, finalize the changelog, bump version files, commit, tag with release notes, and publish only after explicit confirmation. Use when the user or an approved plan asks to release integrated work from the release branch."
category: release
volatility: evolving
reviewed: 2026-10-09
---

# Cut Release

## Use When
- The user or an approved plan explicitly asks for a release.
- The work to ship is integrated on `policy.release_branch`.

## Do Not Use When
- No release was requested → report release readiness instead of releasing.
- Adding an Unreleased changelog entry for one change → `project-docs-sync`.
- CI is failing on the release branch → `ci-failure-triage` first.

## Inputs
- The release request and its scope: included work and target channels.
- `policy.release_branch`, `policy.protected_branches`, `docs.changelog`, `docs.locales`, `commands.build`, and `commands.test` from the profile.
- The previous release tag: `git describe --tags --abbrev=0`.

## Steps
1. Confirm the release was requested and its scope fits «Release Unit» in [release.md](../../wiki/workflows/release.md). If either is unclear, stop and ask.
2. Run «Preflight» in [git-workflow.md](../../wiki/workflows/git-workflow.md) in the main checkout. Confirm the latest CI run on the integration branch passed: `gh run list --branch <integration_branch> --limit 1`.
3. List what ships: `git log --oneline <previous-tag>..origin/<integration_branch>` and the Unreleased section of `docs.changelog`. Flag commits that have no changelog entry.
4. Choose the version per «Version Numbers» in release.md.
5. Start the version unit per «Release Unit» in release.md: its issue (when `policy.issue_first` is on), then its worktree with `git-branch-start`.
6. Finalize `docs.changelog` per «Changelog Finalization» in release.md, and its localized variants per «Localized Variants» in [documentation.md](../../wiki/workflows/documentation.md).
7. Bump every version declaration: find them with `rg -n "<current-version>"`, and change only the project's own version fields, never dependency versions.
8. Run `commands.build` and `commands.test` and record results per «Evidence Format» in [verification.md](../../wiki/workflows/verification.md). Stop on any failure.
9. Commit with `git-commit`, open the pull request into the integration branch with `github-pr-create`, and wait until it merges. When `policy.release_branch` differs, open the promotion pull request per «Promotion» in release.md (`gh pr create --base <release_branch> --head <integration_branch>`) and wait until it merges.
10. Draft the tag and release notes per «Tags And Notes» in release.md for the release commit on `policy.release_branch` (the commit the promotion produced, or the merged version commit when both branches are the same). Create the annotated tag locally: `git tag -a <tag> <commit> -F <notes-file>`.
11. Show the user the version, tag, commit ID, notes, and publish targets, and get explicit confirmation per «Confirm Before Irreversible Or Outward Actions» in [integrity.md](../../wiki/principles/integrity.md). Approval covers only the actions shown.
12. After confirmation, push the tag (`git push origin <tag>`) and publish per «Publishing» in release.md. Verify the result, for example `gh release view <tag>`.

## Output
- The version, tag, release commit ID, and finalized changelog section.
- The release URL and published artifacts, or the step where the release stopped and what it awaits.
