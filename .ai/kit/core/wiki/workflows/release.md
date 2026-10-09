---
owns: "Project release policy: what a release unit is, promotion from the integration branch to the release branch, version numbers, changelog finalization, tags and notes, and publishing"
volatility: evolving
reviewed: 2026-10-09
---

# Release

Releases of the project the kit is installed in. Kit releases follow [versioning.md](../evolution/versioning.md).

## When

- the user asks for a release, a version bump, a tag, or publication.

## Route Away When

- the procedure: the `release-cut` skill,
- branch rules: [git-workflow.md](git-workflow.md).

## Release Unit

- A release is its own unit, started only by an explicit request. Finishing a feature never implies releasing it.
- The version commit is made in a task worktree like any other change and merged into the integration branch through a pull request.
- When `policy.release_branch` equals `policy.integration_branch`, tag that merged commit.
- When they differ, promote: open a pull request from the integration branch into the release branch, and after it merges tag the resulting commit on the release branch.
- When the release branch receives a commit the integration branch lacks (an urgent fix merged there with the user's approval), merge the release branch back into the integration branch in the same release unit.

## Promotion

- A promotion pull request carries everything merged into the integration branch since the last promotion; its body lists the included pull requests and the changelog section.
- Open one only on request, when CI on the integration branch's head passed and no unit planned for the release is still open.
- Promotion without a version bump is allowed when the project does not version its releases; the rest of this page still applies.

## Version Numbers

- Use the scheme the project already uses; default to Semantic Versioning: breaking change → major, new backward-compatible feature → minor, fix → patch.
- Before 1.0.0, breaking changes bump the minor version.
- Every version file the project keeps (package manifests, version constants) changes in the same commit.

## Changelog Finalization

- Move `## [Unreleased]` entries under `## [<version>] - <YYYY-MM-DD>`, leaving an empty Unreleased section.
- Check that every merged user-visible change since the last tag has an entry; missing entries matter more than polished wording.
- Breaking changes and migration steps appear first.

## Tags And Notes

- Tag format follows the project's existing tags; default `v<version>`. Use annotated tags.
- Release notes summarize the changelog section for that version; do not invent content beyond it.

## Publishing

- Pushing tags, creating hosted releases, and publishing packages are outward actions: confirm with the user before each, per [integrity.md](../principles/integrity.md).
- After publishing, verify the release is visible and installable where that can be checked.
