---
owns: "How the three Mu3Library packages are released beyond the profile's [[release.packages]]: the root changelog headers, install URLs, and GitHub Release titles and notes"
volatility: evolving
reviewed: 2026-10-09
---

# Package Release

The kit's [release.md](../../kit/core/wiki/workflows/release.md) owns the release unit, promotion, and version rules, and the `release-cut` skill walks it. Each package's version file, package-local changelog, and tag pattern are in `[[release.packages]]` in the profile. This page records the remaining formats.

## When

- a release, version bump, tag, or GitHub Release of a package is requested.

## Route Away When

- the changelog's localized copies: «Human-Facing Docs» in [overview.md](overview.md).

## Changelogs And Titles

The detailed notes go in the root `CHANGELOG.md` (and its localized copies) under a per-package header. The package-local `CHANGELOG.md` gets a short entry for the same version that points to the root entry, in the same commit.

| Package | Root `CHANGELOG.md` header | Release title |
|---|---|---|
| `base` | `## [base/X.Y.Z] - YYYY-MM-DD` | `[Base] vX.Y.Z` |
| `urp` | `## [urp/X.Y.Z] - YYYY-MM-DD` | `[URP] vX.Y.Z` |
| `watermelon` | `## [game/watermelon/X.Y.Z] - YYYY-MM-DD` | `[Watermelon Game] vX.Y.Z` |

- Plain `vX.Y.Z` tags (`v0.0.20` through `v0.6.0`) are historical.
- The Watermelon Game tag path is `game/watermelon` while its commit scope is `watermelon`; keep each as written.

## Install URLs

```text
https://github.com/doqltl179/Mu3Library_ForUnity.git?path=<package folder>#<tag>
```

Update `README.md` and its localized copies when a new tag changes the install guidance.

## GitHub Release

A pushed tag is not a GitHub Release; create one per tag after pushing it, then check it with `gh release view <tag>`.

```sh
gh release create base/vX.Y.Z --title "[Base] vX.Y.Z" --notes-file <notes.md>
```

- Pass notes with `--notes-file`, never inline `--notes`: PowerShell corrupts Markdown code spans in inline arguments.
- `PREV_TAG` is the previous GitHub Release of the same package (`gh release list`), not merely the previous Git tag.

```md
## What's Changed
- ...

## Package
- Version: `x.y.z`
- Install: `https://github.com/doqltl179/Mu3Library_ForUnity.git?path=Mu3Library_Base#base/vX.Y.Z`

## Full Changelog
https://github.com/doqltl179/Mu3Library_ForUnity/compare/PREV_TAG...NEW_TAG
```
