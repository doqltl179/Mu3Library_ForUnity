---
owns: "Project release policy: what a release unit is, its scope (whole repository or separately versioned packages), promotion from the integration branch to the release branch, version numbers, changelog finalization, tags and notes, and publishing"
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
- The version commit is made on a task branch like any other change, in its own worktree when «Worktrees» in [git-workflow.md](git-workflow.md) gives the unit one, and merged into the integration branch through a pull request.
- When `policy.release_branch` equals `policy.integration_branch`, tag that merged commit.
- When they differ, promote: open a pull request from the integration branch into the release branch, and after it merges tag the resulting commit on the release branch.
- When the release branch receives a commit the integration branch lacks (an urgent fix merged there with the user's approval), merge the release branch back into the integration branch in the same release unit.

## Release Scope

- By default a release covers the whole repository: one version, one tag from `release.tag_pattern`, and one changelog (`docs.changelog`) when the project keeps one per «Changelog» in [documentation.md](documentation.md).
- When `release.packages` lists packages, each is versioned separately with its own `version_file`, `changelog`, and `tag_pattern` (for example `core/v{version}`). A release names the packages it covers; a package with no change since its last tag is left out.
- One release unit may cover several packages; each gets its own version and tag, and its own changelog section when it keeps a changelog.

## Promotion

- A promotion pull request carries everything merged into the integration branch since the last promotion; its body lists the included pull requests and, when the project keeps a changelog, the changelog section.
- Open one only on request, when no unit planned for the release is still open and the integration branch's head is verified: CI passed on it, or with `hosting.ci = false` the full verification (every relevant `commands.*`) passed on a fresh checkout of it, with the results in the promotion pull request body.
- Promotion without a version bump is allowed when the project does not version its releases; the rest of this page still applies.
- With `hosting.platform = "none"` there is no promotion pull request: under the same conditions, after the user approves the merge and the push, switch the main checkout to the release branch and bring it up to date (`git pull --ff-only` when it has a remote), merge the integration branch into it with `git merge --no-ff`, put in the merge commit body what the promotion pull request body would hold, push the release branch when it has a remote, and switch back to the integration branch.

## Version Numbers

- Use the scheme the project already uses; default to Semantic Versioning: breaking change → major, new backward-compatible feature → minor, fix → patch.
- Before 1.0.0, breaking changes bump the minor version.
- Every version file the project keeps (package manifests, version constants; for packages, each `version_file` in scope) changes in the same commit.

## Changelog Finalization

- Apply this section to each changelog in scope that the project keeps per «Changelog» in documentation.md; skip it when there is none.
- Move `## [Unreleased]` entries under `## [<version>] - <YYYY-MM-DD>`, leaving an empty Unreleased section.
- Check that every merged user-visible change since the last tag has an entry; missing entries matter more than polished wording.
- Breaking changes and migration steps appear first, except in the kit's own `CHANGELOG.md` in the kit repository, which keeps the order of «Changelog Format» in [versioning.md](../evolution/versioning.md).

## Tags And Notes

- Tag names come from `release.tag_pattern` (default `v{version}`), or from each package's `tag_pattern`. Use annotated tags.
- Release notes summarize the changelog section for that version or, without a changelog, the pull requests merged since the previous tag (the units' merge commits, with `hosting.platform = "none"`; since the first commit for a first release); do not invent content beyond that source.

## Publishing

- Pushing tags, creating hosted releases, and publishing packages are outward actions: confirm with the user before each, per [integrity.md](../principles/integrity.md).
- After publishing, verify the release is visible and installable where that can be checked.
